# Cirra Release Process

1. Set `Version` and `AssemblyVersion` in `src/CloudBerry.App/CloudBerry.App.csproj`.
2. Run `./tools/release/prepare-github-release.ps1` from the repository root.
3. Review `update-feed/latest.json`. Its version, installer URL, SHA-256, and byte count must match the generated installer.
4. Commit and push the code and update manifest before creating the matching `vX.Y.Z` tag.
5. Create the GitHub release from that tag and attach the installer, installer checksum, protected portable ZIP, and ZIP checksum.
6. Confirm the release installer URL returns the exact SHA-256 in `update-feed/latest.json`.
7. For older beta installs still using the legacy Cloudflare endpoint, run `./tools/update-feed/publish_update.ps1 -PackageName Cirra-Setup-vX.Y.Z` after the GitHub release is live.

Cirra only stages executable installers whose SHA-256 and optional byte count match the manifest. An interrupted installation pauses automatic retries for that version until the user explicitly retries; a newer release clears the pause.
