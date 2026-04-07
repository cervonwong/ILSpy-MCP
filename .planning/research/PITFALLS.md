# Pitfalls Research

**Domain:** .NET decompilation MCP server (ILSpy-MCP feature parity milestone)
**Researched:** 2026-04-07
**Confidence:** HIGH (verified against codebase, ILSpy issues, MCP SDK releases)

## Critical Pitfalls

### Pitfall 1: CSharpDecompiler Instantiation Per Call Prevents Reuse and Causes Memory Pressure

**What goes wrong:**
The current `ILSpyDecompilerService` creates a new `CSharpDecompiler` instance for every single tool invocation (visible in every method: `DecompileTypeAsync`, `DecompileMethodAsync`, `GetTypeInfoAsync`, etc.). Each instantiation loads the PE file, parses metadata, and builds a type system. For bulk operations (decompile_namespace, export_project) and cross-reference analysis (which must scan all types), this means the same assembly gets loaded dozens or hundreds of times. Memory spikes, GC pressure increases, and operations that should take seconds take minutes.

**Why it happens:**
The original design serves single-type lookups where one CSharpDecompiler per call is acceptable. When adding bulk operations, developers often just loop over existing single-item methods instead of restructuring to share a decompiler instance.

**How to avoid:**
- For bulk operations, create one `CSharpDecompiler` per assembly and pass it through the operation
- For the ILScanner service (cross-refs, string search, constant search), use `PEFile` + `MetadataReader` directly instead of `CSharpDecompiler` -- scanning IL opcodes does not require the full decompilation pipeline
- Keep `CSharpDecompiler` instances short-lived within a single request scope (they are not thread-safe)

**Warning signs:**
- Bulk decompile of a namespace with 50 types takes >10 seconds on a moderate assembly
- Memory usage spikes to 500MB+ during export_project on assemblies like System.Private.CoreLib
- GC Gen2 collections visible during cross-reference scans

**Phase to address:**
Phase 1 (SDK upgrades + bug fixes) should restructure the decompiler service to accept an optional shared instance. Phase 2 (IL scanning features) must use PEFile/MetadataReader directly for scanning, not CSharpDecompiler.

---

### Pitfall 2: MCP SDK 0.4 to 1.x Has Multiple Breaking Change Waves

**What goes wrong:**
The upgrade from `ModelContextProtocol 0.4.0-preview.3` to 1.x is not a single-step migration. The SDK went through breaking changes at 0.5, 0.7, 0.9, and 1.0-rc.1, each wave touching different APIs. Developers who target 1.0 directly face a wall of compile errors with no clear migration path. Key breakages include:
- `McpServerHandlers` no longer works as independent options type (0.9) -- must use `McpServerOptions.Handlers`
- Filter registration API completely restructured (0.9) -- `Add*Filter` methods replaced with `WithMessageFilters`/`WithRequestFilters`
- Server back-references removed from protocol DTO types (1.0-rc.1) -- `Tool.McpServerTool` property gone
- Collection properties changed from `List<T>` to `IList<T>` (0.9)
- Binary data changed from `string` to `ReadOnlyMemory<byte>` (0.9)
- Legacy SSE disabled by default in 1.2

**Why it happens:**
Preview SDK to stable SDK transitions accumulate breaking changes. The 0.4 preview API surface was experimental.

**How to avoid:**
- Upgrade incrementally: 0.4 -> latest 0.x preview -> 1.0, fixing compile errors at each step
- The current codebase uses `AddMcpServer()`, `WithStdioServerTransport()`, `WithToolsFromAssembly()`, `[McpServerToolType]`, and `[McpServerTool]` -- verify each of these survives in 1.x
- The tool registration pattern (`[McpServerToolType]` on class, `[McpServerTool]` on method) appears to have survived to 1.x, but parameter passing conventions may have changed
- Write a single integration test that boots the MCP server and lists tools before and after upgrade to verify nothing dropped

**Warning signs:**
- More than 20 compile errors after upgrading the NuGet package
- Tools register but don't appear in client tool listings
- Stdio transport connects but tool calls return protocol errors

