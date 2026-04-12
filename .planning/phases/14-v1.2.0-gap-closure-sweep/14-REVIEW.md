---
phase: 14-v1.2.0-gap-closure-sweep
reviewed: 2026-04-12T00:00:00Z
depth: standard
files_reviewed: 34
files_reviewed_list:
  - Application/UseCases/AnalyzeAssemblyUseCase.cs
  - Application/UseCases/DecompileMethodUseCase.cs
  - Application/UseCases/DecompileTypeUseCase.cs
  - Application/UseCases/DisassembleMethodUseCase.cs
  - Application/UseCases/DisassembleTypeUseCase.cs
  - Application/UseCases/ExportProjectUseCase.cs
  - Application/UseCases/GetTypeMembersUseCase.cs
  - Application/UseCases/ListAssemblyTypesUseCase.cs
  - Application/UseCases/ListEmbeddedResourcesUseCase.cs
  - Application/UseCases/ListNamespaceTypesUseCase.cs
  - Application/UseCases/SearchConstantsUseCase.cs
  - Application/UseCases/SearchMembersByNameUseCase.cs
  - Application/UseCases/SearchStringsUseCase.cs
  - Domain/Services/IDisassemblyService.cs
  - Infrastructure/Decompiler/ILSpyDisassemblyService.cs
  - Program.cs
  - Tests/Fixtures/ToolTestFixture.cs
  - Tests/Security/SecurityAndRobustnessTests.cs
  - Tests/Tools/DisassembleMethodToolTests.cs
  - Tests/Tools/DisassembleTypeToolTests.cs
  - Tests/Tools/GetTypeMembersToolTests.cs
  - Tests/Tools/ListAssemblyTypesToolTests.cs
  - Tests/Tools/ListEmbeddedResourcesToolTests.cs
  - Tests/Tools/ListNamespaceTypesToolTests.cs
  - Tests/Tools/NativeDllGuardTests.cs
  - Tests/Tools/SearchMembersByNameToolTests.cs
  - Transport/Mcp/Tools/DisassembleMethodTool.cs
  - Transport/Mcp/Tools/DisassembleTypeTool.cs
  - Transport/Mcp/Tools/GetTypeMembersTool.cs
  - Transport/Mcp/Tools/ListAssemblyTypesTool.cs
  - Transport/Mcp/Tools/ListEmbeddedResourcesTool.cs
  - Transport/Mcp/Tools/ListNamespaceTypesTool.cs
  - Transport/Mcp/Tools/SearchMembersByNameTool.cs
  - docs/PAGINATION.md
findings:
  critical: 0
  warning: 3
  info: 6
  total: 9
status: issues_found
---

# Phase 14: Code Review Report

**Reviewed:** 2026-04-12
**Depth:** standard
**Files Reviewed:** 34
**Status:** issues_found

## Summary

Reviewed 34 files in the Phase 14 v1.2.0 gap-closure sweep, covering five use case layers (decompile/disassemble/list/search/analyze/export), the IL disassembly infrastructure adapter, the `Program.cs` composition root, all associated MCP tool handlers, and the test suite. No critical security issues or crash-risk bugs were found. The codebase follows a consistent layered pattern with well-centralized pagination, timeout, and error-sanitization handling.

Three correctness-level concerns surfaced:

1. Multi-overload method disassembly reports `METHOD_NOT_FOUND` instead of a distinct "ambiguous overload" signal, and constructs unused diagnostic text that callers would benefit from.
2. Truncation in every "bounded output" use case (decompile/disassemble/analyze) slices raw strings at `MaxDecompilationSize` code-unit positions without guarding against UTF-16 surrogate pair splits, which can yield malformed output for non-BMP content (e.g., emoji in string literals surfaced via IL disassembly).
3. `ExportProjectUseCase` creates the output directory before validating emptiness, which materially changes filesystem state as a side effect of a validation failure path.

The remaining findings are informational: culture-sensitive `ToLower()` usage that should migrate to `ToLowerInvariant()`, redundant re-throw-only `catch` clauses, and minor style points.

## Warnings

### WR-01: Multi-overload method disassembly silently discards overload info and misreports as not-found

