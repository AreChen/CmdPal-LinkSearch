# LinkSearch Privacy Policy

Last updated: 2026-05-09

LinkSearch is a PowerToys Command Palette extension for searching links from a Linkwarden server that you configure.

## Data We Do Not Collect

The LinkSearch developer does not collect, receive, sell, or share personal data from the app. LinkSearch does not include developer-operated telemetry, analytics, advertising, or crash reporting.

## Data Stored On Your Device

LinkSearch stores its settings locally on your device. These settings may include:

- Your Linkwarden server URL
- Your Linkwarden access token
- Your preferred language and search settings
- Optional rerank API URL, API key, and model settings

These settings are used only to run the extension and are not sent to the LinkSearch developer.

## Network Requests

LinkSearch makes network requests only to services that you configure or enable:

- Linkwarden: LinkSearch sends your search query and Linkwarden access token to your configured Linkwarden server so it can search your links.
- Optional rerank service: If you enable reranking, LinkSearch sends your search query and candidate link information, such as title, description, URL, tags, and collection name, to your configured rerank API provider.

Those services are operated by you or by third parties you choose. Their privacy practices are governed by their own policies.

## Caching And Logs

LinkSearch may keep search results in short-lived memory cache while the extension is running to improve responsiveness. It does not write a persistent search-result cache.

Diagnostic logs, if present, remain on your device. Do not share logs publicly if they may contain service responses or other sensitive details.

## Your Choices

You can remove stored settings by clearing the extension settings or uninstalling LinkSearch. You can disable reranking at any time in the extension settings.

## Contact

For privacy questions or support, open an issue at:

https://github.com/AreChen/CmdPal-LinkSearch/issues
