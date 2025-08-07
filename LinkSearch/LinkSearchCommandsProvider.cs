// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using LinkSearch;
using LinkSearch.Helpers;
using LinkSearch.Services;

namespace LinkSearch;

public partial class LinkSearchCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private static readonly SettingsManager _settingsManager = new();
    private readonly RerankService _rerankService;
    private readonly RerankConnectionTestService _rerankConnectionTestService;

    public LinkSearchCommandsProvider()
    {
        DisplayName = "LinkSearch";
        Icon = Icons.LinkSearchExtIcon;
        Settings = _settingsManager.Settings;
        
        // 创建服务实例
        _rerankService = new RerankService(_settingsManager);
        _rerankConnectionTestService = new RerankConnectionTestService(_settingsManager);
        
        _commands = [
            new CommandItem(new LinkSearchPage(_settingsManager, _rerankService, _rerankConnectionTestService)) {
                Title = DisplayName,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