**File:** `Infrastructure/Decompiler/ILSpyDisassemblyService.cs:145-155`
**Issue:** When a type contains multiple methods with the requested name, the code constructs a detailed `overloads` string listing signatures — then throws `MethodNotFoundException` with NO reference to that string. The `overloads` local is dead code, and callers receive `METHOD_NOT_FOUND` which is misleading: the method exists, just ambiguously. Agents cannot programmatically distinguish "missing" from "ambiguous" and cannot see the available overloads to disambiguate.
**Fix:**
```csharp
if (methods.Count > 1)
{
    var overloads = string.Join(", ",
        methods.Select(m =>
        {
            var parameters = string.Join(", ",
                m.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
            return $"{methodName}({parameters})";
        }));
    // Use a distinct exception (e.g., AmbiguousMethodException) or at minimum
    // include `overloads` in the exception message so the agent can disambiguate.
    throw new MethodNotFoundException(methodName, typeName.FullName,
        $"Multiple overloads found for '{methodName}': {overloads}. " +
        $"The current tool does not accept a parameter-signature selector.");
}
```
Consider introducing `AmbiguousMethodException` + an `AMBIGUOUS_METHOD` error code so the transport layer can expose a structured list of signatures.

### WR-02: String truncation by code-unit count can split UTF-16 surrogate pairs

**File:** `Application/UseCases/DecompileMethodUseCase.cs:56`, `Application/UseCases/DecompileTypeUseCase.cs:58`, `Application/UseCases/DisassembleMethodUseCase.cs:62`, `Application/UseCases/DisassembleTypeUseCase.cs:58`, `Application/UseCases/AnalyzeAssemblyUseCase.cs:81`
**Issue:** All five bounded-output use cases slice the raw output via `raw[..maxBytes]`. `string.Length` returns UTF-16 code-unit count, not bytes, so (a) the `total`/`returned` values published in the pagination footer mislabel char counts as bytes (see `docs/PAGINATION.md` line 65: "Total bytes... of the full output"), and (b) when the cut index lands between a high and low surrogate of an astral-plane character (e.g., an emoji literal appearing via `ldstr` in a disassembled method), the returned string contains an unpaired surrogate that is invalid UTF-16 and will round-trip through JSON as `U+FFFD` or raise encoder errors on strict consumers.
**Fix:** Introduce a small helper and use it everywhere:
```csharp
private static string SafeTruncateChars(string s, int maxChars)
{
    if (s.Length <= maxChars) return s;
    var cut = maxChars;
    // Do not cut between a high and low surrogate.
    if (cut > 0 && char.IsHighSurrogate(s[cut - 1])) cut--;
    return s[..cut];
}
```
Also align naming: either rename `MaxDecompilationSize` / footer semantics to "characters", or convert to real byte counting via `Encoding.UTF8.GetByteCount` before truncating. The current "bytes" language in `docs/PAGINATION.md` and the code's char-based accounting disagree.

### WR-03: `ExportProjectUseCase` creates output directory as a side effect of validation

**File:** `Application/UseCases/ExportProjectUseCase.cs:56-61`
**Issue:** The code calls `Directory.CreateDirectory(outputDirectory)` and THEN checks whether the directory is empty, throwing `OutputDirectoryNotEmptyException` if not. If the user passes a non-existent path and the (empty) validation succeeds, the directory is left behind — but if the user passes an EXISTING non-empty directory, validation fails and the directory is untouched (fine). The real risk is pre-creating a directory before all validation (including the concurrency-limited work) runs: if the overall operation fails later, the caller is left with an empty stray directory they didn't have before. For a reverse-engineering tool operating against user-specified paths (which may be mistyped), that's a small but surprising side effect. The comment acknowledges this is "acceptable for a single-user MCP tool" but the validation ordering still materially changes filesystem state before the non-empty check.
**Fix:** Split the "already exists and empty?" check from the create step:
```csharp
if (Directory.Exists(outputDirectory))
{
    if (Directory.EnumerateFileSystemEntries(outputDirectory).Any())
        throw new OutputDirectoryNotEmptyException(outputDirectory);
}
else
{
    Directory.CreateDirectory(outputDirectory);
}
```
This preserves the TOCTOU-tolerant spirit (both branches still race with external actors) while eliminating the "create then validate" surprise for the common typo case.

