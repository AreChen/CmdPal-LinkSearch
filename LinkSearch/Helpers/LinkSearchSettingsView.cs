using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LinkSearch.Localization;
using LinkSearch.Models;
using LinkSearch.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LinkSearch.Helpers;

internal sealed partial class LinkSearchSettingsView : ICommandSettings, IDisposable
{
    private readonly SettingsManager _manager;
    private readonly LinkSearchSettingsPage _page;

    internal LinkSearchSettingsView(SettingsManager manager)
    {
        _manager = manager;
        _page = new LinkSearchSettingsPage(manager);
        _manager.Settings.SettingsChanged += OnSettingsChanged;
    }

    public IContentPage SettingsPage => _page;

    private void OnSettingsChanged(object? sender, Settings args) => _page.SettingsChanged();

    public void Dispose()
    {
        _manager.Settings.SettingsChanged -= OnSettingsChanged;
        _page.StopConnectionTest();
    }
}

internal sealed partial class LinkSearchSettingsPage : ContentPage
{
    private readonly SettingsManager _manager;
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;
    private readonly object _testLock = new();
    private readonly Func<CancellationToken, Task<RerankConnectionTestResult>> _testConnection;
    private CancellationTokenSource? _testCts;
    private long _testVersion;
    private bool _testing;
    private string? _testMessage;

    internal LinkSearchSettingsPage(SettingsManager manager, Func<CancellationToken, Task<RerankConnectionTestResult>>? testConnection = null)
    {
        _manager = manager;
        _testConnection = testConnection ?? TestConnectionAsync;
        Name = "LinkSearch";
        Icon = new IconInfo("\uE713");
    }

    public override IContent[] GetContent()
    {
        lock (_testLock)
        {
            return [new LinkSearchSettingsForm(_manager, this, _testing, _testMessage)];
        }
    }

    internal void Refresh() => RaiseItemsChanged();

    internal void SettingsChanged()
    {
        StopConnectionTest();
        Refresh();
    }

    internal void StopConnectionTest()
    {
        CancellationTokenSource? previous;
        lock (_testLock)
        {
            _testVersion++;
            previous = _testCts;
            _testCts = null;
            _testing = false;
            _testMessage = null;
        }
        try { previous?.Cancel(); } catch (ObjectDisposedException) { }
    }

    internal void CancelConnectionTest()
    {
        StopConnectionTest();
        lock (_testLock)
        {
            _testMessage = _manager.Text(LocalizedTextKey.RerankConnectionCanceled);
        }
        Refresh();
    }

    internal void StartConnectionTest()
    {
        CancellationTokenSource source;
        long version;
        lock (_testLock)
        {
            if (_testing) return;
            version = ++_testVersion;
            source = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            _testCts = source;
            _testing = true;
            _testMessage = _manager.Text(LocalizedTextKey.SettingsTestingConnection);
        }
        Refresh();
        _ = Task.Run(() => CompleteConnectionTestAsync(version, source));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The existing connection test uses the same serializer as normal ranking.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The existing connection test uses the same serializer as normal ranking.")]
    private async Task<RerankConnectionTestResult> TestConnectionAsync(CancellationToken token)
    {
        using var service = new RerankConnectionTestService(_manager);
        return await service.TestConnectionAsync(token).ConfigureAwait(false);
    }

    private async Task CompleteConnectionTestAsync(long version, CancellationTokenSource source)
    {
        string message;
        try
        {
            var result = await _testConnection(source.Token).ConfigureAwait(false);
            if (source.IsCancellationRequested && result.ErrorType == "Canceled")
            {
                result = RerankConnectionTestResult.CreateFailure("TimeoutError", "Connection test timed out");
            }
            message = result.IsSuccess
                ? _manager.Format(LocalizedTextKey.RerankConnectionSuccess, result.ResponseTimeMs)
                : RerankConnectionMessageFormatter.FormatFailure(result, _manager.CurrentUiLanguage);
        }
        catch (Exception)
        {
            message = _manager.Text(LocalizedTextKey.RankingFailed);
        }
        finally
        {
            source.Dispose();
        }

        lock (_testLock)
        {
            if (version != _testVersion) return;
            _testCts = null;
            _testing = false;
            _testMessage = message;
        }
        if (_syncContext is null) Refresh();
        else _syncContext.Post(_ => Refresh(), null);
    }
}

internal sealed partial class LinkSearchSettingsForm : FormContent
{
    private readonly SettingsManager _manager;
    private readonly LinkSearchSettingsPage? _page;
    private readonly FormContent _settingsForm;
    private readonly string _baseTemplate;
    private readonly JsonObject _draft = new();
    private bool _enabled;
    private RankingMode _mode;
    private DecisionProvider _provider;
    private readonly bool _testing;
    private readonly string? _testMessage;

