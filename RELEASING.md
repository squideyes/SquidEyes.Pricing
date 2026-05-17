# Releasing SquidEyes.Pricing

Releases are fully automated by [release-please](https://github.com/googleapis/release-please-action). You write conventional-commit messages, push, and merge a single bot-managed PR when you want to ship. There are no manual tags, no version files to bump, no scripts to run.

## TL;DR — the daily flow

```text
1. Make changes in VS 2026
2. Commit with a conventional-commit Subject  (e.g. "fix: ..." or "feat: ...")
3. Push to master
4. A bot PR titled "chore(master): release X.Y.Z" appears on GitHub
5. When you have enough changes to ship, click Merge on that PR
6. The merge auto-builds, packs, and publishes to nuget.org
```

That's it. No `dotnet pack`, no `git tag`, no `dotnet nuget push`.

## Conventional commits — what to type in the VS Subject box

| Commit subject                              | Bump   | Appears in CHANGELOG as |
|---|---|---|
| `fix: candle skipped trade at UntilET`       | patch  | **Bug Fixes** |
| `feat: add CandleSet aggregator`             | minor  | **Features** |
| `feat!: rename Tick.Volume to Tick.Size`     | major  | **⚠ BREAKING CHANGES** |
| `perf: avoid alloc in TryAdd`                | patch  | **Performance Improvements** |
| `refactor: extract NewsImpactDefaults`       | none   | **Code Refactoring** |
| `docs: clarify embargo defaults`             | none   | (omitted) |
| `chore: bump xunit to 2.10`                  | none   | (omitted) |
| `test: cover negative impact minutes`        | none   | (omitted) |
| `ci: tighten workflow timeout`               | none   | (omitted) |
| `build: bump SDK to .NET 9`                  | none   | (omitted) |

Optional scope in parens after the type — `feat(stba): ...`, `fix(sessions): ...`. Decorative; groups entries in the CHANGELOG under that scope's heading.

### Marking a breaking change

Two equivalent ways:

```text
feat!: rename Tick.Volume to Tick.Size
```

…or as a footer in the commit body (after a blank line):

```text
feat: rename Tick.Volume to Tick.Size

BREAKING CHANGE: Tick.Volume is now Tick.Size.
```

Either bumps **major** and adds a `⚠ BREAKING CHANGES` section to the CHANGELOG.

### Commits that get ignored

If you forget the prefix entirely (`WIP fix something`), or use one not in the list (`update:`, `misc:`), release-please **ignores** the commit. It still lands on master but won't trigger a release or appear in the CHANGELOG. Handy for in-flight WIP commits — just rewrite the message to a real conventional prefix before merging the release PR.

## How the release PR works

After every push to master with at least one releasable commit since the last release, release-please opens (or updates) one long-lived PR. It's titled like:

> chore(master): release 1.1.0

It contains:

- An update to `CHANGELOG.md` with the auto-generated entries for the unreleased commits
- An update to `.release-please-manifest.json` with the new version

You can:

- **Leave it open** while accumulating more commits — release-please rewrites the same PR as new commits land, growing the proposed CHANGELOG entry
- **Merge it** when you want to ship — that's the "ship" button

### What happens when you click Merge

In sequence, on a single workflow run:

1. The merge commit lands on master
2. `release-please.yml` fires again
3. release-please action detects this is a release commit, creates the GitHub Release + tag `v1.1.0`
4. The `publish` job in the same workflow detects `release_created == true` and runs
5. `dotnet build` / `test` / `pack` with MinVer reading the new tag → version `1.1.0`
6. `dotnet nuget push` lands the `.nupkg` + `.snupkg` on nuget.org
7. nuget.org reindexes (~5-15 min for search; immediate for direct URLs)

If anything goes wrong in step 5 or 6 (e.g., MinVer falls back to `0.0.0-alpha.0` somehow), the **abort step** halts the workflow before pushing to NuGet. No more ghost versions.

## Where the CHANGELOG is shown

Three places, automatically kept in sync:

1. **`CHANGELOG.md` at the repo root** — the canonical, version-controlled file. release-please rewrites this on each release PR. Visible at <https://github.com/squideyes/SquidEyes.Pricing/blob/master/CHANGELOG.md>.

2. **GitHub Releases page** — each release-please-created release on <https://github.com/squideyes/SquidEyes.Pricing/releases> has the same per-version notes as its body. This is what people see when they click "Releases" on the repo sidebar, and it's what RSS-style update tools (Dependabot, Renovate) read.

3. **NuGet.org package page** — the package detail page at <https://www.nuget.org/packages/SquidEyes.Pricing> shows a "Release notes" section pulled from the `.nupkg`'s metadata. To make NuGet display the per-version notes there, set `PackageReleaseNotes` in the csproj at pack time. We're currently *not* setting that — the NuGet page just shows the latest CHANGELOG from the bundled README. If you want per-version release notes on nuget.org too, add this to the csproj (release-please can inject the right text for each release, but it takes a small extra step — ask if you want to wire it up).

The README inside the published `.nupkg` is the one rendered on the NuGet package page. Since we bundle the repo's `README.md` directly (`<None Include="..\..\README.md" Pack="true" ...>`), the README on the NuGet page is always the README at the moment of pack — which is whatever's on master when the release PR is merged.

## Manual override (rarely needed)

If for some reason you need to ship without going through release-please (urgent patch, fixing a borked release), you can still tag manually:

```powershell
git tag -a v1.2.3 -m "Release 1.2.3"
git push origin v1.2.3
```

…**but** the tag-triggered workflow has been removed, so this won't auto-publish. It just creates the git tag. To actually publish you'd need to either restore the old `release.yml` (it's in git history) or push to NuGet by hand:

```powershell
dotnet pack src\SquidEyes.Pricing -c Release -o artifacts
dotnet nuget push artifacts\SquidEyes.Pricing.1.2.3.nupkg `
  --api-key <your-key> `
  --source https://api.nuget.org/v3/index.json
```

In practice, you shouldn't need this. release-please's PR can be merged any time, including for a one-line urgent fix.

## What to do right now (first release-please PR)

Make any commit with a conventional prefix, push it, and the first release PR will appear. The csproj icon fix from earlier is the obvious candidate:

```text
Subject:   fix: switch package icon to Squidly.png
```

Push, wait 30-60s, check <https://github.com/squideyes/SquidEyes.Pricing/pulls> — there will be a *"chore(master): release 1.0.2"* PR (patch bump because of `fix:`). Open it, glance at the CHANGELOG diff, click Merge. `1.0.2` lands on NuGet a couple minutes later.

## Troubleshooting

**The release PR never appeared.** Check the latest *Release Please* run at <https://github.com/squideyes/SquidEyes.Pricing/actions/workflows/release-please.yml>. The log will say either "no release necessary" (no qualifying commits) or show an error.

**A commit didn't trigger a bump.** Most likely the prefix wasn't in the recognized list. Look at the merged commit's subject line: it has to start with one of `fix:`, `feat:`, `feat!:`, `perf:`, `refactor:`, `revert:`, or contain a `BREAKING CHANGE:` footer to count toward the version. `docs:`, `chore:`, `test:`, `ci:`, `build:`, `style:` land in the repo but don't bump.

**The publish step failed.** The `Refuse to publish a no-tag fallback` step is set up to abort before any NuGet push if MinVer hits its fallback. If that ever fires, paste the workflow's `Build` step output (specifically the `MinVer:` lines) and we'll diagnose.