**Phase to address:**
Phase 1 (SDK upgrades). Do this FIRST before any new features. Run all existing tests after upgrade to catch regressions.

---

### Pitfall 3: CancellationTokenSource Leak in TimeoutService

**What goes wrong:**
`TimeoutService.CreateTimeoutToken()` creates `CancellationTokenSource` instances that are never disposed. Both the `timeoutCts` and the linked CTS from `CreateLinkedTokenSource` leak. `CancellationTokenSource` registers kernel timer callbacks internally. Under sustained load (many MCP tool calls), this leaks OS handles and memory. The linked CTS also holds a reference to the original cancellation token's registration, preventing GC of the entire call chain.

**Why it happens:**
The method returns a `CancellationToken` (value type) but the `CancellationTokenSource` (disposable) is created locally and never exposed for disposal. This is a common C# pattern mistake.

**How to avoid:**
- Return the `CancellationTokenSource` itself (or a wrapper) so callers can dispose it
- Alternatively, use a `using` pattern in the use case layer where the timeout is created and the operation is awaited
- The fix pattern: use cases should create and own the CTS, wrapping the operation in a `using` block

**Warning signs:**
- Handle count in Process Explorer grows monotonically during sustained tool usage
- `ObjectDisposedException` does NOT occur (because nothing is being disposed -- that is the problem)
- Memory profiler shows thousands of live `CancellationTokenSource` objects

**Phase to address:**
Phase 1 (bug fixes). This is a documented known bug in PROJECT.md. Fix before adding bulk operations which will amplify the leak.

---

### Pitfall 4: IL Scanning Obfuscated/Malformed Assemblies Crashes the Server

**What goes wrong:**
`System.Reflection.Metadata.MetadataReader` throws `BadImageFormatException` on assemblies with metadata that violates ECMA-335. This is by design -- SRM rejects non-compliant assemblies. Obfuscators like .NET Reactor, ConfuserEx, and Dotfuscator can produce metadata streams that trigger this. The crash surfaces as:
- "Illegal tables in compressed metadata stream"
- "Not enough space for Metadata stream"
- "Row ID or heap offset is too large"
- "Invalid SEH header"

For an MCP server, an unhandled `BadImageFormatException` during IL scanning can crash the entire process or leave the server in a bad state.

**Why it happens:**
Developers test with well-formed .NET assemblies (runtime libraries, their own code) and never encounter malformed metadata. IL scanning code iterates method bodies, reads opcode operands, and dereferences metadata tokens -- any of which can throw on corrupted assemblies.

**How to avoid:**
- Wrap all `PEFile`/`MetadataReader` operations in try-catch for `BadImageFormatException`, `InvalidOperationException`, and `ArgumentOutOfRangeException`
- For IL scanning: catch exceptions per-method-body, not per-assembly. One bad method should not abort scanning the entire assembly. Log and skip.
- Use `PEStreamOptions.PrefetchEntireImage` when loading to fail fast rather than getting random IO errors during scanning
- Return partial results with a warning rather than failing entirely

**Warning signs:**
- Users report "server crashed when scanning X.dll" for third-party/obfuscated assemblies
- Integration tests only use BCL assemblies (System.Collections.dll, etc.) and never test error paths

**Phase to address:**
Phase 2 (IL scanning features). Every method in the ILScanner service needs per-method-body exception handling from day one.

---

### Pitfall 5: Bulk Decompilation Produces Unbounded Output That Overflows MCP Response Limits

**What goes wrong:**
`decompile_namespace` on a namespace with 200 types or `export_project` on a large assembly produces megabytes of decompiled C# code. MCP tool responses have practical size limits (clients truncate or timeout on very large responses). The AI assistant receiving the response may also have context window limits that make multi-megabyte responses useless.

**Why it happens:**
Bulk operations are implemented as "loop over all items and concatenate" without considering output size. The existing codebase already has `MaxDecompilationSize` in options (1MB default) but it is only applied per-type, not to aggregate output.