    internal LinkSearchSettingsForm(SettingsManager manager, LinkSearchSettingsPage? page = null, bool testing = false, string? testMessage = null)
    {
        _manager = manager;
        _page = page;
        _testing = testing;
        _testMessage = testMessage;
        _settingsForm = (FormContent)manager.Settings.ToContent()[0];
        _baseTemplate = _settingsForm.TemplateJson;
        DataJson = _settingsForm.DataJson;
        StateJson = _settingsForm.StateJson;
        _enabled = manager.EnableRerank;
        _mode = manager.RankingMode;
        _provider = manager.DecisionProvider;
        Render();
    }

    public override ICommandResult SubmitForm(string inputs, string data)
    {
        JsonObject? submitted;
        JsonObject? action;
        try
        {
            submitted = JsonNode.Parse(inputs)?.AsObject();
            action = string.IsNullOrWhiteSpace(data) ? null : JsonNode.Parse(data)?.AsObject();
        }
        catch (JsonException)
        {
            return CommandResult.KeepOpen();
        }
        catch (InvalidOperationException)
        {
            return CommandResult.KeepOpen();
        }

        if (submitted is null)
        {
            return CommandResult.KeepOpen();
        }

        foreach (var pair in submitted)
        {
            if (pair.Key.StartsWith("linkSearch.", StringComparison.Ordinal) && pair.Value is not null)
            {
                _draft[pair.Key] = pair.Value.DeepClone();
            }
        }

        var operation = action?["linksearchAction"]?.GetValue<string>();
        var value = action?["value"]?.GetValue<string>();
        switch (operation)
        {
            case "cancel-test":
                _page?.CancelConnectionTest();
                return CommandResult.KeepOpen();
            case "toggle":
                _enabled = !_enabled;
                break;
            case "mode":
                _mode = value switch
                {
                    "decision-noul" => RankingMode.DecisionNoul,
                    "decision-choice" => RankingMode.DecisionChoice,
                    _ => RankingMode.Rerank,
                };
                break;
            case "provider":
                _provider = value switch
                {
                    "openrouter" => DecisionProvider.OpenRouter,
                    "openai" => DecisionProvider.OpenAi,
                    _ => DecisionProvider.SystemOne,
                };
                break;
            case "backend":
                if (value == "rerank")
                {
                    _mode = RankingMode.Rerank;
                }
                else
                {
                    if (_mode == RankingMode.Rerank)
                    {
                        _mode = RankingMode.DecisionNoul;
                    }
                    _provider = value switch
                    {
                        "openrouter" => DecisionProvider.OpenRouter,
                        "openai" => DecisionProvider.OpenAi,
                        _ => DecisionProvider.SystemOne,
                    };
                }
                break;
            case null:
            case "save":
            case "test":
                _draft["linkSearch.EnableRerank"] = _enabled ? "true" : "false";
                _draft["linkSearch.RankingEnabledExplicitlySet"] = "true";
                _draft["linkSearch.RankingMode"] = ModeValue(_mode);
                _draft["linkSearch.DecisionProvider"] = ProviderValue(_provider);
                var result = _settingsForm.SubmitForm(_draft.ToJsonString(), "{}");
                if (operation == "test")
                {
                    if (_enabled) _page?.StartConnectionTest();
                    return CommandResult.KeepOpen();
                }
                return result;
            default:
                return CommandResult.KeepOpen();
        }

        Render();
        return CommandResult.KeepOpen();
    }

    private void Render() => TemplateJson = SettingsLayout.Organize(_baseTemplate, _manager, _draft, _enabled, _mode, _provider, _testing, _testMessage);

    internal static string ModeValue(RankingMode mode) => mode switch
    {
        RankingMode.DecisionNoul => "decision-noul",
        RankingMode.DecisionChoice => "decision-choice",
        _ => "rerank",
    };

