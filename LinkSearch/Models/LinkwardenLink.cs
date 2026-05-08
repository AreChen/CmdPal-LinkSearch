using System;
using System.Collections.Generic;

namespace LinkSearch.Models;

internal sealed record LinkwardenLink(
    string Name,
    string Description,
    string Url,
    string Collection,
    IReadOnlyList<string> Tags)
{
    public static LinkwardenLink Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>());
}
