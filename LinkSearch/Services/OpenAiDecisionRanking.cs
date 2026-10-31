using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal static class OpenAiDecisionRanking
{
    private static readonly (string Label, string Description)[] Levels =
    [
        ("unrelated", "Does not address the user's requested subject or task."),
        ("weak", "Related to the subject but does not address the requested task."),
        ("partial", "Addresses part of the requested task but misses an important requirement."),
        ("direct", "Directly addresses the specific subject and task in the query."),
    ];

    internal static byte[] CreateRequest(string model, string query, IReadOnlyList<string> documents)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteString("input", query);
            writer.WriteStartArray("questions");
            for (var i = 0; i < documents.Count; i++)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "score");
                writer.WriteString("name", $"rel_{i}");
                writer.WriteString("instructions", $"How well does this bookmark address the user's search query? Search query: {query}. {documents[i]}");
                writer.WriteStartArray("levels");
                foreach (var (label, description) in Levels)
                {
                    writer.WriteStartObject();
                    writer.WriteString("label", label);
                    writer.WriteString("description", description);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    internal static bool TryParseResponse(JsonElement root, int documentCount, out RerankResponse? response)
    {
        response = null;
        if (documentCount <= 0 || root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Array ||
            answers.GetArrayLength() != documentCount)
        {
            return false;
        }

        var results = new RerankResult[documentCount];
        var seen = new bool[documentCount];
        foreach (var answer in answers.EnumerateArray())
        {
            if (answer.ValueKind != JsonValueKind.Object ||
                !answer.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "score" ||
                !TryIndex(answer, documentCount, out var index) || seen[index] ||
                !answer.TryGetProperty("score", out var value) || value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDouble(out var score) || !double.IsFinite(score) || score < 0 || score > Levels.Length - 1)
            {
                return false;
            }

            seen[index] = true;
            results[index] = RerankResult.Create(index, score);
        }

        Array.Sort(results, (left, right) =>
        {
            var byScore = right.RelevanceScore.CompareTo(left.RelevanceScore);
            return byScore != 0 ? byScore : left.Index.CompareTo(right.Index);
        });
        response = new RerankResponse { Results = results };
        return true;
    }

    private static bool TryIndex(JsonElement answer, int count, out int index)
    {
        index = -1;
        if (!answer.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = name.GetString();
        return text is not null && text.StartsWith("rel_", StringComparison.Ordinal) &&
            int.TryParse(text.AsSpan(4), out index) && index >= 0 && index < count &&
            string.Equals(text, $"rel_{index}", StringComparison.Ordinal);
    }
}