    internal static string ProviderValue(DecisionProvider provider) => provider switch
    {
        DecisionProvider.OpenRouter => "openrouter",
        DecisionProvider.OpenAi => "openai",
        _ => "system-one",
    };
}

internal static class SettingsLayout
{
    private static readonly HashSet<string> ManagedFields = new(StringComparer.Ordinal)
    {
        "linkSearch.EnableRerank", "linkSearch.RankingEnabledExplicitlySet", "linkSearch.RankingMode", "linkSearch.DecisionProvider",
        "linkSearch.RerankApiUrl", "linkSearch.RerankApiKey", "linkSearch.RerankModelName",
        "linkSearch.DecisionApiUrl", "linkSearch.DecisionApiKey", "linkSearch.DecisionModelName",
        "linkSearch.OpenRouterApiUrl", "linkSearch.OpenRouterApiKey", "linkSearch.OpenRouterModelName",
        "linkSearch.OpenAiApiUrl", "linkSearch.OpenAiApiKey", "linkSearch.OpenAiModelName",
        "linkSearch.RankingCandidateLimit",
    };

    internal static string Organize(string template, SettingsManager manager, JsonObject draft, bool enabled, RankingMode mode, DecisionProvider provider, bool testing = false, string? testMessage = null)
    {
        var card = JsonNode.Parse(template)!.AsObject();
        if (testing)
        {
            var waitingBody = new JsonArray();
            AddHeading(waitingBody, manager.Text(LocalizedTextKey.SettingsRankingSection), false);
            AddLabel(waitingBody, manager.Text(LocalizedTextKey.SettingsTestingConnection));
            waitingBody.Add((JsonNode)ActionRow(Action(manager.Text(LocalizedTextKey.SettingsCancelTest), "cancel-test")));
            card["body"] = waitingBody;
            card["actions"] = new JsonArray();
            return card.ToJsonString();
        }
        var original = card["body"]!.AsArray();
        var fields = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        while (original.Count > 0)
        {
            var item = original[0]!;
            original.RemoveAt(0);
            var id = FindId(item);
            if (id is null)
            {
                continue;
            }

            if (item["type"]?.GetValue<string>() is "Input.Text" or "Input.ChoiceSet")
            {
                item["label"] = item["title"]?.GetValue<string>();
                if (id.EndsWith("ApiKey", StringComparison.Ordinal))
                {
                    item["style"] = "password";
                }
            }

            ApplyDraft(item, draft);
            fields.Add(id, item);
        }

        var body = new JsonArray();
        AddHeading(body, manager.Text(LocalizedTextKey.SettingsConnectionSection), false);
        AddFields(body, fields, "LinkwardenBaseUrl", "LinkwardenApiKey");
        AddHeading(body, manager.Text(LocalizedTextKey.SettingsRankingSection), true);
        body.Add((JsonNode)ActionRow(Action(manager.Text(enabled ? LocalizedTextKey.SettingsDisableRanking : LocalizedTextKey.SettingsEnableRanking), "toggle")));

        if (enabled)
        {
            AddLabel(body, manager.Text(LocalizedTextKey.SettingsRankingService));
            body.Add((JsonNode)ActionRow(
                Action(manager.Text(LocalizedTextKey.SettingsModeRerank), "backend", "rerank", mode == RankingMode.Rerank),
                Action("OpenRouter", "backend", "openrouter", mode != RankingMode.Rerank && provider == DecisionProvider.OpenRouter),
                Action("OpenAI", "backend", "openai", mode != RankingMode.Rerank && provider == DecisionProvider.OpenAi),
                Action(manager.Text(LocalizedTextKey.SettingsProviderCustom), "backend", "system-one", mode != RankingMode.Rerank && provider == DecisionProvider.SystemOne)));

            if (mode == RankingMode.Rerank)
            {
                AddFields(body, fields, "RerankApiUrl", "RerankApiKey", "RerankModelName");
            }
            else
            {
                if (provider == DecisionProvider.OpenAi)
                {
                    AddLabel(body, manager.Text(LocalizedTextKey.SettingsScoreMode));
                }
                else
                {
                    AddLabel(body, manager.Text(LocalizedTextKey.RankingModeLabel));
                    body.Add((JsonNode)ActionRow(
                        Action(manager.Text(LocalizedTextKey.SettingsModeNoul), "mode", "decision-noul", mode == RankingMode.DecisionNoul),
                        Action(manager.Text(LocalizedTextKey.SettingsModeChoice), "mode", "decision-choice", mode == RankingMode.DecisionChoice)));
                }

                switch (provider)
                {
                    case DecisionProvider.OpenRouter:
                        AddGuides(body, manager, "https://openrouter.ai/docs/quickstart", "https://openrouter.ai/docs/api/api-reference/alphadecisions/submit-a-decisions-request");
                        AddFields(body, fields, "OpenRouterApiUrl", "OpenRouterApiKey", "OpenRouterModelName");
                        break;
                    case DecisionProvider.OpenAi:
                        AddGuides(body, manager, "https://developers.openai.com/api/docs/quickstart", "https://developers.openai.com/api/docs/guides/decisions");
                        AddFields(body, fields, "OpenAiApiUrl", "OpenAiApiKey", "OpenAiModelName");
                        break;
                    default:
                        AddFields(body, fields, "DecisionApiUrl", "DecisionApiKey", "DecisionModelName");
                        break;
                }
            }

            AddFields(body, fields, "RankingCandidateLimit");
            AddLabel(body, manager.Text(LocalizedTextKey.SettingsRankingPrivacy));
            body.Add((JsonNode)ActionRow(Action(manager.Text(LocalizedTextKey.SettingsTestConnection), "test")));
            if (!string.IsNullOrEmpty(testMessage)) AddLabel(body, testMessage);
        }

        AddHeading(body, manager.Text(LocalizedTextKey.SettingsSearchSection), true);
        AddFields(body, fields, "SearchDelayMilliseconds", "MaxResults", "LanguageMode");
        foreach (var field in fields)
        {
            if (!ManagedFields.Contains(field.Key))
            {
                body.Add(field.Value);
            }
        }

        card["body"] = body;
        card["actions"]![0]!["title"] = manager.Text(LocalizedTextKey.SettingsSave);
        card["actions"]![0]!["data"] = new JsonObject { ["linksearchAction"] = "save" };
        return card.ToJsonString();
    }

