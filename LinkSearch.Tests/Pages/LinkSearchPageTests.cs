using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LinkSearch.Helpers;
using LinkSearch.Presenters;
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Pages;

public sealed class LinkSearchPageTests
{
    private const int PreserveSelectionRefresh = -2;

    [Fact]
    public async Task UpdateSearchText_refreshes_results_without_forcing_first_item_selection()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LinkwardenBaseUrl", "https://cloud.linkwarden.app");
        settings.SetForTest("linkSearch.LinkwardenApiKey", "valid-token-1");
        settings.SetForTest("linkSearch.SearchDelayMilliseconds", "300");
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("Trend Paper", "https://example.com/trend-paper")));
        using var client = new HttpClient(handler);
        var linkwardenService = new LinkwardenService(settings, new SearchCache(TimeSpan.FromMinutes(5)), client);
        using var rerankService = new RerankService(settings);
        using var rerankConnectionTestService = new RerankConnectionTestService(settings);
        var presenter = new SearchResultPresenter(settings);
        using var page = new LinkSearchPage(settings, linkwardenService, rerankService, rerankConnectionTestService, presenter);
        var refreshMode = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, args) => refreshMode.TrySetResult(args.TotalItems);

        page.UpdateSearchText(string.Empty, "trend paper");

        var completed = await Task.WhenAny(refreshMode.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(refreshMode.Task, completed);
        Assert.Equal(PreserveSelectionRefresh, await refreshMode.Task);
    }

    [Fact]
    public async Task UpdateSearchText_coalesces_rapid_input_into_single_search_for_latest_query()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LinkwardenBaseUrl", "https://cloud.linkwarden.app");
        settings.SetForTest("linkSearch.LinkwardenApiKey", "valid-token-1");
        settings.SetForTest("linkSearch.SearchDelayMilliseconds", "300");
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("Trend Paper", "https://example.com/trend-paper")));
        using var client = new HttpClient(handler);
        var linkwardenService = new LinkwardenService(settings, new SearchCache(TimeSpan.FromMinutes(5)), client);
        using var rerankService = new RerankService(settings);
        using var rerankConnectionTestService = new RerankConnectionTestService(settings);
        var presenter = new SearchResultPresenter(settings);
        using var page = new LinkSearchPage(settings, linkwardenService, rerankService, rerankConnectionTestService, presenter);
        var refreshMode = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, args) => refreshMode.TrySetResult(args.TotalItems);

        var query = string.Empty;
        foreach (var character in "trend paper")
        {
            var oldQuery = query;
            query += character;
            page.UpdateSearchText(oldQuery, query);
        }

        var completed = await Task.WhenAny(refreshMode.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(refreshMode.Task, completed);
        Assert.Equal(PreserveSelectionRefresh, await refreshMode.Task);
        Assert.Collection(handler.Queries, query => Assert.Equal("trend paper", query));
    }

    [Fact]
    public async Task UpdateSearchText_runs_latest_query_after_slow_in_flight_search_ignores_cancellation()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LinkwardenBaseUrl", "https://cloud.linkwarden.app");
        settings.SetForTest("linkSearch.LinkwardenApiKey", "valid-token-1");
        settings.SetForTest("linkSearch.SearchDelayMilliseconds", "300");
        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequestToken = CancellationToken.None;
        var requestCount = 0;
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                firstRequestToken = cancellationToken;
                firstRequestStarted.SetResult();
                await releaseFirstRequest.Task;
            }

            return JsonResponse("Trend Paper", "https://example.com/trend-paper");
        });
        using var client = new HttpClient(handler);
        var linkwardenService = new LinkwardenService(settings, new SearchCache(TimeSpan.FromMinutes(5)), client);
        using var rerankService = new RerankService(settings);
        using var rerankConnectionTestService = new RerankConnectionTestService(settings);
        var presenter = new SearchResultPresenter(settings);
        using var page = new LinkSearchPage(settings, linkwardenService, rerankService, rerankConnectionTestService, presenter);

        page.UpdateSearchText(string.Empty, "trend");
        var firstStarted = await Task.WhenAny(firstRequestStarted.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(firstRequestStarted.Task, firstStarted);

        page.UpdateSearchText("trend", "trend paper");
        var finalQueryStarted = await WaitUntilAsync(() => handler.Queries.Contains("trend paper"), TimeSpan.FromSeconds(3));
        Assert.True(finalQueryStarted, $"Observed queries before releasing first request: {string.Join(", ", handler.Queries)}");
        Assert.False(firstRequestToken.IsCancellationRequested);

        releaseFirstRequest.SetResult();

        var finalQueryObserved = await WaitUntilAsync(() => handler.Queries.Contains("trend paper"), TimeSpan.FromSeconds(2));
        Assert.True(finalQueryObserved, $"Observed queries: {string.Join(", ", handler.Queries)}");
    }

    [Fact]
    public async Task UpdateSearchText_does_not_search_single_character_left_after_rapid_backspace()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LinkwardenBaseUrl", "https://cloud.linkwarden.app");
        settings.SetForTest("linkSearch.LinkwardenApiKey", "valid-token-1");
        settings.SetForTest("linkSearch.SearchDelayMilliseconds", "300");
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("Trend Paper", "https://example.com/trend-paper")));
        using var client = new HttpClient(handler);
        var linkwardenService = new LinkwardenService(settings, new SearchCache(TimeSpan.FromMinutes(5)), client);
        using var rerankService = new RerankService(settings);
        using var rerankConnectionTestService = new RerankConnectionTestService(settings);
        var presenter = new SearchResultPresenter(settings);
        using var page = new LinkSearchPage(settings, linkwardenService, rerankService, rerankConnectionTestService, presenter);

        page.UpdateSearchText(string.Empty, "paper");
        var firstQueryObserved = await WaitUntilAsync(() => handler.Queries.Contains("paper"), TimeSpan.FromSeconds(3));
        Assert.True(firstQueryObserved, $"Observed queries: {string.Join(", ", handler.Queries)}");

        page.UpdateSearchText("paper", "p");
        await Task.Delay(TimeSpan.FromMilliseconds(800));

        Assert.DoesNotContain("p", handler.Queries);
    }

    [Fact]
    public async Task SettingsChanged_prevents_old_in_flight_search_from_overwriting_refreshed_results()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LinkwardenBaseUrl", "https://cloud.linkwarden.app");
        settings.SetForTest("linkSearch.LinkwardenApiKey", "valid-token-1");
        settings.SetForTest("linkSearch.SearchDelayMilliseconds", "10");
        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var handler = new FakeHttpMessageHandler(async (_, _) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                firstRequestStarted.SetResult();
                await releaseFirstRequest.Task;
                return JsonResponse("Old Result", "https://example.com/old");
            }

            return JsonResponse("New Result", "https://example.com/new");
        });
        using var client = new HttpClient(handler);
        var linkwardenService = new LinkwardenService(settings, new SearchCache(TimeSpan.FromMinutes(5)), client);
        using var rerankService = new RerankService(settings);
        using var rerankConnectionTestService = new RerankConnectionTestService(settings);
        var presenter = new SearchResultPresenter(settings);
        using var page = new LinkSearchPage(settings, linkwardenService, rerankService, rerankConnectionTestService, presenter);

        page.UpdateSearchText(string.Empty, "trend");
        var firstStarted = await Task.WhenAny(firstRequestStarted.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(firstRequestStarted.Task, firstStarted);

        InvokeSettingsChanged(page, settings);
        var refreshedSearchObserved = await WaitUntilAsync(() => handler.Queries.Count >= 2, TimeSpan.FromSeconds(3));
        Assert.True(refreshedSearchObserved, $"Observed queries: {string.Join(", ", handler.Queries)}");
        var refreshedResultsApplied = await WaitUntilAsync(() => ContainsItemTitle(page, "New Result"), TimeSpan.FromSeconds(3));
        Assert.True(refreshedResultsApplied, "Expected refreshed results before releasing the old request.");

        releaseFirstRequest.SetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Assert.True(ContainsItemTitle(page, "New Result"));
        Assert.False(ContainsItemTitle(page, "Old Result"));
    }

    private static HttpResponseMessage JsonResponse(string name, string url)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{ \"data\": [ {{ \"name\": \"{name}\", \"url\": \"{url}\" }} ] }}"),
        };
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopAt = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < stopAt)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }

    private static bool ContainsItemTitle(LinkSearchPage page, string title)
    {
        foreach (var item in page.GetItems())
        {
            if (string.Equals(item.Title, title, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void InvokeSettingsChanged(LinkSearchPage page, SettingsManager settings)
    {
        var method = typeof(LinkSearchPage).GetMethod("OnSettingsChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(page, new object?[] { settings, settings.Settings });
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        public List<string> Queries { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = GetSearchQuery(request.RequestUri);
            Queries.Add(query);
            return sendAsync(request, cancellationToken);
        }

        private static string GetSearchQuery(Uri? uri)
        {
            var query = uri?.Query.TrimStart('?') ?? string.Empty;
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2 && string.Equals(keyValue[0], "searchQueryString", StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(keyValue[1].Replace('+', ' '));
                }
            }

            return string.Empty;
        }
    }

    private sealed class TempSettingsFile : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LinkSearch.Tests", Guid.NewGuid().ToString("N"));

        public TempSettingsFile()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "settings.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
