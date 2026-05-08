using System;
using System.Collections.Generic;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using LinkSearch.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LinkSearch.Presenters;

internal sealed class SearchResultPresenter
{
    private readonly SettingsManager _settingsManager;

    public SearchResultPresenter(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
    }

    public IListItem CreateEmptyQueryItem() => new ListItem(new NoOpCommand())
    {
        Title = _settingsManager.Text(LocalizedTextKey.EmptyQueryTitle),
        Icon = Icons.LinkSearchExtIcon,
    };

    public IListItem CreateEmptyResultItem() => new ListItem(new NoOpCommand())
    {
        Title = _settingsManager.Text(LocalizedTextKey.EmptyResultTitle),
        Subtitle = _settingsManager.Text(LocalizedTextKey.EmptyResultSubtitle),
        Icon = Icons.LinkSearchExtIcon,
    };

    public IReadOnlyList<IListItem> CreateErrorItems(LinkwardenSearchError error)
    {
        return new[]
        {
            new ListItem(new NoOpCommand())
            {
                Title = FormatErrorTitle(error),
                Subtitle = _settingsManager.Text(LocalizedTextKey.RetryOrCheckSettings),
                Icon = Icons.LinkSearchExtIcon,
            },
        };
    }

    public IReadOnlyList<IListItem> CreateResultItems(IReadOnlyList<LinkwardenLink>? links)
    {
        var items = new List<IListItem>();
        if (links is null || links.Count == 0)
        {
            items.Add(CreateEmptyResultItem());
            return items;
        }

        var maxResults = _settingsManager.MaxResults;
        foreach (var link in links)
        {
            if (items.Count >= maxResults)
            {
                break;
            }

            if (link is null || string.IsNullOrWhiteSpace(link.Url))
            {
                continue;
            }

            try
            {
                var name = link.Name ?? string.Empty;
                var description = link.Description ?? string.Empty;
                var collectionName = link.Collection ?? string.Empty;
                var linkTags = link.Tags ?? Array.Empty<string>();
                var collection = string.IsNullOrWhiteSpace(collectionName) ? string.Empty : $" [{collectionName}]";
                var tags = linkTags.Count == 0 ? string.Empty : $" #{_settingsManager.Text(LocalizedTextKey.TagLabel)}: {string.Join(", ", linkTags)}";
                items.Add(new ListItem(new OpenUrlCommand(link.Url, _settingsManager))
                {
                    Title = $"{name}{collection}",
                    Subtitle = $"{description}{tags}",
                    Icon = Icons.LinkSearchExtIcon,
                });
            }
            catch (ArgumentException ex)
            {
                Log.Debug($"Skipped invalid URL '{link.Url}': {ex.Message}");
            }
        }

        if (items.Count == 0)
        {
            items.Add(CreateEmptyResultItem());
        }

        return items;
    }

    private string FormatErrorTitle(LinkwardenSearchError error)
    {
        return error.MessageKey switch
        {
            LocalizedTextKey.ApiRequestFailed when error.StatusCode.HasValue => _settingsManager.Format(error.MessageKey, error.StatusCode.Value),
            LocalizedTextKey.ApiCallException when !string.IsNullOrWhiteSpace(error.Detail) => _settingsManager.Format(error.MessageKey, error.Detail),
            _ => _settingsManager.Text(error.MessageKey),
        };
    }
}
