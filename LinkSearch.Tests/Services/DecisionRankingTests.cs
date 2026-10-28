using System;
using System.Linq;
using System.Text.Json;
using LinkSearch.Models;
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class DecisionRankingTests
{
    [Fact]
    public void Noul_request_uses_one_question_per_document_without_criteria()
    {
        var bytes = DecisionRanking.CreateRequest(RankingMode.DecisionNoul, "Eapil-D1", "搜索", new[] { "first", "second" });
        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;

        Assert.Equal("Eapil-D1", root.GetProperty("model").GetString());
        Assert.Equal("搜索", root.GetProperty("state").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        var questions = root.GetProperty("questions");
        Assert.Equal(2, questions.EnumerateObject().Count());
        var first = questions.GetProperty("rel_0");
        Assert.Equal("noul", first.GetProperty("type").GetString());
        Assert.Equal("Is this bookmark relevant to the query? first", first.GetProperty("instructions").GetString());
        Assert.False(first.TryGetProperty("criteria", out _));
        Assert.Contains("second", questions.GetProperty("rel_1").GetProperty("instructions").GetString());
    }

    [Fact]
    public void Choice_request_uses_one_question_with_all_documents_as_options()
    {
        var bytes = DecisionRanking.CreateRequest(RankingMode.DecisionChoice, "Eapil-D1", "query", new[] { "first", "second" });
        using var json = JsonDocument.Parse(bytes);
        var questions = json.RootElement.GetProperty("questions");

        Assert.Single(questions.EnumerateObject());
        var rank = questions.GetProperty("rank");
        Assert.Equal("choice", rank.GetProperty("type").GetString());
        Assert.Equal("first", rank.GetProperty("criteria").GetProperty("doc_0").GetString());
        Assert.Equal("second", rank.GetProperty("criteria").GetProperty("doc_1").GetString());
    }

    [Fact]
    public void Choice_request_keeps_large_candidate_sets_in_one_comparison()
    {
        var documents = Enumerable.Range(0, 200).Select(i => $"document {i}").ToArray();
        using var json = JsonDocument.Parse(DecisionRanking.CreateRequest(RankingMode.DecisionChoice, "model", "query", documents));

        var questions = json.RootElement.GetProperty("questions");
        Assert.Single(questions.EnumerateObject());
        var criteria = questions.GetProperty("rank").GetProperty("criteria");
        Assert.Equal(200, criteria.EnumerateObject().Count());
        Assert.Equal("document 199", criteria.GetProperty("doc_199").GetString());
    }

    [Theory]
    [InlineData("noul", "{\"answers\":{\"rel_0\":{\"type\":\"noul\",\"noul\":0.2},\"rel_1\":{\"type\":\"noul\",\"noul\":0.9},\"rel_2\":{\"type\":\"noul\",\"noul\":0.2}}}")]
    [InlineData("choice", "{\"answers\":{\"rank\":{\"type\":\"choice\",\"choice\":\"doc_1\",\"probabilities\":{\"doc_0\":0.1,\"doc_1\":0.8,\"doc_2\":0.1}}}}")]
    public void Valid_response_orders_by_score_and_preserves_input_order_for_ties(string mode, string payload)
    {
        using var json = JsonDocument.Parse(payload);

        Assert.True(DecisionRanking.TryParseResponse(json.RootElement, ParseMode(mode), 3, out var response));
        Assert.NotNull(response);
        Assert.Equal(new[] { 1, 0, 2 }, response.Results.Select(result => result.Index));
    }

    [Theory]
    [InlineData("noul", "{}")]
    [InlineData("noul", "{\"answers\":{\"rel_0\":{\"type\":\"noul\",\"noul\":0.5}}}")]
    [InlineData("noul", "{\"answers\":{\"rel_0\":{\"type\":\"choice\",\"noul\":0.5},\"rel_1\":{\"type\":\"noul\",\"noul\":0.5}}}")]
    [InlineData("noul", "{\"answers\":{\"rel_0\":{\"type\":\"noul\",\"noul\":-0.1},\"rel_1\":{\"type\":\"noul\",\"noul\":0.5}}}")]
    [InlineData("noul", "{\"answers\":{\"rel_0\":{\"type\":\"noul\",\"noul\":1e999},\"rel_1\":{\"type\":\"noul\",\"noul\":0.5}}}")]
    [InlineData("choice", "{\"answers\":{\"rank\":{\"type\":\"choice\",\"choice\":\"doc_0\",\"probabilities\":{\"doc_0\":1}}}}")]
    [InlineData("choice", "{\"answers\":{\"rank\":{\"type\":\"choice\",\"choice\":\"other\",\"probabilities\":{\"doc_0\":0.5,\"doc_1\":0.5}}}}")]
    [InlineData("choice", "{\"answers\":{\"rank\":{\"type\":\"choice\",\"choice\":\"doc_0\",\"probabilities\":{\"doc_0\":0.7,\"doc_1\":0.7}}}}")]
    [InlineData("choice", "{\"answers\":{\"rank\":{\"type\":\"choice\",\"choice\":\"doc_0\",\"probabilities\":{\"doc_0\":0.5,\"other\":0.5}}}}")]
    public void Invalid_or_incomplete_response_is_rejected(string mode, string payload)
    {
        using var json = JsonDocument.Parse(payload);

        Assert.False(DecisionRanking.TryParseResponse(json.RootElement, ParseMode(mode), 2, out var response));
        Assert.Null(response);
    }

    [Fact]
    public void Rerank_mode_is_not_a_decision_request()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DecisionRanking.CreateRequest(RankingMode.Rerank, "model", "query", new[] { "document" }));
    }

    private static RankingMode ParseMode(string mode) => mode == "noul" ? RankingMode.DecisionNoul : RankingMode.DecisionChoice;
}
