using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal static class OpenAiDecisionRanking
{
    internal static byte[] CreateRequest(RankingMode mode, string model, string query, IReadOnlyList<string> documents)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteString("input", query);
            writer.WriteStartArray("questions");
            if (mode == RankingMode.DecisionNoul)
            {
                for (var i = 0; i < documents.Count; i++)
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", "predicate");
                    writer.WriteString("name", $"rel_{i}");
                    writer.WriteString("instructions", $"Is this bookmark relevant to the query? {documents[i]}");
                    writer.WriteEndObject();
                }
            }
            else if (mode == RankingMode.DecisionChoice)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "choice");
                writer.WriteString("name", "rank");
                writer.WriteString("instructions", "Which bookmark is most relevant to the query?");
                writer.WriteStartArray("choices");
                for (var i = 0; i < documents.Count; i++)
                {
                    writer.WriteStartObject();
                    writer.WriteString("value", $"doc_{i}");
                    writer.WriteString("description", documents[i]);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    internal static bool TryParseResponse(JsonElement root, RankingMode mode, int documentCount, out RerankResponse? response)
    {
        response = null;
        if (documentCount <= 0 || root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var results = new RerankResult[documentCount];
        if (mode == RankingMode.DecisionNoul)
        {
            if (answers.GetArrayLength() != documentCount)
            {
                return false;
            }

            var seen = new bool[documentCount];
            foreach (var answer in answers.EnumerateArray())
            {
                if (!HasType(answer, "predicate") || !TryIndex(answer, "name", "rel_", documentCount, out var index) ||
                    seen[index] || !TryProbability(answer, "probability", out var score))
                {
                    return false;
                }

                seen[index] = true;
                results[index] = RerankResult.Create(index, score);
            }
        }
        else if (mode == RankingMode.DecisionChoice)
        {
            if (answers.GetArrayLength() != 1)
            {
                return false;
            }

            var answer = answers[0];
            if (!HasType(answer, "choice") || !answer.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String || name.GetString() != "rank" ||
                !TryIndex(answer, "choice", "doc_", documentCount, out _) ||
                !answer.TryGetProperty("probabilities", out var probabilities) || probabilities.ValueKind != JsonValueKind.Array ||
                probabilities.GetArrayLength() != documentCount)
            {
                return false;
            }

            var seen = new bool[documentCount];
            var sum = 0.0;
            foreach (var probability in probabilities.EnumerateArray())
            {
                if (probability.ValueKind != JsonValueKind.Object ||
                    !TryIndex(probability, "value", "doc_", documentCount, out var index) || seen[index] ||
                    !TryProbability(probability, "probability", out var score))
                {
                    return false;
                }

                seen[index] = true;
                sum += score;
                results[index] = RerankResult.Create(index, score);
            }

            if (Math.Abs(sum - 1.0) > 0.001)
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

    private static bool HasType(JsonElement value, string expected) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty("type", out var type) &&
        type.ValueKind == JsonValueKind.String && type.GetString() == expected;

    private static bool TryIndex(JsonElement value, string property, string prefix, int count, out int index)
    {
        index = -1;
        if (!value.TryGetProperty(property, out var name) || name.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = name.GetString();
        return text is not null && text.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(text.AsSpan(prefix.Length), out index) && index >= 0 && index < count &&
            string.Equals(text, $"{prefix}{index}", StringComparison.Ordinal);
    }

    private static bool TryProbability(JsonElement value, string property, out double probability)
    {
        probability = 0;
        return value.TryGetProperty(property, out var number) && number.ValueKind == JsonValueKind.Number &&
            number.TryGetDouble(out probability) && double.IsFinite(probability) && probability >= 0 && probability <= 1;
    }
}
