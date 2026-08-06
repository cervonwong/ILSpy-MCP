---
quick_id: 260806-teg
slug: prepare-and-publish-v1-2-1-patch-release
date: 2026-08-06
status: complete
commit: 531a5c9
---

# Quick Task 260806-teg: Prepare and publish v1.2.1 patch release — Summary

## Outcome

Cut the **v1.2.1** patch release. The two source-only fixes merged since v1.2.0
(PR #2 and PR #3) now reach the pre-packaged binaries via the automated
`release.yml` GitHub Actions build.

## What changed

| Task | File | Change |
|------|------|--------|
| 1 | `ILSpy.Mcp.csproj` | `<Version>` bumped `1.1.0` → `1.2.1` (was stale — never bumped for v1.2.0) |
| 2 | `CHANGELOG.md` | Added `## [1.2.1] - 2026-08-06` section (two `Fixed` entries) + `[1.2.1]` compare link (`v1.2.0...v1.2.1`) |

## Release fixes documented

- **PR #2** (`20c3828`) — `find_extension_methods` / `search_members_by_name` no
  longer filter to public types only; internal/private types are now searchable.
- **PR #3** (`c0671c9`) — `DOTNET_hostBuilder__reloadConfigOnChange=false` disables
  the host's working-dir config watch, eliminating CPU spikes when cwd is a project dir.

## Commits

- `531a5c9` — Bump version to 1.2.1 and add changelog entry (release-prep, atomic)
- Annotated tag **`v1.2.1`** created at `531a5c9`.

## Verification

- `dotnet build ILSpy.Mcp.csproj -c Release` → **Build succeeded, 0 warnings, 0 errors**.
- Tag `v1.2.1` confirmed pointing at `531a5c9`.
- Release trigger: pushing tag `v1.2.1` starts `.github/workflows/release.yml`, which
  publishes self-contained single-file binaries for win-x64, linux-x64, linux-arm64,
  osx-x64, osx-arm64 (zip/tar.gz + SHA256) and creates the GitHub Release.

## Notes

- Tag intentionally points at the clean release-prep commit (`531a5c9`), not the
  later `.planning/` docs commit, so auto-generated release notes stay noise-free.
- Executed inline on `main` (not via worktree isolation) because the tag must land on
  `main` in order and the tag push is an outward-facing release trigger.
