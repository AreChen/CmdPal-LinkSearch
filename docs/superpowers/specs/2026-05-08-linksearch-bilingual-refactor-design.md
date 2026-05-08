# LinkSearch Bilingual Refactor Design

Date: 2026-05-08

## Context

LinkSearch is a PowerToys Command Palette extension for searching Linkwarden links and optionally reranking results. Current baseline builds successfully with `dotnet build "LinkSearch.sln" -p:Platform=x64`, with 11 CA1852 analyzer warnings about internal types that can be sealed.

Current code already has bilingual README files, but in-app settings labels, descriptions, search placeholders, empty states, and error messages are mostly hard-coded in Chinese. `LinkSearchPage.cs` also mixes CmdPal page lifecycle, debounce/cancellation, Linkwarden HTTP calls, JSON parsing, result rendering, and Rerank fallback behavior in one large file. The official Command Palette extension model supports extension-provided settings pages through the toolkit settings object exposed by the command provider.

## Goals

1. Add Chinese and English support for extension settings and user-facing prompts.
2. Support language mode `Auto`, `zh-CN`, and `en-US`.
3. Keep the existing CmdPal registration, top-level command, and settings page entry behavior intact.
4. Refactor search, parsing, rerank, rendering, and localization into testable units.
5. Add a short-lived in-memory search cache to reduce repeated Linkwarden calls for identical queries.
6. Add automated tests for non-host-dependent behavior.

## Non-Goals

1. Do not add persistent cache in this phase, because saved link URLs may be private and would require cache clearing, migration, and privacy UX.
2. Do not redesign the visual UI beyond localized settings and prompt text.
3. Do not change MSIX registration, COM CLSID, manifest app extension registration, or publish identity.
4. Do not add Microsoft Store publishing work.

## Recommended Approach

Use a staged internal refactor. Keep the external extension surface stable while extracting clear internal boundaries. Each stage should build and have tests where possible.

This is preferred over a full rewrite because it addresses the real issues without changing extension registration or user entry points. It is preferred over a minimal localization-only pass because the current page/service coupling would make localization and later changes fragile.

## Architecture

`LinkSearchCommandsProvider` remains the top-level command provider and still exposes the toolkit settings page through `MoreCommands`. It creates a `LinkSearchPage` with shared service dependencies.

`LinkSearchPage` becomes responsible for CmdPal page lifecycle only: title/name/placeholder setup, search debounce, cancellation, reacting to settings changes, and raising item changes. It should not parse Linkwarden JSON or construct HTTP requests directly.

`SettingsManager` continues to own persisted settings and environment variable fallbacks. It gains a language setting and exposes parsed values for URL, API key, result limits, search delay, rerank settings, and resolved UI language.

`Localization` owns all user-facing text and resolves strings based on `Auto`, `zh-CN`, or `en-US`. `Auto` uses `CultureInfo.CurrentUICulture`; Chinese cultures resolve to Simplified Chinese, everything else resolves to English.

`LinkwardenService` owns Linkwarden HTTP requests and response parsing. It returns internal link models and does not depend on CmdPal UI types.

`RerankService` keeps rerank API responsibilities, but accepts internal link models and returns reordered link models. If rerank is disabled, incomplete, canceled, or fails, it returns the original order and logs the reason.

`SearchResultPresenter` converts internal search state into CmdPal `IListItem` values using localized text.

`SearchCache` provides short-lived in-memory caching for Linkwarden results keyed by normalized query plus effective Linkwarden endpoint identity. It does not cache reranked ordering, so rerank setting changes still take effect immediately.

## Components

### Localization

Add a small strongly-typed localization layer rather than scattering dictionaries through the code. It should include:

1. `LanguageMode` enum: `Auto`, `Chinese`, `English`.
2. `UiLanguage` enum or equivalent resolved language: `Chinese`, `English`.
3. `Localizer` or `LocalizedStrings` class with keys for settings titles/descriptions, placeholders, empty states, errors, command names, tag labels, and rerank connection test messages.
4. Deterministic fallback to English if a key is missing.

### Settings

Add a language setting at the top of the settings page. Labels and descriptions should be generated from the resolved language. Existing setting keys should stay stable to preserve user configuration. New language setting key should be namespaced under `linkSearch`.

Settings should keep current defaults unless the value is clearly invalid:

1. Linkwarden Base URL default remains `https://cloud.linkwarden.app`.
2. Rerank API URL default remains `https://api.siliconflow.cn/v1/rerank`.
3. Rerank model default remains `BAAI/bge-reranker-v2-m3`.
4. Search delay clamps to 300-2000 ms.
5. Max results clamps to 1-200.

### Linkwarden Service

Move Linkwarden fetch and JSON parsing out of `LinkSearchPage`. The parser should support both known response shapes:

1. `{ "data": { "links": [...] } }`
2. `{ "data": [...] }`

It should handle missing optional fields like description, tags, and collection. Missing required URL should be represented as an invalid item that presenter can skip or report safely.

