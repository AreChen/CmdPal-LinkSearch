# Changelog

## [0.1.10] - 2026-10-31

- Use Score for OpenAI decision ranking while preserving existing provider settings; retain Noul and Choice for OpenRouter and custom System One.
- Add a provider-specific connection test, a configurable candidate limit, and visible ranking failures without switching providers.
- Show original search results while ranking runs and cancel obsolete requests when the query changes.
- Keep the default selection at the top when ranking changes the result order.
- Include the search query in each OpenAI Score question to improve relevance.
- Omit empty descriptions and tags from ranking requests across all providers.
- Keep the settings page open when saving and testing a ranking service connection.
- Update the English and Chinese ranking guides with Score requests, migration, privacy, and candidate-limit behavior.
- Update x64 and ARM64 MSIX package versions to `0.1.10.0`.

## [0.1.9] - 2026-10-29

- Add OpenAI Decisions and OpenRouter Decisions providers with separate endpoints, credentials, and models, supporting compatible gateways as well as per-item and Choice ranking.
- Hide ranking settings when disabled, switch modes and providers in one action, provide official setup links, mask API keys, and preserve existing custom System One settings.
- Keep the original order when a provider refuses a question or returns an incomplete ranking.
- Move ranking configuration and API examples into separate English and Chinese guides.
- Update x64 and ARM64 MSIX package versions to `0.1.9.0`.

## [0.1.8] - 2026-10-28

- Fix ranking mode selection so Noul and Choice use the configured decision model instead of the Rerank API.
- Migrate previously saved ranking and language selections while preserving existing settings.
- Add regression tests for setting choices, migration, and the selected ranking service.
- Update x64 and ARM64 MSIX package versions to `0.1.8.0`.

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

[0.1.10]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.10
[0.1.9]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.9
[0.1.8]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.8
[0.1.7]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.7
[0.1.6]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.6
[0.1.5]: https://github.com/AreChen/CmdPal-LinkSearch/releases/tag/v0.1.5