    private static void ApplyDraft(JsonNode node, JsonObject draft)
    {
        if (node is JsonObject obj)
        {
            if (obj["id"] is JsonValue id && id.TryGetValue<string>(out var key) && draft[key] is { } value)
            {
                obj["value"] = value.DeepClone();
            }

            foreach (var pair in obj)
            {
                if (pair.Value is not null)
                {
                    ApplyDraft(pair.Value, draft);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    ApplyDraft(item, draft);
                }
            }
        }
    }

    private static string? FindId(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj["id"] is JsonValue value && value.TryGetValue<string>(out var id))
            {
                return id;
            }

            foreach (var pair in obj)
            {
                if (pair.Value is not null && FindId(pair.Value) is { } nested)
                {
                    return nested;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null && FindId(item) is { } nested)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static void AddHeading(JsonArray body, string title, bool separator) => body.Add((JsonNode)new JsonObject
    {
        ["type"] = "TextBlock", ["text"] = title, ["weight"] = "Bolder", ["size"] = "Medium",
        ["wrap"] = true, ["separator"] = separator, ["spacing"] = separator ? "Large" : "None",
    });

    private static void AddLabel(JsonArray body, string title) => body.Add((JsonNode)new JsonObject
    {
        ["type"] = "TextBlock", ["text"] = title, ["wrap"] = true, ["spacing"] = "Medium",
    });

    private static void AddFields(JsonArray target, Dictionary<string, JsonNode> fields, params string[] names)
    {
        foreach (var name in names)
        {
            var key = $"linkSearch.{name}";
            if (fields.Remove(key, out var field))
            {
                target.Add(field);
            }
        }
    }

    private static JsonObject ActionRow(params JsonObject[] actions)
    {
        var array = new JsonArray();
        foreach (var action in actions)
        {
            array.Add((JsonNode)action);
        }

        return new JsonObject { ["type"] = "ActionSet", ["actions"] = array };
    }

    private static JsonObject Action(string title, string operation, string? value = null, bool selected = false) => new()
    {
        ["type"] = "Action.Submit", ["title"] = selected ? $"\u2713 {title}" : title,
        ["style"] = "default",
        ["data"] = new JsonObject { ["linksearchAction"] = operation, ["value"] = value },
    };

    private static void AddGuides(JsonArray body, SettingsManager manager, string setupUrl, string apiUrl) => body.Add((JsonNode)ActionRow(
        new JsonObject { ["type"] = "Action.OpenUrl", ["title"] = manager.Text(LocalizedTextKey.SettingsSetupGuide), ["url"] = setupUrl },
        new JsonObject { ["type"] = "Action.OpenUrl", ["title"] = manager.Text(LocalizedTextKey.SettingsDecisionGuide), ["url"] = apiUrl }));
}