### Rerank

Rerank document text should be generated consistently and independently from UI text. Use stable field labels in English for API payloads to reduce multilingual prompt drift, while UI messages remain localized.

Rerank failures should not make search fail. They should fall back to original Linkwarden order and log diagnostic information.

### Search Cache

Use an in-memory cache with a default 5 minute TTL. Cache raw Linkwarden search results only. Clear cache when settings change in ways that affect fetched data or authorization: Linkwarden base URL, API key, and explicit service disposal. Language and max result changes should not require cache invalidation because localized text and result limits are applied during presentation.

The cache should not be persisted to disk in this phase.

### Presentation

Use localized strings for:

1. Search placeholder.
2. Empty search prompt.
3. Empty result prompt.
4. Missing/invalid token prompt.
5. Invalid base URL prompt.
6. API failure, network failure, timeout, response format errors.
7. Open link command name.
8. Tag label.
9. Rerank connection test success and failure text.

Result titles should remain the link name plus collection. Result subtitles should include description and tags when present.

## Data Flow

1. Provider constructs settings, localizer, Linkwarden service, rerank service, cache, presenter, and page.
2. Settings load from the toolkit JSON settings file and existing environment variable fallbacks.
3. Page resolves localized title/placeholder/empty content from current settings.
4. User types a query.
5. Page debounces and cancels stale searches.
6. Page asks `LinkwardenService` for raw results. The service checks `SearchCache` first, then calls Linkwarden if there is no valid cache hit.
7. `LinkSearchPage` calls `RerankService` with raw results when rerank is enabled and configured.
8. Presenter creates localized `IListItem` values.
9. Page posts `RaiseItemsChanged` through the captured synchronization context.

## Error Handling

Use explicit error categories rather than ad hoc strings:

1. Configuration error: missing token, invalid URL, invalid numeric setting.
2. Authentication/authorization error: 401 and 403.
3. Network error: DNS, SSL, connection failure.
4. Timeout/cancellation: distinguish expected cancellation from user-visible timeout.
5. Response format error: missing `data`, unsupported `data`, non-array `links`.
6. Rerank degraded mode: rerank failed but Linkwarden results are still shown.

All user-visible messages go through localization. Logs may remain Chinese or English during the refactor, but new logs should prefer English for consistency with diagnostics.

## Testing

Add `LinkSearch.Tests` for host-independent behavior. Use normal .NET tests and avoid launching CmdPal or Windows Launcher.

Test coverage should include:

1. Language auto detection and manual override.
2. Localized settings text for Chinese and English.
3. URL normalization and invalid URL handling.
4. Search delay and max result clamping.
5. Linkwarden parser support for `data.links` and `data[]`.
6. Missing optional fields in Linkwarden responses.
7. Rerank empty/failing responses preserve original order.
8. Search cache hit, expiration, and invalidation.

Manual verification should cover:

1. Build x64 Debug.
2. Open CmdPal extension settings and switch language.
3. Confirm settings labels/descriptions update after reopening the settings page or restarting CmdPal if the toolkit does not refresh setting metadata live.
4. Search with valid Linkwarden credentials.
5. Search with rerank disabled and enabled.
6. Validate missing token, invalid URL, and network failure prompts in both languages.

## Existing Improvement Findings

1. `LinkSearchPage.cs` is too large and mixes UI, HTTP, parsing, rerank, and rendering responsibilities.
2. User-facing strings are hard-coded in Chinese despite bilingual README files.
3. The README mentions creating `LinkSearch/config.json`, but current settings code only reads toolkit settings and environment variables.
4. Build passes but reports CA1852 warnings for internal classes that can be sealed.
5. `RerankService` and `RerankConnectionTestService` are passed into `LinkSearchPage` and also disposed by both page and provider, which should be reviewed to avoid double-dispose patterns.
6. Rerank currently builds document labels in Chinese, which is UI-language leakage into API payload construction.
7. Search lacks automated tests around Linkwarden response variants and rerank fallback behavior.

## Acceptance Criteria

1. Users can choose Auto, Simplified Chinese, or English in extension settings.
2. Auto chooses Chinese for Chinese UI cultures and English otherwise.
3. Settings labels, setting descriptions, page placeholder, empty states, command names, and errors appear in the selected language.
4. Existing saved setting keys continue to work.
5. Search still works without rerank.
6. Rerank still reorders results when configured and gracefully falls back when it fails.
7. Repeated identical queries within the cache TTL avoid an extra Linkwarden HTTP request.
8. `dotnet build "LinkSearch.sln" -p:Platform=x64` succeeds.
9. New tests pass.

## Implementation Notes

Implementation should proceed in small stages:

1. Add tests and localization primitives.
2. Refactor settings parsing and localized labels.
3. Extract Linkwarden models/service/parser.
4. Extract result presentation.
5. Add memory cache.
6. Adjust Rerank service boundaries.
7. Trim analyzer warnings and review disposal ownership.

No git commit should be created unless explicitly requested.
