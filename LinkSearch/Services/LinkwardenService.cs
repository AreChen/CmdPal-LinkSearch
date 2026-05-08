using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal sealed class LinkwardenService
{
    private readonly SettingsManager _settingsManager;
    private readonly HttpClient _httpClient;
    private readonly SearchCache _cache;
    private readonly object _cacheInvalidationLock = new();
    private string? _lastTokenForCache;
    private int _cacheGeneration;

    public LinkwardenService(SettingsManager settingsManager, SearchCache cache, HttpClient? httpClient = null)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _httpClient = httpClient ?? HttpClientProvider.Shared;
    }

    public async Task<LinkwardenSearchResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return LinkwardenSearchResult.Success(Array.Empty<LinkwardenLink>());
        }

        var token = _settingsManager.LinkwardenApiKey;
        if (string.IsNullOrWhiteSpace(token))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.MissingTokenTitle));
        }

        if (!SettingsManager.ValidateApiKey(token))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.InvalidApiKeyTitle));
        }

        int cacheGeneration;
        lock (_cacheInvalidationLock)
        {
            if (!StringComparer.Ordinal.Equals(_lastTokenForCache, token))
            {
                _cache.Clear();
                _lastTokenForCache = token;
                _cacheGeneration++;
            }

            cacheGeneration = _cacheGeneration;
        }

        var baseUrl = _settingsManager.LinkwardenBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.InvalidBaseUrlTitle));
        }

        if (_cache.TryGet(baseUrl, query, out var cachedLinks))
        {
            return LinkwardenSearchResult.Success(cachedLinks);
        }

        try
        {
            var url = $"{baseUrl}/api/v1/search?searchQueryString={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var key = (int)response.StatusCode switch
                {
                    401 => LocalizedTextKey.InvalidApiKeyTitle,
                    403 => LocalizedTextKey.ApiRequestFailed,
                    _ => LocalizedTextKey.ApiRequestFailed,
                };
                var kind = (int)response.StatusCode switch
                {
                    401 => LinkwardenSearchErrorKind.Authentication,
                    403 => LinkwardenSearchErrorKind.Authorization,
                    _ => LinkwardenSearchErrorKind.ApiFailure,
                };

                var statusCode = (int)response.StatusCode;
                return LinkwardenSearchResult.Failure(new LinkwardenSearchError(kind, key, statusCode.ToString(CultureInfo.InvariantCulture), statusCode));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var parsed = LinkwardenResponseParser.Parse(document.RootElement);
            if (parsed.Error is null)
            {
                lock (_cacheInvalidationLock)
                {
                    if (cacheGeneration == _cacheGeneration && StringComparer.Ordinal.Equals(_lastTokenForCache, token))
                    {
                        _cache.Set(baseUrl, query, parsed.Links);
                    }
                }
            }

            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Network, LocalizedTextKey.NetworkRequestFailed, ex.Message));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Timeout, LocalizedTextKey.RequestTimeout));
        }
        catch (UriFormatException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.UrlFormatError, ex.Message));
        }
        catch (JsonException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.InvalidDataNode, ex.Message));
        }
        catch (IOException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Network, LocalizedTextKey.NetworkRequestFailed, ex.Message));
        }
    }
}
