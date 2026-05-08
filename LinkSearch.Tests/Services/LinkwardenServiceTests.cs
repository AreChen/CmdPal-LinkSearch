using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LinkSearch.Helpers;
using LinkSearch.Models;
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class LinkwardenServiceTests
{
    private static readonly DateTimeOffset s_fixedTime = DateTimeOffset.Parse("2026-05-08T00:00:00Z", CultureInfo.InvariantCulture);

    [Fact]
    public async Task SearchAsync_propagates_caller_cancellation()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var handler = new FakeHttpMessageHandler((_, cancellationToken) => Task.FromCanceled<HttpResponseMessage>(cancellationToken));
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync("query", cancellation.Token));
    }

    [Fact]
    public async Task SearchAsync_maps_handler_task_cancellation_to_timeout_when_caller_token_is_not_canceled()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var handler = new FakeHttpMessageHandler((_, _) => throw new TaskCanceledException("timeout"));
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);

        var result = await service.SearchAsync("query", CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal(LinkwardenSearchErrorKind.Timeout, result.Error.Kind);
    }

    [Fact]
    public async Task SearchAsync_caches_successful_results_for_same_query()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("A", "https://example.com/a")));
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);

        var first = await service.SearchAsync("query", CancellationToken.None);
        var second = await service.SearchAsync("query", CancellationToken.None);

        Assert.Null(first.Error);
        Assert.Null(second.Error);
        Assert.Equal("A", Assert.Single(second.Links).Name);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_does_not_cache_parser_failures()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var requestCount = 0;
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(requestCount == 1
                ? JsonResponse("{ \"items\": [] }")
                : JsonResponse("A", "https://example.com/a"));
        });
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);

        var first = await service.SearchAsync("query", CancellationToken.None);
        var second = await service.SearchAsync("query", CancellationToken.None);

        Assert.NotNull(first.Error);
        Assert.Null(second.Error);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchAsync_sets_per_request_headers_without_mutating_http_client_defaults()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("A", "https://example.com/a")));
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);

        var result = await service.SearchAsync("query", CancellationToken.None);

        Assert.Null(result.Error);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("valid-token-1", request.AuthorizationParameter);
        Assert.Contains("application/json", request.AcceptMediaTypes);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.Empty(client.DefaultRequestHeaders.Accept);
    }

    [Fact]
    public async Task SearchAsync_clears_cache_when_token_changes_for_same_base_url_and_query()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var requestCount = 0;
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(requestCount == 1
                ? JsonResponse("A", "https://example.com/a")
                : JsonResponse("B", "https://example.com/b"));
        });
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);

        var first = await service.SearchAsync("query", CancellationToken.None);
        settings.SetForTest("linkSearch.LinkwardenApiKey", "valid-token-2");
        var second = await service.SearchAsync("query", CancellationToken.None);

        Assert.Equal("A", Assert.Single(first.Links).Name);
        Assert.Equal("B", Assert.Single(second.Links).Name);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchAsync_does_not_allow_old_in_flight_request_to_repopulate_cache_after_token_changes()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpMessageHandler(async (request, _) =>
        {
            if (request.Headers.Authorization?.Parameter == "valid-token-1")
            {
                firstRequestStarted.SetResult();
                await releaseFirstRequest.Task;
                return JsonResponse("A", "https://example.com/a");
            }

            return JsonResponse("B", "https://example.com/b");
        });
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);

        var first = service.SearchAsync("query", CancellationToken.None);
        await firstRequestStarted.Task;
        settings.SetForTest("linkSearch.LinkwardenApiKey", "valid-token-2");
        var second = await service.SearchAsync("query", CancellationToken.None);

        releaseFirstRequest.SetResult();
        var completedFirst = await first;
        var third = await service.SearchAsync("query", CancellationToken.None);

        Assert.Equal("B", Assert.Single(second.Links).Name);
        Assert.Equal("A", Assert.Single(completedFirst.Links).Name);
        Assert.Equal("B", Assert.Single(third.Links).Name);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, (int)LinkwardenSearchErrorKind.Authentication, "401")]
    [InlineData(HttpStatusCode.Forbidden, (int)LinkwardenSearchErrorKind.Authorization, "403")]
    [InlineData(HttpStatusCode.NotFound, (int)LinkwardenSearchErrorKind.ApiFailure, "404")]
    [InlineData(HttpStatusCode.InternalServerError, (int)LinkwardenSearchErrorKind.ApiFailure, "500")]
    public async Task SearchAsync_maps_non_success_status_codes(HttpStatusCode statusCode, int expectedKind, string expectedDetail)
    {
        using var temp = new TempSettingsFile();
        var settings = CreateSettings(temp.Path, "valid-token-1");
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));
        using var client = new HttpClient(handler);
        var service = CreateService(settings, client);

        var result = await service.SearchAsync("query", CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal((LinkwardenSearchErrorKind)expectedKind, result.Error.Kind);
        Assert.Equal(expectedDetail, result.Error.Detail);
        Assert.Equal((int)statusCode, result.Error.StatusCode);
    }

    private static LinkwardenService CreateService(SettingsManager settings, HttpClient client)
    {
        return new LinkwardenService(settings, new SearchCache(TimeSpan.FromMinutes(5), () => s_fixedTime), client);
    }

    private static SettingsManager CreateSettings(string path, string apiKey)
    {
        var settings = new SettingsManager(path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LinkwardenBaseUrl", "https://cloud.linkwarden.app");
        settings.SetForTest("linkSearch.LinkwardenApiKey", apiKey);
        return settings;
    }

    private static HttpResponseMessage JsonResponse(string name, string url)
    {
        return JsonResponse($"{{ \"data\": [ {{ \"name\": \"{name}\", \"url\": \"{url}\" }} ] }}");
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        };
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RequestSnapshot(
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.Accept.Select(header => header.MediaType ?? string.Empty).ToArray()));
            return sendAsync(request, cancellationToken);
        }
    }

    private sealed record RequestSnapshot(string? AuthorizationScheme, string? AuthorizationParameter, IReadOnlyList<string> AcceptMediaTypes);

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
