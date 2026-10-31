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
    public void Previous_modes_use_score_without_changing_the_configured_provider(string previousMode)
    {
        var mode = previousMode == "noul" ? RankingMode.DecisionNoul : RankingMode.DecisionChoice;
        using var json = JsonDocument.Parse(DecisionRanking.CreateRequest(mode, "gpt-6-luna", "my query", Documents, DecisionProvider.OpenAi));
        var root = json.RootElement;
        Assert.Equal("gpt-6-luna", root.GetProperty("model").GetString());
        Assert.Equal("my query", root.GetProperty("input").GetString());
        Assert.False(root.TryGetProperty("state", out _));
        var questions = root.GetProperty("questions");
        Assert.Equal(2, questions.GetArrayLength());
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal("score", questions[i].GetProperty("type").GetString());
            Assert.Equal($"rel_{i}", questions[i].GetProperty("name").GetString());
            Assert.Contains("Search query: my query.", questions[i].GetProperty("instructions").GetString());
            Assert.Contains(Documents[i], questions[i].GetProperty("instructions").GetString());
            Assert.Equal(new[] { "unrelated", "weak", "partial", "direct" },
                questions[i].GetProperty("levels").EnumerateArray().Select(level => level.GetProperty("label").GetString()));
            Assert.All(questions[i].GetProperty("levels").EnumerateArray(), level => Assert.False(string.IsNullOrWhiteSpace(level.GetProperty("description").GetString())));
        }
    }

    [Fact]
    public void Query_is_preserved_in_each_score_question()
    {
        const string query = "OpenAI status \"incident\"\nservice";
        using var json = JsonDocument.Parse(DecisionRanking.CreateRequest(RankingMode.DecisionNoul, "example", query, Documents, DecisionProvider.OpenAi));
        Assert.Equal(query, json.RootElement.GetProperty("input").GetString());
        foreach (var question in json.RootElement.GetProperty("questions").EnumerateArray())
        {
            Assert.Contains($"Search query: {query}.", question.GetProperty("instructions").GetString());
        }
    }

    [Fact]
    public void OpenRouter_still_uses_system_one_noul_shape()
    {
        using var json = JsonDocument.Parse(DecisionRanking.CreateRequest(RankingMode.DecisionNoul, "typesafe/jev-1.14", "my query", Documents, DecisionProvider.OpenRouter));
        Assert.Equal("my query", json.RootElement.GetProperty("state").GetString());
        Assert.Equal("noul", json.RootElement.GetProperty("questions").GetProperty("rel_0").GetProperty("type").GetString());
        Assert.False(json.RootElement.TryGetProperty("stream", out _));
    }

    [Theory]
    [InlineData("noul")]
    [InlineData("choice")]
    public void Responses_sort_by_score_and_tolerate_metadata(string previousMode)
    {
        var mode = previousMode == "noul" ? RankingMode.DecisionNoul : RankingMode.DecisionChoice;
        using var json = JsonDocument.Parse("{\"model\":\"example\",\"answers\":[{\"type\":\"score\",\"name\":\"rel_1\",\"score\":3.0,\"confidence\":0.7},{\"type\":\"score\",\"name\":\"rel_0\",\"score\":0.0}]}");
        Assert.True(DecisionRanking.TryParseResponse(json.RootElement, mode, 2, out var response, DecisionProvider.OpenAi));
        Assert.Equal(new[] { 1, 0 }, response!.Results.Select(result => result.Index));
    }

    [Theory]
    [InlineData("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":1},{\"type\":\"score\",\"name\":\"rel_0\",\"score\":2}]}")]
    [InlineData("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":1},{\"type\":\"refusal\",\"name\":\"rel_1\"}]}")]
    [InlineData("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":-0.1},{\"type\":\"score\",\"name\":\"rel_1\",\"score\":2}]}")]
    [InlineData("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":3.1},{\"type\":\"score\",\"name\":\"rel_1\",\"score\":2}]}")]
    [InlineData("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":1}]}")]
    [InlineData("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_00\",\"score\":1},{\"type\":\"score\",\"name\":\"rel_1\",\"score\":2}]}")]
    [InlineData("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_0\",\"score\":\"1\"},{\"type\":\"score\",\"name\":\"rel_1\",\"score\":2}]}")]
    public void Invalid_or_incomplete_answers_are_rejected(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        Assert.False(DecisionRanking.TryParseResponse(json.RootElement, RankingMode.DecisionNoul, 2, out _, DecisionProvider.OpenAi));
    }

    [Fact]
    public void Equal_scores_preserve_original_order()
    {
        using var json = JsonDocument.Parse("{\"answers\":[{\"type\":\"score\",\"name\":\"rel_1\",\"score\":1.5},{\"type\":\"score\",\"name\":\"rel_0\",\"score\":1.5}]}");
        Assert.True(DecisionRanking.TryParseResponse(json.RootElement, RankingMode.DecisionNoul, 2, out var response, DecisionProvider.OpenAi));
        Assert.Equal(new[] { 0, 1 }, response!.Results.Select(result => result.Index));
    }
}
