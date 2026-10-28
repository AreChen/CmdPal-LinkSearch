using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal static class DecisionRanking
{
    internal static byte[] CreateRequest(RankingMode mode, string model, string query, IReadOnlyList<string> documents)
    {
        if (mode is not (RankingMode.DecisionNoul or RankingMode.DecisionChoice))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteString("state", query);
            writer.WriteStartObject("questions");

            if (mode == RankingMode.DecisionNoul)
            {
                for (var i = 0; i < documents.Count; i++)
                {
                    writer.WriteStartObject($"rel_{i}");
                    writer.WriteString("type", "noul");
                    writer.WriteString("instructions", $"Is this bookmark relevant to the query? {documents[i]}");
                    writer.WriteEndObject();
                }
            }
            else
            {
                writer.WriteStartObject("rank");
                writer.WriteString("type", "choice");
                writer.WriteString("instructions", "Which bookmark is most relevant to the query?");
                writer.WriteStartObject("criteria");
                for (var i = 0; i < documents.Count; i++)
                {
                    writer.WriteString($"doc_{i}", documents[i]);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteBoolean("stream", false);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    internal static bool TryParseResponse(JsonElement root, RankingMode mode, int documentCount, out RerankResponse? response)
    {
        response = null;
        if (documentCount <= 0 || root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var results = new RerankResult[documentCount];
        if (mode == RankingMode.DecisionNoul)
        {
            if (CountProperties(answers) != documentCount)
            {
                return false;
            }

            for (var i = 0; i < documentCount; i++)
            {
                if (!answers.TryGetProperty($"rel_{i}", out var answer) ||
                    !HasType(answer, "noul") || !TryProbability(answer, "noul", out var score))
                {
                    return false;
                }

                results[i] = RerankResult.Create(i, score);
            }
        }
        else if (mode == RankingMode.DecisionChoice)
        {
            if (CountProperties(answers) != 1 ||
                !answers.TryGetProperty("rank", out var answer) || !HasType(answer, "choice") ||
                !answer.TryGetProperty("choice", out var choice) || choice.ValueKind != JsonValueKind.String ||
                !answer.TryGetProperty("probabilities", out var probabilities) || probabilities.ValueKind != JsonValueKind.Object ||
                CountProperties(probabilities) != documentCount)
            {
                return false;
            }

            var selected = choice.GetString();
            var selectedFound = false;
            var sum = 0.0;
            for (var i = 0; i < documentCount; i++)
            {
                var label = $"doc_{i}";
                if (!TryProbability(probabilities, label, out var score))
                {
                    return false;
                }

                selectedFound |= string.Equals(selected, label, StringComparison.Ordinal);
                sum += score;
                results[i] = RerankResult.Create(i, score);
            }

            if (!selectedFound || Math.Abs(sum - 1.0) > 0.001)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        Array.Sort(results, (left, right) =>
        {
            var byScore = right.RelevanceScore.CompareTo(left.RelevanceScore);
            return byScore != 0 ? byScore : left.Index.CompareTo(right.Index);
        });
        response = new RerankResponse { Results = results };
        return true;
    }

    private static bool HasType(JsonElement answer, string expected) =>
        answer.ValueKind == JsonValueKind.Object &&
        answer.TryGetProperty("type", out var type) &&
        type.ValueKind == JsonValueKind.String &&
        string.Equals(type.GetString(), expected, StringComparison.Ordinal);

    private static bool TryProbability(JsonElement parent, string name, out double value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) &&
            double.IsFinite(value) &&
            value >= 0 && value <= 1;
    }

    private static int CountProperties(JsonElement element)
    {
        var count = 0;
        foreach (var _ in element.EnumerateObject())
        {
            count++;
        }

        return count;
    }
}