## Info

### IN-01: `ToLower()` uses current culture; prefer `ToLowerInvariant()`

**File:** `Application/UseCases/GetTypeMembersUseCase.cs:51,58,69,76,82`, `Application/UseCases/ListAssemblyTypesUseCase.cs:68`
**Issue:** `ctor.Accessibility.ToString().ToLower()` (and the other four members) use the thread's current culture. On Turkish locales `"Public".ToLower()` yields `"publıc"` (dotless i in some situations involving i/I pairs). The output is user-visible and should be culture-invariant.
**Fix:** Replace all `.ToLower()` calls that lowercase enum/identifier-like text with `.ToLowerInvariant()`. `Program.cs:29,35,47` already models the correct pattern.

### IN-02: Redundant `catch-rethrow` handlers in `SearchStringsUseCase`

**File:** `Application/UseCases/SearchStringsUseCase.cs:70-77`
**Issue:** `catch (ArgumentException) { throw; }` and `catch (RegexMatchTimeoutException) { throw; }` do nothing the default CLR behavior wouldn't. They exist only to short-circuit the `catch (Exception ex)` that follows, which is a valid reason — but a comment should make this intent explicit, otherwise future edits may delete them thinking they're dead code.
**Fix:**
```csharp
// Let these propagate to the tool layer WITHOUT being logged as "Unexpected error".
catch (ArgumentException) { throw; }
catch (RegexMatchTimeoutException) { throw; }
```

### IN-03: Dead `overloads` local variable

**File:** `Infrastructure/Decompiler/ILSpyDisassemblyService.cs:147-153`
**Issue:** The `overloads` string is constructed inside the `methods.Count > 1` branch and never referenced. This is the same site flagged in WR-01 — if WR-01 is implemented (include overload list in the exception message), this finding resolves with it. Standalone note so it isn't missed if WR-01 is deferred.
**Fix:** Either thread `overloads` into the thrown exception (see WR-01) or delete the local.

### IN-04: `NamespaceFilteredProjectDecompiler` catch is too broad

**File:** `Application/UseCases/ExportProjectUseCase.cs:87-92`
**Issue:** `catch (Exception)` when constructing `NamespaceFilteredProjectDecompiler` is a bare swallow that falls back to unfiltered decompilation with a generic warning. Since the subclass constructor just calls `base(resolver)` and stores a string, the only realistic failure is a NullReferenceException from `resolver`, which would equally fail on the fallback path. The broad catch hides real misconfigurations (e.g., future dependency-injected resolvers with side effects) behind a cheerful "not supported" message.
**Fix:** Narrow the catch to the specific expected exception, or simply remove the try/catch and let the constructor throw — the enclosing `catch (Exception ex)` on line 109 already converts unexpected failures into partial-export warnings.

### IN-05: `Directory.GetFiles(outputDirectory, "*.*", ...)` in `ExportProjectUseCase` includes files without a dot

**File:** `Application/UseCases/ExportProjectUseCase.cs:125`
**Issue:** `"*.*"` on Windows/.NET is equivalent to `"*"` (matches all files, including dot-less names). The intent appears to be "all files" — the current behavior is correct, but `"*.*"` is a common Windows-ism that confuses readers who expect it to require a literal dot. Prefer `"*"` for clarity.
**Fix:** `Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories);`

### IN-06: Recursive `BuildEntry` has no depth guard for pathological nesting

**File:** `Application/UseCases/ListNamespaceTypesUseCase.cs:149-178`
**Issue:** `BuildEntry` recurses through `nestedByParent` without a depth limit. Real .NET assemblies rarely nest deeper than 3-4 levels, but a malicious or generated assembly could theoretically induce stack-overflow. Since inputs come from user-chosen binaries, this is an Info-level DoS surface rather than a bug.
**Fix:** Either enforce a depth cap (e.g., 32 levels, throw `InvalidOperationException`) or convert to iterative traversal using an explicit `Stack<(TypeInfo type, int depth)>`.

---

_Reviewed: 2026-04-12_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
