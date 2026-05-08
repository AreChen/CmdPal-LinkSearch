using System;
using System.Collections.Generic;

namespace LinkSearch.Models;

internal sealed record LinkwardenSearchResult(IReadOnlyList<LinkwardenLink> Links, LinkwardenSearchError? Error)
{
    public static LinkwardenSearchResult Success(IReadOnlyList<LinkwardenLink> links) => new(links, null);

    public static LinkwardenSearchResult Failure(LinkwardenSearchError error) => new(Array.Empty<LinkwardenLink>(), error);
}
