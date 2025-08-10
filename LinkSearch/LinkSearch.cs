// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using LinkSearch.Helpers;

namespace LinkSearch;

[Guid("1723955f-282a-4aa0-8a38-2b46f9b775a0")]
public sealed partial class LinkSearch : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly LinkSearchCommandsProvider _provider = new();

    public LinkSearch(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        try
        {
            // 释放 Provider 资源，确保后台任务被取消，避免宿主长时运行崩溃
            (_provider as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error($"LinkSearch.Dispose 释放 Provider 异常: {ex.Message}");
        }
        finally
        {
            // 无论释放是否成功，都设置事件以通知宿主
            this._extensionDisposedEvent.Set();
        }
    }
}
