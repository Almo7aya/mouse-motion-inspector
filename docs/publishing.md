# Publishing a release

CI is manual only. Pushes, tags and pull requests do not start it. From an
authenticated checkout, run:

```powershell
gh workflow run ci.yml --ref main
gh run list --workflow ci.yml --limit 5
gh run watch <run-id> --exit-status
```

Use the exact successful run for the intended commit. CI packages both Windows
flavors and source, with checksum files. It does not publish them automatically.
Local framework-dependent packages can be built with `scripts/package.ps1` and
`scripts/package-source.ps1`; self-contained packaging needs .NET runtime packs.

Before publishing, check the version in `native/Directory.Build.props`, review
the staged files, follow the [release checklist](release-checklist.md), and use
release notes that describe the actual attached package flavors and validation.
Do not attach captures, inventories or unreviewed test data.

Create an annotated tag on the tested commit and push it. Publish its existing CI
packages entirely on GitHub using the separate **Manual release** workflow:

```powershell
git tag -a v1.0.0 <tested-commit> -m 'Mouse Motion Inspector 1.0.0'
git push origin v1.0.0
gh workflow run release.yml --ref main -f tag=v1.0.0 -f build_run_id=<run-id>
```

The release workflow checks that the selected successful manual build belongs to
this repository and matches the tagged commit. It transfers the six package and
checksum files on the GitHub runner, creates a draft, and publishes it only after
the uploads succeed. Release notes come from `docs/releases/<tag>.md` at the tag.
Neither workflow runs on pushes, tags or pull requests.

For local publication instead, download that run with `gh run download <run-id>
--name MouseMotionInspector-win-x64 --dir artifacts/ci-release`, then use
`gh release create` with `--verify-tag`, `--notes-file` and explicit archive and
checksum paths. Avoid asset wildcards that might include stale packages.

Repository settings: public visibility, MIT license, issues enabled, relevant
mouse/Windows/C# topics, and private vulnerability reporting enabled when
available. The repository URL used by the app is in `AboutPage.cs`.
