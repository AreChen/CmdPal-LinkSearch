# Changelog

## [0.1.7] - 2026-10-28

- Add selectable decision-model ranking using System One Noul and Choice alongside the existing rerank API.
- Keep decision service configuration separate and preserve the original order when decision requests fail or return invalid results.
- Limit ranking inputs to the search query and candidate titles, descriptions, and tags.
- Update development and test dependencies, and use the dedicated MSIX build tooling package.
- Update x64 and ARM64 MSIX package versions to `0.1.7.0`.

## [0.1.6] - 2026-09-12

- Upgrade Microsoft.CommandPalette.Extensions to `0.12.260812002`.
- Bump x64 and ARM64 MSIX package versions to `0.1.6.0`.
- Publish versioned release notes from `docs/releases/` through the release workflow.

## [0.1.5] - 2026-06-24

- Upgrade Microsoft.CommandPalette.Extensions to `0.11.260520004`.
- Migrate the application, tests, packaging, and release workflow to .NET 10.
- Remove the explicit System.Text.Json dependency and stale Toolkit package version.
- Bump the MSIX package version to `0.1.5.0` and update README badges.

[0.1.7]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.7
[0.1.6]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.6
[0.1.5]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.5
