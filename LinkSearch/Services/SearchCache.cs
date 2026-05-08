using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal sealed class SearchCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _utcNow;

    public SearchCache(TimeSpan? ttl = null, Func<DateTimeOffset>? utcNow = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public bool TryGet(string? baseUrl, string? query, out IReadOnlyList<LinkwardenLink> links)
    {
        var key = CreateKey(baseUrl, query);
        if (_entries.TryGetValue(key, out var entry) && _utcNow() - entry.CreatedAt <= _ttl)
        {
            links = entry.Links;
            return true;
        }

        if (entry is not null)
        {
            ((ICollection<KeyValuePair<string, CacheEntry>>)_entries)
                .Remove(new KeyValuePair<string, CacheEntry>(key, entry));
        }

        links = Array.Empty<LinkwardenLink>();
        return false;
    }

    public void Set(string? baseUrl, string? query, IReadOnlyList<LinkwardenLink> links)
    {
        _entries[CreateKey(baseUrl, query)] = new CacheEntry(_utcNow(), links);
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private static string CreateKey(string? baseUrl, string? query)
    {
        return string.Concat(
            (baseUrl ?? string.Empty).Trim().TrimEnd('/').ToUpperInvariant(),
            "\n",
            (query ?? string.Empty).Trim().ToUpperInvariant());
    }

    private sealed record CacheEntry(DateTimeOffset CreatedAt, IReadOnlyList<LinkwardenLink> Links);
}
