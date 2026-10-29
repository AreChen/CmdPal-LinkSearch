using System.Linq;
using System.Text.Json;
using LinkSearch.Models;
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class OpenAiDecisionRankingTests
{
    private static readonly string[] Documents = ["first document", "second document"];

    [Theory]
    [InlineData("noul")]
    [InlineData("choice")]
    public void Request_uses_openai_decisions_schema(string method)
    {
        var mode = ParseMode(method);
        using var json = JsonDocument.Parse(DecisionRanking.CreateRequest(mode, "gpt-6-luna", "my query", Documents, DecisionProvider.OpenAi));
        var root = json.RootElement;
        Assert.Equal("gpt-6-luna", root.GetProperty("model").GetString());
        Assert.Equal("my query", root.GetProperty("input").GetString());
        Assert.False(root.TryGetProperty("state", out _));
        Assert.False(root.TryGetProperty("stream", out _));
        var questions = root.GetProperty("questions");
        if (mode == RankingMode.DecisionNoul)
        {
            Assert.Equal(2, questions.GetArrayLength());
            Assert.Equal("predicate", questions[0].GetProperty("type").GetString());
            Assert.Equal("rel_0", questions[0].GetProperty("name").GetString());
            Assert.Contains(Documents[0], questions[0].GetProperty("instructions").GetString());
        }
        else
        {
            Assert.Single(questions.EnumerateArray());
            Assert.Equal("choice", questions[0].GetProperty("type").GetString());
            Assert.Equal("rank", questions[0].GetProperty("name").GetString());
            var choices = questions[0].GetProperty("choices");
            Assert.Equal("doc_1", choices[1].GetProperty("value").GetString());
            Assert.Equal(Documents[1], choices[1].GetProperty("description").GetString());
        }
    }

    [Fact]
    public void OpenRouter_request_uses_system_one_shape_without_stream()
    {
        using var json = JsonDocument.Parse(DecisionRanking.CreateRequest(RankingMode.DecisionNoul, "typesafe/jev-1.13", "my query", Documents, DecisionProvider.OpenRouter));
        Assert.Equal("my query", json.RootElement.GetProperty("state").GetString());
        Assert.Equal("noul", json.RootElement.GetProperty("questions").GetProperty("rel_0").GetProperty("type").GetString());
        Assert.False(json.RootElement.TryGetProperty("stream", out _));
    }

    [Theory]
    [InlineData("noul", "{\"answers\":[{\"type\":\"predicate\",\"name\":\"rel_1\",\"probability\":0.9},{\"type\":\"predicate\",\"name\":\"rel_0\",\"probability\":0.1}]}")]
    [InlineData("choice", "{\"answers\":[{\"type\":\"choice\",\"name\":\"rank\",\"choice\":\"doc_1\",\"probabilities\":[{\"value\":\"doc_1\",\"probability\":0.9},{\"value\":\"doc_0\",\"probability\":0.1}]}]}")]
    public void Response_is_ordered_by_probability_and_matched_by_name(string method, string payload)
    {
        using var json = JsonDocument.Parse(payload);
        Assert.True(DecisionRanking.TryParseResponse(json.RootElement, ParseMode(method), 2, out var response, DecisionProvider.OpenAi));
        Assert.Equal(new[] { 1, 0 }, response!.Results.Select(result => result.Index));
    }

    [Theory]
    [InlineData("noul", "{\"answers\":[{\"type\":\"predicate\",\"name\":\"rel_0\",\"probability\":0.1},{\"type\":\"predicate\",\"name\":\"rel_0\",\"probability\":0.9}]}")]
    [InlineData("noul", "{\"answers\":[{\"type\":\"predicate\",\"name\":\"rel_0\",\"probability\":0.1},{\"type\":\"refusal\",\"name\":\"rel_1\"}]}")]
    [InlineData("noul", "{\"answers\":[{\"type\":\"predicate\",\"name\":\"rel_0\",\"probability\":1.1},{\"type\":\"predicate\",\"name\":\"rel_1\",\"probability\":0.9}]}")]
    [InlineData("choice", "{\"answers\":[{\"type\":\"choice\",\"name\":\"rank\",\"choice\":\"doc_1\"}]}")]
    [InlineData("choice", "{\"answers\":[{\"type\":\"choice\",\"name\":\"rank\",\"choice\":\"doc_2\",\"probabilities\":[{\"value\":\"doc_0\",\"probability\":0.1},{\"value\":\"doc_1\",\"probability\":0.9}]}]}")]
    [InlineData("choice", "{\"answers\":[{\"type\":\"choice\",\"name\":\"rank\",\"choice\":\"doc_1\",\"probabilities\":[{\"value\":\"doc_0\",\"probability\":0.7},{\"value\":\"doc_1\",\"probability\":0.9}]}]}")]
    public void Invalid_or_incomplete_answers_are_rejected(string method, string payload)
    {
        using var json = JsonDocument.Parse(payload);
        Assert.False(DecisionRanking.TryParseResponse(json.RootElement, ParseMode(method), 2, out _, DecisionProvider.OpenAi));
    }

    [Fact]
    public void Equal_probabilities_preserve_original_order()
    {
        using var json = JsonDocument.Parse("{\"answers\":[{\"type\":\"predicate\",\"name\":\"rel_1\",\"probability\":0.5},{\"type\":\"predicate\",\"name\":\"rel_0\",\"probability\":0.5}]}");
        Assert.True(DecisionRanking.TryParseResponse(json.RootElement, RankingMode.DecisionNoul, 2, out var response, DecisionProvider.OpenAi));
        Assert.Equal(new[] { 0, 1 }, response!.Results.Select(result => result.Index));
    }

    private static RankingMode ParseMode(string value) => value == "noul" ? RankingMode.DecisionNoul : RankingMode.DecisionChoice;
}
