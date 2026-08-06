---
quick_id: 260806-teg
slug: prepare-and-publish-v1-2-1-patch-release
date: 2026-08-06
status: planned
---

# Quick Task 260806-teg: Prepare and publish v1.2.1 patch release

## Goal

Cut a v1.2.1 patch release so the two source-only fixes landed since v1.2.0 reach
the pre-packaged binaries. Bump the assembly version, document the release in the
changelog, commit, then tag + push to trigger the automated `release.yml` GitHub
Actions build.

## Context

Since v1.2.0 (2026-04-12), two changes affect the shipped binary but exist only in
source, not in any downloadable release:

- **PR #2** (`20c3828`) — `find_extension_methods` / `search_members_by_name` no
  longer filter to public types only, so internal/private types are now searchable.
- **PR #3** (`c0671c9`) — sets `DOTNET_hostBuilder__reloadConfigOnChange=false` to
  stop the .NET host watching the working dir, eliminating CPU spikes when cwd is a
  project dir.

Both are bug/perf fixes with no new features and no breaking changes → semver patch.

The csproj `<Version>` is a stale `1.1.0` (never bumped for v1.2.0); this release
corrects it to `1.2.1`.

## Tasks

### Task 1 — Bump assembly version
- **files:** `ILSpy.Mcp.csproj`
- **action:** Change `<Version>1.1.0</Version>` → `<Version>1.2.1</Version>`
- **verify:** `grep '<Version>1.2.1</Version>' ILSpy.Mcp.csproj`
- **done:** csproj reports version 1.2.1

### Task 2 — Add CHANGELOG [1.2.1] section
- **files:** `CHANGELOG.md`
- **action:** Insert a `## [1.2.1] - 2026-08-06` section above `## [1.2.0]` with a
  `### Fixed` list for the two fixes; add the `[1.2.1]` compare link
  (`v1.2.0...v1.2.1`) at the bottom.
- **verify:** `grep '\[1.2.1\]' CHANGELOG.md`
- **done:** changelog documents 1.2.1 with compare link

### Task 3 — Commit release prep (atomic)
- **action:** Commit csproj + CHANGELOG together as the release-prep commit.
- **done:** single commit on main with version bump + changelog.

### Task 4 — Tag and push release
- **action:** Push the release-prep commit, then create annotated tag `v1.2.1` and
  push it to trigger `.github/workflows/release.yml`.
- **verify:** `gh run list` shows the Release workflow running for tag v1.2.1.
- **done:** tag v1.2.1 pushed; CI release build triggered.

## Out of scope
- No code/behavior changes (fixes already merged).
- No changes to the release workflow itself.