**How to avoid:**
- Implement streaming/chunking for bulk operations: return a summary with type list, then let the AI request individual types
- For `export_project`: write files to disk and return the file paths, not the content
- For `decompile_namespace`: enforce an aggregate size limit and return a truncation warning
- Consider pagination: "showing types 1-20 of 85, use offset parameter for more"

**Warning signs:**
- Tool responses taking >10 seconds to serialize
- AI assistant receives truncated/garbled output
- MCP client connection drops during large responses

**Phase to address:**
Phase 3 (bulk decompilation features). Design the response format before implementation.

---

### Pitfall 6: ICSharpCode.Decompiler 9.1 to 10.x API Surface Changes

**What goes wrong:**
The upgrade from ICSharpCode.Decompiler 9.1 to 10.x includes breaking changes:
- `ParameterModifiers` replaced with `ReferenceKind` -- any code inspecting method parameters will break
- New `IFileLoader` API changes how assemblies are loaded
- `WholeProjectDecompiler.CreateDecompiler` became `protected virtual` -- relevant for `export_project`
- `IProjectFileWriter` and `IProjectInfoProvider` APIs are now public -- may conflict with custom implementations

The current codebase uses `CSharpDecompiler` directly and accesses `method.Parameters`, `type.Methods`, `type.Properties`, etc. Parameter-related code paths will break.

**Why it happens:**
The decompiler is an active project that refines its API. Major version bumps signal intentional breaking changes.

**How to avoid:**
- Upgrade the NuGet package, fix compile errors, and run all tests BEFORE adding new features
- The `MapToMethodInfo` method in `ILSpyDecompilerService` accesses `method.Parameters` -- verify this still works with the new parameter model
- For `export_project`, use `WholeProjectDecompiler` (now more extensible in v10) rather than rolling your own project export
- Pin to the specific 10.x version, do not use floating version references

**Warning signs:**
- `method.Parameters[0].Type` behaves differently or has different properties
- `DecompilerSettings` has new/removed options
- Decompiled output format changes (different whitespace, different comment styles)

**Phase to address:**
Phase 1 (SDK upgrades). Upgrade decompiler alongside MCP SDK. Both upgrades create a stable foundation.

---

### Pitfall 7: CSharpDecompiler Is Not Thread-Safe

**What goes wrong:**
The ILSpy documentation explicitly states: "Instances of the CSharpDecompiler class are not thread-safe." The current codebase creates fresh instances per call (safe but slow). If bulk operations try to parallelize by sharing a `CSharpDecompiler` across threads, they will get corrupted output or crashes. Even seemingly read-only operations like `TypeSystem.MainModule.GetTypeDefinition` mutate internal caches.

**Why it happens:**
Performance optimization instinct: "this is slow, let me parallelize it." Sharing the decompiler instance across parallel tasks seems like an obvious optimization but violates the thread-safety contract.

**How to avoid:**
- One `CSharpDecompiler` per thread/task for decompilation operations
- For IL scanning (which uses `PEFile`/`MetadataReader` only), `MetadataReader` IS thread-safe for read operations -- parallelize at that level instead
- Use `SemaphoreSlim` to limit concurrent decompilations (the codebase already has `MaxConcurrentOperations` in config, but PROJECT.md notes the semaphore enforcement is broken)

**Warning signs:**
- Random `NullReferenceException` or `InvalidOperationException` during parallel decompilation
- Output from parallel bulk decompilation contains mixed-up type bodies
- Works fine in sequential tests, fails intermittently under load

