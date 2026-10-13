# Publishing a release

CI is manual only. Pushes, tags and pull requests do not start it. From an
authenticated checkout, run:

```powershell
gh workflow run ci.yml --ref main
gh run list --workflow ci.yml --limit 5
gh run watch <run-id> --exit-status
gh run download <run-id> --name MouseMotionInspector-win-x64 --dir artifacts/ci-release
```

Use the exact successful run for the intended commit. CI packages both Windows
flavors and source, with checksum files. It does not publish them automatically.
Local framework-dependent packages can be built with `scripts/package.ps1` and
`scripts/package-source.ps1`; self-contained packaging needs .NET runtime packs.

Before publishing, check the version in `native/Directory.Build.props`, review
the staged files, follow the [release checklist](release-checklist.md), and use
release notes that describe the actual attached package flavors and validation.
Do not attach captures, inventories or unreviewed test data.

Create an annotated tag on the tested commit, push it, then use `gh release create`
with `--verify-tag`, `--notes-file docs/releases/<tag>.md`, and explicit archive
and checksum paths. Avoid asset wildcards that might include stale packages.

Repository settings: public visibility, MIT license, issues enabled, relevant
mouse/Windows/C# topics, and private vulnerability reporting enabled when
available. The repository URL used by the app is in `AboutPage.cs`.
