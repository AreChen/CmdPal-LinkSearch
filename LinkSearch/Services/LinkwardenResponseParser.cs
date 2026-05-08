using System.Collections.Generic;
using System.Text.Json;
using LinkSearch.Localization;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal static class LinkwardenResponseParser
{
    public static LinkwardenSearchResult Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    public static LinkwardenSearchResult Parse(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataElement))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.MissingDataNode));
        }

        JsonElement linksElement;
        if (dataElement.ValueKind == JsonValueKind.Object && dataElement.TryGetProperty("links", out linksElement))
        {
        }
        else if (dataElement.ValueKind == JsonValueKind.Array)
        {
            linksElement = dataElement;
        }
        else
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.InvalidDataNode));
        }

        if (linksElement.ValueKind != JsonValueKind.Array)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.LinksNotArray));
        }

        var links = new List<LinkwardenLink>();
        foreach (var link in linksElement.EnumerateArray())
        {
            if (link.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = ReadString(link, "name");
            var description = ReadString(link, "description");
            var url = ReadString(link, "url");
            var collection = string.Empty;

            if (link.TryGetProperty("collection", out var collectionElement) && collectionElement.ValueKind == JsonValueKind.Object)
            {
                collection = ReadString(collectionElement, "name");
            }

            var tags = new List<string>();
            if (link.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsElement.EnumerateArray())
                {
                    if (tag.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var tagName = ReadString(tag, "name");
                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        tags.Add(tagName);
                    }
                }
            }

            links.Add(new LinkwardenLink(name, description, url, collection, tags));
        }

        return LinkwardenSearchResult.Success(links);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }
}