**Phase to address:**
Phase 1 (fix semaphore enforcement bug). Phase 3 (bulk decompilation must respect concurrency limits).

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| New CSharpDecompiler per tool call | Simple, no lifetime management | Repeated PE parsing, memory pressure on bulk ops | Acceptable for single-type operations, never for bulk |
| Catching all exceptions as AssemblyLoadException | Uniform error handling | Hides real errors (OutOfMemory, StackOverflow, thread aborts) | Never -- distinguish recoverable from fatal exceptions |
| Tests using only BCL assemblies | Easy to set up, deterministic | Misses edge cases with obfuscated/mixed-mode/large assemblies | Acceptable for unit tests, not for integration tests |
| Returning raw decompiled code in MCP responses | Simple implementation | Unbounded output, context window overflow | Only acceptable with size limits enforced |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| MCP SDK tool registration | Assuming `[McpServerToolType]` + `[McpServerTool]` attributes are stable across versions | Verify tool registration with an integration test that lists tools after SDK upgrade |
| ICSharpCode.Decompiler type resolution | Using `FullTypeName` constructor with user input directly | Validate/sanitize type names; handle generic type names (backtick notation like `List\`1`) |
| PEFile loading | Loading assembly without specifying `PEStreamOptions` | Use `PEStreamOptions.PrefetchEntireImage` for scanning operations to avoid file lock issues |
| Cross-assembly analysis | Assuming all assemblies in a directory can be loaded | Some DLLs are native, mixed-mode, or satellite assemblies -- filter and handle failures gracefully |
| MetadataReader token resolution | Dereferencing any metadata token without checking IsNil | Always check `handle.IsNil` before calling `GetMethodDefinition`, `GetTypeDefinition`, etc. |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Creating CSharpDecompiler per type in bulk operations | Linear slowdown, 10s+ for namespace with 50+ types | Share decompiler instance within a bulk operation scope | Any namespace with >20 types |
| Full type system construction for IL scanning | 2-5 second startup per assembly just to scan opcodes | Use PEFile + MetadataReader directly, skip CSharpDecompiler entirely | Any assembly; always slower than needed |
| Unbounded LINQ materialization during cross-ref search | Memory spike when scanning all method bodies in large assembly | Use streaming/yield return, process methods one at a time | Assemblies with >5000 methods (e.g., System.Private.CoreLib) |
| Deep recursion in decompiler on complex methods | StackOverflowException on methods with deeply nested control flow | Set stack size limits, add recursion depth guards, catch and skip | Obfuscated assemblies with flattened control flow |
| String concatenation for bulk output | O(n^2) string building, GC pressure | Use StringBuilder or write directly to TextWriter/Stream | Output >100KB |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Accepting arbitrary file paths from MCP tool input | Path traversal; reading sensitive files outside intended scope | Validate paths, consider allowlisting directories, or at minimum log all file access |
| Loading assemblies from user-specified directories without filtering | Loading and executing native DLLs or assemblies with module initializers | Only load files with valid PE/.NET headers; use PEStreamOptions.PrefetchEntireImage to avoid running code |
| No resource extraction size limits | Embedded resources can be multi-GB (e.g., ML models in assemblies) | Enforce size limits on resource extraction; stream rather than buffer |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Cross-reference search returns raw token references instead of resolved names | AI assistant cannot interpret "calls token 0x0600042A" | Resolve all metadata tokens to human-readable type/method names |
| IL output uses raw metadata tokens without annotations | Users must manually look up what each token means | Add inline comments: `call 0x0600042A /* System.String::Concat */` |
| Bulk decompilation returns everything or nothing | Large results overflow context; empty results on failure give no diagnosis | Return structured results: list of types with status (success/failed/skipped) and decompiled code |
| String search returns line-level matches without context | Impossible to understand where/how the string is used | Return method name + surrounding IL context for each match |

## "Looks Done But Isn't" Checklist

