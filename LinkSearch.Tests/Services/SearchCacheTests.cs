using System;
using LinkSearch.Models;
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class SearchCacheTests
{
    [Fact]
    public void TryGet_returns_cached_links_before_ttl_expires()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);

        Assert.True(cache.TryGet("https://cloud.linkwarden.app", "docs", out var cached));
        Assert.Same(links, cached);
    }

    [Fact]
    public void TryGet_expires_after_ttl()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);
        now.Value = now.Value.AddMinutes(6);

        Assert.False(cache.TryGet("https://cloud.linkwarden.app", "docs", out _));
    }

    [Fact]
    public void TryGet_returns_cached_links_at_exact_ttl()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);
        now.Value = now.Value.Add(TimeSpan.FromMinutes(5));

        Assert.True(cache.TryGet("https://cloud.linkwarden.app", "docs", out var cached));
        Assert.Same(links, cached);
    }

    [Fact]
    public void TryGet_expires_just_after_ttl()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);
        now.Value = now.Value.Add(TimeSpan.FromMinutes(5)).AddTicks(1);

        Assert.False(cache.TryGet("https://cloud.linkwarden.app", "docs", out _));
    }

    [Fact]
    public void TryGet_normalizes_base_url_and_query_key_parts()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set(" https://cloud.linkwarden.app/ ", " Docs ", links);

        Assert.True(cache.TryGet("HTTPS://CLOUD.LINKWARDEN.APP", "docs", out var cached));
        Assert.Same(links, cached);
    }

    [Fact]
    public void TryGet_treats_null_key_parts_like_empty_strings()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set(null, null, links);

        Assert.True(cache.TryGet(string.Empty, string.Empty, out var cached));
        Assert.Same(links, cached);
    }

    [Fact]
    public void Clear_removes_cached_links()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);
        cache.Clear();

        Assert.False(cache.TryGet("https://cloud.linkwarden.app", "docs", out _));
    }

    private sealed class MutableClock
    {
        public MutableClock(DateTimeOffset value)
        {
            Value = value;
        }

        public DateTimeOffset Value { get; set; }

        public DateTimeOffset UtcNow() => Value;
    }
}
