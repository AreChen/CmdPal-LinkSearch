using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LinkSearch.Helpers;
using LinkSearch.Models;
using LinkSearch.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class RankingServiceTests : IDisposable
{
    private const string NoulResponse = "{\"answers\":{\"rel_0\":{\"type\":\"noul\",\"noul\":0.1},\"rel_1\":{\"type\":\"noul\",\"noul\":0.9}}}";
    private const string ChoiceResponse = "{\"answers\":{\"rank\":{\"type\":\"choice\",\"choice\":\"doc_1\",\"probabilities\":{\"doc_0\":0.1,\"doc_1\":0.9}}}}";
    private const string RerankResponse = "{\"results\":[{\"index\":1,\"relevance_score\":0.9},{\"index\":0,\"relevance_score\":0.1}]}";
    private const string OpenAiScoreResponse = "{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":0.1},{\"type\":\"score\",\"name\":\"rel_1\",\"score\":2.9}]}";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LinkSearch.Tests", Guid.NewGuid().ToString("N"));
    private readonly LinkwardenLink[] _links =
    {
        new("first", "first description", "https://private.example/first", "private collection", new[] { "tag-one" }),
        new("second", "second description", "https://private.example/second", "private collection", new[] { "tag-two" }),
    };

    public RankingServiceTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("rerank")]
    [InlineData("decision-noul")]
    [InlineData("decision-choice")]
    public async Task Search_uses_selected_service_and_sends_only_requested_fields(string mode)
    {
        var settings = CreateSettings(mode);
        string? url = null;
        string? key = null;
        string? body = null;
        using var client = new HttpClient(new FakeHandler(async (request, token) =>
        {
            url = request.RequestUri!.ToString();
            key = request.Headers.Authorization!.Parameter;
            body = await request.Content!.ReadAsStringAsync(token);
            return Response(mode switch
            {
                "rerank" => RerankResponse,
                "decision-noul" => NoulResponse,
                _ => ChoiceResponse,
            });
        }));
        using var service = new RerankService(settings, client);

        var result = await service.RerankLinksAsync("search query", _links);

        Assert.Equal(new[] { _links[1], _links[0] }, result);
        Assert.Equal(mode == "rerank" ? "https://rerank.example/v1/rerank" : "https://decision.example/v1/systemone", url);
        Assert.Equal(mode == "rerank" ? "rerank-key" : "decision-key", key);
        Assert.NotNull(body);
        Assert.DoesNotContain("private.example", body);
        Assert.DoesNotContain("private collection", body);
        Assert.Contains("first description", body);
        Assert.Contains("tag-one", body);
        Assert.Contains("second description", body);
        Assert.Contains("tag-two", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(mode == "rerank" ? "rerank-model" : "decision-model", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("search query", json.RootElement.GetProperty(mode == "rerank" ? "query" : "state").GetString());
    }

    [Theory]
    [InlineData("rerank", "rerank")]
    [InlineData("system-one", "decision-noul")]
    [InlineData("system-one", "decision-choice")]
    [InlineData("openrouter", "decision-noul")]
    [InlineData("openrouter", "decision-choice")]
    [InlineData("openai", "decision-noul")]
    [InlineData("openai", "decision-choice")]
    public async Task Search_omits_only_empty_document_fields_for_every_provider(string provider, string mode)
    {
        var settings = CreateSettings(mode);
        settings.SetForTest("linkSearch.DecisionProvider", provider == "rerank" ? "system-one" : provider);
        settings.SetForTest("linkSearch.OpenRouterApiKey", "router-key");
        settings.SetForTest("linkSearch.OpenAiApiKey", "openai-key");
        var links = new[]
        {
            new LinkwardenLink("empty", "", "", "", Array.Empty<string>()),
            new LinkwardenLink("whitespace", " \t ", "", "", new[] { "", " \t " }),
            new LinkwardenLink("description", "  useful description  ", "", "", Array.Empty<string>()),
            new LinkwardenLink("tags", "", "", "", new[] { "first tag", "second tag" }),
            new LinkwardenLink("complete", "useful description", "", "", new[] { "useful tag" }),
        };
        var expected = new[]
        {
            $"Name: empty{Environment.NewLine}",
            $"Name: whitespace{Environment.NewLine}",
            $"Name: description{Environment.NewLine}Description:   useful description  {Environment.NewLine}",
            $"Name: tags{Environment.NewLine}Tags: first tag, second tag{Environment.NewLine}",
            $"Name: complete{Environment.NewLine}Description: useful description{Environment.NewLine}Tags: useful tag{Environment.NewLine}",
        };
        var response = provider switch
        {
            "rerank" => JsonSerializer.Serialize(new { results = links.Select((_, i) => new { index = i, relevance_score = 1.0 }) }),
            "openai" => JsonSerializer.Serialize(new { answers = links.Select((_, i) => new { type = "score", name = $"rel_{i}", score = 1.0 }) }),
            _ when mode == "decision-noul" => JsonSerializer.Serialize(new { answers = links.Select((_, i) => i).ToDictionary(i => $"rel_{i}", _ => new { type = "noul", noul = 0.5 }) }),
            _ => JsonSerializer.Serialize(new { answers = new { rank = new { type = "choice", choice = "doc_0", probabilities = links.Select((_, i) => i).ToDictionary(i => $"doc_{i}", _ => 1.0 / links.Length) } } }),
        };
        string? body = null;
        using var client = new HttpClient(new FakeHandler(async (request, token) =>
        {
            body = await request.Content!.ReadAsStringAsync(token);
            return Response(response);
        }));
        using var service = new RerankService(settings, client);

        var outcome = await service.RankLinksAsync("query", links);

        Assert.Equal(RankingStatus.Applied, outcome.Status);
        Assert.NotNull(body);
        using var json = JsonDocument.Parse(body);
        for (var i = 0; i < links.Length; i++)
        {
            var actual = provider switch
            {
                "rerank" => json.RootElement.GetProperty("documents")[i].GetString(),
                "openai" => json.RootElement.GetProperty("questions")[i].GetProperty("instructions").GetString(),
                _ when mode == "decision-noul" => json.RootElement.GetProperty("questions").GetProperty($"rel_{i}").GetProperty("instructions").GetString(),
                _ => json.RootElement.GetProperty("questions").GetProperty("rank").GetProperty("criteria").GetProperty($"doc_{i}").GetString(),
            };
            Assert.NotNull(actual);
            if (provider is "rerank" || mode == "decision-choice" && provider != "openai")
                Assert.Equal(expected[i], actual);
            else
                Assert.EndsWith(expected[i], actual);
        }
    }

    [Theory]
    [InlineData("openrouter", "decision-noul")]
    [InlineData("openrouter", "decision-choice")]
    [InlineData("openai", "decision-noul")]
    [InlineData("openai", "decision-choice")]
    public async Task Search_and_connection_test_use_the_selected_decision_provider(string provider, string mode)
    {
        var settings = CreateSettings(mode);
        settings.SetForTest("linkSearch.DecisionProvider", provider);
        settings.SetForTest("linkSearch.OpenRouterApiKey", "router-key");
        settings.SetForTest("linkSearch.OpenAiApiKey", "openai-key");
        var calls = 0;
        using var client = new HttpClient(new FakeHandler(async (request, token) =>
        {
            calls++;
            Assert.Equal(provider == "openai" ? "https://api.openai.com/v1/decisions" : "https://openrouter.ai/api/alpha/decisions", request.RequestUri!.ToString());
            Assert.Equal(provider == "openai" ? "openai-key" : "router-key", request.Headers.Authorization!.Parameter);
            var body = await request.Content!.ReadAsStringAsync(token);
            Assert.DoesNotContain("private.example", body);
            Assert.DoesNotContain("private collection", body);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(provider == "openai" ? "gpt-6-luna" : "typesafe/jev-1.14", json.RootElement.GetProperty("model").GetString());
            Assert.False(json.RootElement.TryGetProperty("stream", out _));
            Assert.Equal(provider == "openai" ? JsonValueKind.Array : JsonValueKind.Object, json.RootElement.GetProperty("questions").ValueKind);
            if (provider == "openai") Assert.Equal("score", json.RootElement.GetProperty("questions")[0].GetProperty("type").GetString());
            return Response(provider == "openai"
                ? OpenAiScoreResponse
                : mode == "decision-noul" ? NoulResponse : ChoiceResponse);
        }));
        using var ranking = new RerankService(settings, client);
        using var connection = new RerankConnectionTestService(settings, client);

        Assert.Equal(new[] { _links[1], _links[0] }, await ranking.RerankLinksAsync("query", _links));
        var result = await connection.TestConnectionAsync();
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("openai", "OpenAiApiUrl", "OpenAiApiKey", "https://gateway.example/v1/decisions", OpenAiScoreResponse)]
    [InlineData("openrouter", "OpenRouterApiUrl", "OpenRouterApiKey", "https://gateway.example/api/alpha/decisions", NoulResponse)]
    public async Task Search_uses_provider_specific_gateway_endpoint(string provider, string urlSetting, string keySetting, string url, string payload)
    {
        var settings = CreateSettings("decision-noul");
        settings.SetForTest("linkSearch.DecisionProvider", provider);
        settings.SetForTest($"linkSearch.{urlSetting}", url);
        settings.SetForTest($"linkSearch.{keySetting}", "gateway-key");
        var calls = 0;
        using var client = new HttpClient(new FakeHandler((request, _) =>
        {
            calls++;
            Assert.Equal(url, request.RequestUri!.ToString());
            Assert.Equal("gateway-key", request.Headers.Authorization!.Parameter);
            return Task.FromResult(Response(payload));
        }));
        using var service = new RerankService(settings, client);
        Assert.Equal(new[] { _links[1], _links[0] }, await service.RerankLinksAsync("query", _links));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Refused_openai_answer_preserves_original_order_without_using_rerank()
    {
        var settings = CreateSettings("decision-noul");
        settings.SetForTest("linkSearch.DecisionProvider", "openai");
        settings.SetForTest("linkSearch.OpenAiApiKey", "openai-key");
        var calls = 0;
        using var client = new HttpClient(new FakeHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Response("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":0.1},{\"type\":\"refusal\",\"name\":\"rel_1\"}]}"));
        }));
        using var service = new RerankService(settings, client);
        Assert.Same(_links, await service.RerankLinksAsync("query", _links));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("decision-noul", "{}")]
    [InlineData("decision-noul", "not json")]
    [InlineData("decision-choice", "{\"answers\":{\"rank\":{\"type\":\"choice\",\"choice\":\"doc_1\",\"probabilities\":{\"doc_0\":0.7,\"doc_1\":0.9}}}}")]
    public async Task Invalid_decision_response_preserves_original_order(string mode, string payload)
    {
        var settings = CreateSettings(mode);
        var calls = 0;
        using var client = new HttpClient(new FakeHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Response(payload));
        }));
        using var service = new RerankService(settings, client);

        var result = await service.RerankLinksAsync("query", _links);

        Assert.Same(_links, result);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("decision-noul")]
    [InlineData("decision-choice")]
    public async Task Http_failure_preserves_order_without_calling_another_service(string mode)
    {
        var settings = CreateSettings(mode);
        var calls = 0;
        using var client = new HttpClient(new FakeHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Response("unavailable", HttpStatusCode.ServiceUnavailable));
        }));
        using var service = new RerankService(settings, client);

        var outcome = await service.RankLinksAsync("query", _links);
        Assert.Same(_links, outcome.Links);
        Assert.Equal(RankingStatus.Failed, outcome.Status);
        Assert.Equal(503, outcome.HttpStatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Candidate_limit_only_sends_the_prefix_and_preserves_the_remaining_order()
    {
        var settings = CreateSettings("rerank");
        settings.SetForTest("linkSearch.RankingCandidateLimit", "2");
        var links = _links.Concat(new[]
        {
            new LinkwardenLink("third", "third description", "https://example.com/3", "", Array.Empty<string>()),
            new LinkwardenLink("fourth", "fourth description", "https://example.com/4", "", Array.Empty<string>()),
        }).ToArray();
        using var client = new HttpClient(new FakeHandler(async (request, token) =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal(2, json.RootElement.GetProperty("documents").GetArrayLength());
            return Response("{\"results\":[{\"index\":3,\"relevance_score\":1},{\"index\":1,\"relevance_score\":0.9},{\"index\":0,\"relevance_score\":0.1}]}");
        }));
        using var service = new RerankService(settings, client);
        var outcome = await service.RankLinksAsync("query", links);
        Assert.Equal(RankingStatus.Applied, outcome.Status);
        Assert.Equal(new[] { links[1], links[0], links[2], links[3] }, outcome.Links);
    }

    [Fact]
    public async Task Missing_configuration_is_reported_without_network_request()
    {
        var settings = CreateSettings("decision-noul");
        settings.SetForTest("linkSearch.DecisionApiKey", "");
        using var client = new HttpClient(new FakeHandler((_, _) => throw new InvalidOperationException("Unexpected request")));
        using var service = new RerankService(settings, client);
        var outcome = await service.RankLinksAsync("query", _links);
        Assert.Equal(RankingStatus.ConfigurationError, outcome.Status);
        Assert.Same(_links, outcome.Links);
    }

    [Fact]
    public async Task Single_result_does_not_call_ranking_service()
    {
        var settings = CreateSettings("decision-choice");
        var calls = 0;
        using var client = new HttpClient(new FakeHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Response(ChoiceResponse));
        }));
        using var service = new RerankService(settings, client);
        var one = _links.Take(1).ToArray();

        Assert.Same(one, await service.RerankLinksAsync("query", one));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Invalid_decision_endpoint_preserves_order_without_a_request()
    {
        var settings = CreateSettings("decision-noul");
        settings.SetForTest("linkSearch.DecisionApiUrl", "invalid url");
        var calls = 0;
        using var client = new HttpClient(new FakeHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Response(NoulResponse));
        }));
        using var service = new RerankService(settings, client);

        Assert.Same(_links, await service.RerankLinksAsync("query", _links));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated()
    {
        var settings = CreateSettings("decision-noul");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new FakeHandler(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Response(NoulResponse);
        }));
        using var service = new RerankService(settings, client);
        using var cts = new CancellationTokenSource();

        var task = service.RerankLinksAsync("query", _links, cts.Token);
        await started.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Theory]
    [InlineData("rerank")]
    [InlineData("decision-noul")]
    [InlineData("decision-choice")]
    public async Task Connection_test_uses_the_selected_protocol(string mode)
    {
        var settings = CreateSettings(mode);
        using var client = new HttpClient(new FakeHandler(async (request, token) =>
        {
            Assert.Equal(mode == "rerank" ? "rerank-key" : "decision-key", request.Headers.Authorization!.Parameter);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal(mode == "rerank" ? "rerank-model" : "decision-model", json.RootElement.GetProperty("model").GetString());
            return Response(mode switch
            {
                "rerank" => RerankResponse,
                "decision-noul" => NoulResponse,
                _ => ChoiceResponse,
            });
        }));
        using var service = new RerankConnectionTestService(settings, client);

        var result = await service.TestConnectionAsync();

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Theory]
    [InlineData("rerank", "{\"results\":[{\"index\":\"invalid\"}]}")]
    [InlineData("decision-noul", "{}")]
    [InlineData("decision-choice", "not json")]
    public async Task Connection_test_rejects_invalid_success_responses(string mode, string payload)
    {
        var settings = CreateSettings(mode);
        using var client = new HttpClient(new FakeHandler((_, _) => Task.FromResult(Response(payload))));
        using var service = new RerankConnectionTestService(settings, client);

        var result = await service.TestConnectionAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("ResponseError", result.ErrorType);
    }

    private SettingsManager CreateSettings(string mode)
    {
        var settings = new SettingsManager(Path.Combine(_directory, "settings.json"), null);
        settings.GetSettingForTest<ToggleSetting>("linkSearch.EnableRerank").Value = true;
        var rankingMode = settings.GetSettingForTest<ChoiceSetSetting>("linkSearch.RankingMode");
        var selected = rankingMode.Choices.Single(choice => choice.Value == mode);
        rankingMode.Update(new JsonObject { [rankingMode.Key] = selected.Value });
        settings.SetForTest("linkSearch.RerankApiUrl", "https://rerank.example/v1/rerank");
        settings.SetForTest("linkSearch.RerankApiKey", "rerank-key");
        settings.SetForTest("linkSearch.RerankModelName", "rerank-model");
        settings.SetForTest("linkSearch.DecisionApiUrl", "https://decision.example/v1/systemone");
        settings.SetForTest("linkSearch.DecisionApiKey", "decision-key");
        settings.SetForTest("linkSearch.DecisionModelName", "decision-model");
        return settings;
    }

    private static HttpResponseMessage Response(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
