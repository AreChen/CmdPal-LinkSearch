// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using LinkSearch;
using LinkSearch.Helpers;
using LinkSearch.Services;

namespace LinkSearch;

public partial class LinkSearchCommandsProvider : CommandProvider, IDisposable
{
    private readonly ICommandItem[] _commands;
    private static readonly SettingsManager _settingsManager = new();
    private readonly RerankService _rerankService;
    private readonly RerankConnectionTestService _rerankConnectionTestService;
    // 统一的释放取消源：在 Provider.Dispose 时取消，尽可能终止后台任务，降低宿主长时运行风险
    private readonly CancellationTokenSource _disposeCts = new();
    // 持有 Page 引用以便在 Provider.Dispose 时显式释放
    private readonly LinkSearchPage _page;
    private bool _disposed;

    public LinkSearchCommandsProvider()
    {
        DisplayName = "LinkSearch";
        Icon = Icons.LinkSearchExtIcon;
        Settings = _settingsManager.Settings;
        
        // 创建服务实例
        _rerankService = new RerankService(_settingsManager);
        _rerankConnectionTestService = new RerankConnectionTestService(_settingsManager);
        
        // 构建页面并保存实例，便于统一释放
        _page = new LinkSearchPage(_settingsManager, _rerankService, _rerankConnectionTestService);

        _commands = [
            new CommandItem(_page) {
                Title = DisplayName,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    /// <summary>
    /// Provider 释放：取消内部 CTS，并释放 Page 与 Service
    /// </summary>
    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // 取消所有可能关联的后台操作
        try { _disposeCts.Cancel(); } catch { }

        // 依次释放托管对象，逐一捕获异常避免影响整体释放流程
        try { _page?.Dispose(); }
        catch (Exception ex) { Log.Error($"LinkSearchCommandsProvider.Dispose Page 释放异常: {ex.Message}"); }

        try { _rerankService?.Dispose(); }
        catch (Exception ex) { Log.Error($"LinkSearchCommandsProvider.Dispose RerankService 释放异常: {ex.Message}"); }

        try { _rerankConnectionTestService?.Dispose(); }
        catch (Exception ex) { Log.Error($"LinkSearchCommandsProvider.Dispose RerankConnectionTestService 释放异常: {ex.Message}"); }

        try { _disposeCts.Dispose(); } catch { }
        
        GC.SuppressFinalize(this);
    }

}
