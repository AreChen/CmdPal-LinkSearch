# LinkSearch

English | [简体中文](./README.zh.md)

A PowerToys CmdPal plugin to search and open your links quickly inside the command palette, with optional Rerank integration for better, semantic ordering.

- Unified search: search common websites, knowledge bases, internal links, or Linkwarden bookmarks in one place.
- Optional reranking: connect a configurable Rerank API to semantically reorder candidates.
- Lightweight: ready to use, with basic settings and a connection test.


## Usage

- Open PowerToys’ CmdPal (Command Palette).
- Enter via the top-level command for LinkSearch:
  - Registered by the command provider: [LinkSearchCommandsProvider](LinkSearch/LinkSearchCommandsProvider.cs:20)
- Type keywords on the LinkSearch page and press Enter to open a result.
- If Rerank is enabled, results are reordered by the configured Rerank API.

Page and interaction:
- Entry and interaction logic: [LinkSearchPage](LinkSearch/Pages/LinkSearchPage.cs:1)

## Configuration

Open LinkSearch settings from the CmdPal plugin settings (you can also enter via MoreCommands):
- Settings binding: [LinkSearchCommandsProvider](LinkSearch/LinkSearchCommandsProvider.cs:24)

Settings are defined in: [SettingsManager](LinkSearch/Helpers/SettingsManager.cs:11)

Supported fields (UI settings take precedence, env vars next, some defaults provided):
- LinkwardenBaseUrl
  - Description: Linkwarden server URL. Priority: UI setting > env LINKWARDEN_BASE_URL > default https://cloud.linkwarden.app
- LinkwardenApiKey
  - Description: Linkwarden API token. Priority: UI setting > env LINKWARDEN_API_KEY
- EnableRerank
  - Description: enable reranking. Priority: UI setting (bool) > env LINKSEARCH_ENABLE_RERANK (true/false). Default false.
- RerankApiUrl
  - Description: Rerank API URL. Priority: UI setting > env LINKSEARCH_RERANK_API_URL > default https://api.siliconflow.cn/v1/rerank
- RerankApiKey
  - Description: Rerank API key. Priority: UI setting > env LINKSEARCH_RERANK_API_KEY
- RerankModelName
  - Description: Rerank model name. Priority: UI setting > env LINKSEARCH_RERANK_MODEL_NAME > default BAAI/bge-reranker-v2-m3
- SearchDelayMilliseconds
  - Description: debounce delay after typing, range 300–2000ms. Priority: UI setting > env LINKSEARCH_SEARCH_DELAY_MS. Default 600ms. Out-of-range values are clamped.

Env var examples (PowerShell):
```powershell
$env:LINKWARDEN_BASE_URL="https://cloud.linkwarden.app"
$env:LINKWARDEN_API_KEY="your-linkwarden-token"
$env:LINKSEARCH_ENABLE_RERANK="true"
$env:LINKSEARCH_RERANK_API_URL="https://api.siliconflow.cn/v1/rerank"
$env:LINKSEARCH_RERANK_API_KEY="your-rerank-key"
$env:LINKSEARCH_RERANK_MODEL_NAME="BAAI/bge-reranker-v2-m3"
$env:LINKSEARCH_SEARCH_DELAY_MS="600"
```

Rerank behavior:
- Implementation: [RerankService](LinkSearch/Services/RerankService.cs:18)
- For each link, a document is built using name/description/URL/tags/collection and sent to the Rerank API; results are reordered using returned indices.
- Connection test: [RerankConnectionTestService](LinkSearch/Services/RerankConnectionTestService.cs:14) helps validate URL, key, and model settings.

## Development and Build

- Solution and project:
  - [LinkSearch.sln](LinkSearch.sln:1)
  - [LinkSearch.csproj](LinkSearch/LinkSearch.csproj:1)
- App and package manifests:
  - [app.manifest](LinkSearch/app.manifest:1)
  - [Package.appxmanifest](LinkSearch/Package.appxmanifest:1)
- Publish profiles:
  - [Properties/PublishProfiles](LinkSearch/Properties/PublishProfiles/)

Release builds produce an installable MSIX under bin/x64/Release/.../AppPackages.

## Roadmap

- [ ] Microsoft Store and WinGet publishing
- [ ] Richer data sources (local index / more services)
- [ ] Optional offline reranking (local model/embeddings)
- [ ] Enhanced page interactions and filters

Contributions via Issues/PRs are welcome.

## Credits

- Built on the Microsoft Command Palette Extensions Toolkit (PowerToys CmdPal).
- Semantic reranking aligns with modern retrieval practices; thanks to community resources and examples.

## Thanks

- Thanks to contributors for testing and documentation input.

## License

MIT