- [ ] **Cross-reference analysis:** Often misses indirect calls (via delegates, reflection, interface dispatch). Verify with `callvirt` on interface types, not just `call` on concrete types.
- [ ] **String search (ldstr):** Misses strings composed at runtime via `String.Concat`, `String.Format`, or interpolation. Document this limitation explicitly.
- [ ] **Constant search (ldc.*):** Misses constants inlined by the compiler from referenced assemblies. The constant value exists in IL but the original `const` field reference is gone.
- [ ] **find_implementors:** Misses explicit interface implementations where the method name differs from the interface method name. Must check `MethodImplementation` table.
- [ ] **IL output:** Often missing exception handler regions (try/catch/finally/fault blocks). Verify these render correctly.
- [ ] **Assembly metadata:** Missing `[assembly:]` custom attributes, `InternalsVisibleTo`, and `AssemblyInfo` equivalents.
- [ ] **Constructor support (.ctor/.cctor):** Methods named `.ctor` and `.cctor` are filtered out by `!m.IsConstructor` in current code (line 77 in `ILSpyDecompilerService`, line 279 in `SearchMembersAsync`). New features must explicitly include them.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| MCP SDK upgrade breaks all tools | MEDIUM | Revert NuGet package version, fix incrementally through intermediate versions |
| Decompiler upgrade changes output format | LOW | Update test expectations; output format changes are cosmetic |
| Memory leak from CTS in production | LOW | Deploy fix, restart server process (MCP servers are per-session) |
| Bulk operation crashes on obfuscated assembly | MEDIUM | Add per-method exception handling, redeploy; partial results already returned need no recovery |
| Thread-safety violation corrupts output | HIGH | Must identify and fix all shared-state access; may require architecture change |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| CTS leak in TimeoutService | Phase 1 (bug fixes) | Memory profiler shows CTS disposal; handle count stable under load |
| MCP SDK breaking changes | Phase 1 (SDK upgrades) | All 8 existing tools pass integration tests after upgrade |
| Decompiler API changes | Phase 1 (SDK upgrades) | Compile succeeds; decompilation output matches expectations |
| Semaphore enforcement bug | Phase 1 (bug fixes) | Concurrent requests properly throttled; test with >MaxConcurrentOperations simultaneous calls |
| Obfuscated assembly crashes | Phase 2 (IL scanning) | ILScanner returns partial results + error list for malformed assemblies |
| CSharpDecompiler per-call in bulk | Phase 2/3 (scanning + bulk) | Benchmark: bulk decompile of 50 types < 5 seconds |
| Thread-safety in parallel operations | Phase 3 (bulk decompilation) | Stress test: 10 concurrent bulk operations produce correct output |
| Unbounded response size | Phase 3 (bulk decompilation) | All bulk responses under configurable size limit with truncation warning |
| Constructor exclusion | Phase 1 (bug fix) | `get_type_members` returns .ctor/.cctor; `decompile_method` accepts constructor names |
| Cross-ref misses interface dispatch | Phase 2 (cross-refs) | Test: find_usages on IDisposable.Dispose returns implementors' call sites |

## Sources

- [ILSpy GitHub - MetadataReader on obfuscated assemblies (Issue #1069)](https://github.com/icsharpcode/ILSpy/issues/1069)
- [ILSpy GitHub - BadImageFormatException illegal tables (Issue #2419)](https://github.com/icsharpcode/ILSpy/issues/2419)
- [ILSpy GitHub - .NET 9 broke decompiler via changed IL (Issue #3396)](https://github.com/icsharpcode/ILSpy/issues/3396)
- [ILSpy Wiki - Unit Tests](https://github.com/icsharpcode/ILSpy/wiki/Unit-Tests)
- [ILSpy GitHub - Performance: NuGet vs DLL decompilation (Issue #1762)](https://github.com/icsharpcode/ILSpy/issues/1762)
- [ICSharpCode.Decompiler 10 preview release notes](https://libraries.io/nuget/ICSharpCode.Decompiler)
- [MCP C# SDK releases](https://github.com/modelcontextprotocol/csharp-sdk/releases)
- [MCP C# SDK v1.0 release blog](https://devblogs.microsoft.com/dotnet/release-v10-of-the-official-mcp-csharp-sdk/)
- [ILSpy CSharpDecompiler.cs source](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs)
- Direct codebase analysis: `ILSpyDecompilerService.cs`, `TimeoutService.cs`, `Program.cs`, existing test patterns

---
*Pitfalls research for: ILSpy-MCP feature parity milestone*
*Researched: 2026-04-07*
