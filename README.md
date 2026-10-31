<div align="center">
  <img src="./LinkSearch/Assets/StoreLogo.png" width="96" alt="LinkSearch logo" />
  <h1>LinkSearch</h1>
  <p><strong>Search Linkwarden from Microsoft PowerToys Command Palette.</strong></p>
  <p>Find saved links, open pages quickly, and optionally rank results with a rerank API or decision model.</p>
  <p>English | <a href="./README.zh.md">简体中文</a></p>
  <p>
    <a href="https://apps.microsoft.com/detail/9MZ9Q4CFP2N9"><img alt="Get it from Microsoft Store" src="https://img.shields.io/badge/Get_it_on-Microsoft_Store-0078D4?style=for-the-badge&logo=microsoftstore&logoColor=white" /></a>
    <a href="https://github.com/AreChen/CmdPal-LinkSearch/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/AreChen/CmdPal-LinkSearch?style=for-the-badge&logo=github&color=24292f" /></a>
    <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
    <img alt="License MIT" src="https://img.shields.io/badge/License-MIT-0f766e?style=for-the-badge" />
  </p>
  <p>
    <a href="#install">Install</a> •
    <a href="#features">Features</a> •
    <a href="#usage">Usage</a> •
    <a href="./docs/ranking.md#rerank-api">Rerank API</a> •
    <a href="./docs/ranking.md#decision-model-api">Decision Model API</a> •
    <a href="./PRIVACY.md">Privacy</a>
  </p>
</div>

---

## Preview

<div align="center">
  <img src="https://i.imgur.com/fcsbu0o.gif" alt="LinkSearch search preview in PowerToys Command Palette" width="820" />
  <br />
  <br />
  <img src="https://i.imgur.com/eEM6ZtC.png" alt="LinkSearch settings page" width="820" />
</div>

## Features

<table>
  <tr>
    <td width="25%" valign="top">
      <img src="./docs/assets/icons/search.svg" width="32" alt="Search icon" />
      <h3>Fast Search</h3>
      <p>Search Linkwarden collections directly from PowerToys Command Palette.</p>
    </td>
    <td width="25%" valign="top">
      <img src="./docs/assets/icons/external-link.svg" width="32" alt="Open link icon" />
      <h3>Quick Open</h3>
      <p>Open saved pages from search results without switching context.</p>
    </td>
    <td width="25%" valign="top">
      <img src="./docs/assets/icons/sparkles.svg" width="32" alt="Rerank icon" />
      <h3>Optional Rerank</h3>
      <p>Use a rerank API or decision model for semantic ordering.</p>
    </td>
    <td width="25%" valign="top">
      <img src="./docs/assets/icons/languages.svg" width="32" alt="Language icon" />
      <h3>Bilingual UI</h3>
      <p>Use Auto, Simplified Chinese, or English from extension settings.</p>
    </td>
  </tr>
</table>

## Install

> LinkSearch requires Microsoft PowerToys with Command Palette support.

<a href="https://apps.microsoft.com/detail/9MZ9Q4CFP2N9"><img alt="Get it from Microsoft Store" src="https://img.shields.io/badge/Get_it_on-Microsoft_Store-0078D4?style=for-the-badge&logo=microsoftstore&logoColor=white" /></a>

- Recommended: install LinkSearch from the [Microsoft Store](https://apps.microsoft.com/detail/9MZ9Q4CFP2N9).
- Fallback: download the latest [GitHub Release](https://github.com/AreChen/CmdPal-LinkSearch/releases/latest) MSIX sideload package if Microsoft Store is unavailable.

## Usage

1. Open PowerToys CmdPal (Command Palette).
2. Go to the extension settings page and enable the LinkSearch extension.
3. Configure your preferred shortcut key for the extension.
4. Configure Linkwarden server address, access token, and maximum search result count.
5. Choose the interface language: Auto, Simplified Chinese, or English.
6. Optional: enable reranking, choose a service, fill in its configuration, and use **Save and test connection**.
7. Enter the shortcut key and start searching.

## Ranking Modes

Enable ranking in the settings and choose a service:

- **Rerank API**: use a compatible rerank endpoint with your existing rerank settings.
- **OpenRouter** or **custom System One**: choose per-item Noul or Choice.
- **OpenAI**: rank candidates with Score.

Each service has separate settings and credentials. Existing OpenAI configurations retain their endpoint, key, and model while switching to Score.

Ranking sends the query and candidate titles, descriptions, and tags to the selected service; these fields may contain sensitive information. Results appear immediately and update after ranking. Failures retain the original order and show an error. The candidate limit defaults to 50 and can be adjusted independently of the display limit.

For configuration, request formats, and examples, see the [Ranking API Guide](./docs/ranking.md).

## Roadmap

- [x] Microsoft Store publishing.

## Credits

- Built on the PowerToys CmdPal extension framework (Microsoft Command Palette Extensions Toolkit).
- Semantic reranking capability aligns with modern retrieval engineering practices, thanks to community resources and examples.

## Contributing

Issues and pull requests are welcome.

## Privacy

[Privacy Policy](./PRIVACY.md)

## License

MIT
