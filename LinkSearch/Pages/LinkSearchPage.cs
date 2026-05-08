// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using LinkSearch.Models;
using LinkSearch.Presenters;
using LinkSearch.Services;
using System.Diagnostics.CodeAnalysis;



namespace LinkSearch
{
    // 打开链接命令
    internal sealed partial class OpenUrlCommand : InvokableCommand
    {
        private readonly string _url;
        private readonly SettingsManager? _settingsManager;
        private static readonly HashSet<string> AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http",
            "https",
            "mailto",
            "ftp",
            "file"
        };

        public OpenUrlCommand(string url) : this(url, null, false)
        {
        }

        public OpenUrlCommand(string url, SettingsManager settingsManager) : this(url, settingsManager, false)
        {
        }

        private OpenUrlCommand(string url, SettingsManager? settingsManager, bool triggerPropertyChange)
        {
            // URL校验
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL不能为空", nameof(url));
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !AllowedSchemes.Contains(uri.Scheme))
            {
                throw new ArgumentException($"无效的URL: {url}", nameof(url));
            }

            _url = url;
            _settingsManager = settingsManager;

            if (triggerPropertyChange)
            {
                OnPropertyChanged(nameof(Id));
            }
        }
            

        public override string Id => _url;
        public override string Name => _settingsManager?.Text(LocalizedTextKey.OpenLinkCommandName) ?? LocalizedStrings.Get(
            LocalizedTextKey.OpenLinkCommandName,
            LocalizedStrings.ResolveUiLanguage(LanguageMode.Auto, CultureInfo.CurrentUICulture));
        public override IconInfo Icon => Icons.LinkSearchExtIcon;


        // 实现 SDK 要求的 PropChanged 事件 (WinRT 风格)
        // 使用 new 关键字隐藏基类事件，因为基类事件不是 virtual 的
        public new event Windows.Foundation.TypedEventHandler<object, Microsoft.CommandPalette.Extensions.IPropChangedEventArgs>? PropChanged;

        // 保护方法用于触发属性变更事件
        private new void OnPropertyChanged(string propertyName)
        {
            PropChanged?.Invoke(this, new Microsoft.CommandPalette.Extensions.Toolkit.PropChangedEventArgs(propertyName));
        }

        // 在构造函数中触发属性变更事件，确保事件被使用
        public OpenUrlCommand(string url, bool triggerPropertyChange = false) : this(url, null, triggerPropertyChange)
        {
        }

        public override CommandResult Invoke()
        {
            try
            {
                if (Uri.TryCreate(_url, UriKind.Absolute, out var uri))
                {
                    // Fire-and-forget 启动，不阻塞调用线程，避免 UI 卡顿或死锁
                    _ = Windows.System.Launcher.LaunchUriAsync(uri).AsTask().ContinueWith(t =>
                    {
                        try
                        {
                            if (!t.Result)
                            {
                                Log.Debug($"无法打开链接: {_url}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug($"打开链接时发生异常: {ex.Message}");
                        }
                    }, TaskScheduler.Default);
                }
                else
                {
                    Log.Debug($"无效的URL: {_url}");
                    return CommandResult.KeepOpen();
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"打开链接时发生异常: {ex.Message}");
                Log.Debug($"URL: {_url}");
            }
            return CommandResult.Dismiss();
        }
    }

    internal sealed partial class LinkSearchPage : DynamicListPage, System.IDisposable
    {
        private List<IListItem> _allItems = new List<IListItem>();
        private string _currentQuery = string.Empty;
        private long _currentQueryVersion; // 查询版本号，用于验证查询有效性
        private long _activeSearchVersion; // 标记搜索正在进行中
        private readonly SettingsManager _settingsManager;
        private readonly LinkwardenService _linkwardenService;
        private readonly RerankService _rerankService;
        private readonly RerankConnectionTestService _rerankConnectionTestService;
        private readonly SearchResultPresenter _presenter;
        private System.Threading.CancellationTokenSource? _searchCancellationTokenSource;
        // 搜索延迟时间（毫秒）- 现在从设置中获取
        private int SearchDelayMilliseconds => _settingsManager.SearchDelayMilliseconds;
        private string _lastErrorMessage = string.Empty;
        private DateTime _lastErrorTime = DateTime.MinValue;
        private readonly System.Threading.SemaphoreSlim _searchSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        // 捕获 UI 同步上下文用于跨线程安全更新 Items（避免后台线程调用 RaiseItemsChanged 导致崩溃/快捷键异常）
        private readonly SynchronizationContext? _syncContext;
        public LinkSearchPage(
            SettingsManager settingsManager,
            LinkwardenService linkwardenService,
            RerankService rerankService,
            RerankConnectionTestService rerankConnectionTestService,
            SearchResultPresenter presenter)
        {
            // 使用传入的服务
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _linkwardenService = linkwardenService ?? throw new ArgumentNullException(nameof(linkwardenService));
            _rerankService = rerankService ?? throw new ArgumentNullException(nameof(rerankService));
            _rerankConnectionTestService = rerankConnectionTestService ?? throw new ArgumentNullException(nameof(rerankConnectionTestService));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));

            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
            Title = "LinkSearch";
            Name = _settingsManager.Text(LocalizedTextKey.PageName);
            PlaceholderText = _settingsManager.Text(LocalizedTextKey.SearchPlaceholder);
            EmptyContent = _presenter.CreateEmptyResultItem();
            
            // 订阅设置变更事件
            _settingsManager.Settings.SettingsChanged += OnSettingsChanged;

            // 捕获当前同步上下文（UI线程），用于安全触发 RaiseItemsChanged
            try
            {
                _syncContext = SynchronizationContext.Current;
                Log.Info($"LinkSearchPage 捕获到 SynchronizationContext: {(_syncContext != null)}");
            }
            catch (Exception ex)
            {
                Log.Error($"捕获 SynchronizationContext 失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 设置变更事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnSettingsChanged(object? sender, Microsoft.CommandPalette.Extensions.Toolkit.Settings e)
        {
#if DEBUG
            Log.Debug("设置发生变更，重新加载当前搜索结果");
#endif
            Name = _settingsManager.Text(LocalizedTextKey.PageName);
            PlaceholderText = _settingsManager.Text(LocalizedTextKey.SearchPlaceholder);
            EmptyContent = _presenter.CreateEmptyResultItem();
            
            if (string.IsNullOrWhiteSpace(_currentQuery))
            {
                _allItems = new List<IListItem> { _presenter.CreateEmptyQueryItem() };
                RaiseItemsChangedOnUiThread();
            }
            else
            {
                // 取消当前的延迟搜索，然后重新开始
                var t = DebouncedUpdateItemsAsync(_currentQuery, _currentQueryVersion);
                t.ContinueWith(tt =>
                {
                    if (tt.IsFaulted)
                    {
                        Log.Error($"DebouncedUpdateItemsAsync 未观察到的异常: {tt.Exception?.Flatten().Message}");
                    }
                }, TaskScheduler.Default);
            }
        }
        
        /// <summary>
        /// 根据延迟时间获取信号量超时时间
        /// </summary>
        /// <returns>信号量超时时间（毫秒）</returns>
        private int GetSemaphoreTimeout()
        {
            // 将信号量超时时间设置为延迟时间的2-3倍
            // 确保在延迟小于600ms时不会触发重复检索
            // 最小超时时间从1000ms增加到1500ms，以适应新的最小延迟时间300ms
            return Math.Max(SearchDelayMilliseconds * 3, 1500);
        }
        
        /// <summary>
        /// 测试Rerank连接
        /// </summary>
        /// <returns>测试结果</returns>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("IL2026", "TestConnectionAsync 方法可能需要未引用的代码")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("IL3050", "TestConnectionAsync 方法可能需要动态生成的代码")]
        [RequiresUnreferencedCode("Calls LinkSearch.Services.RerankConnectionTestService.TestConnectionAsync()")]
        [RequiresDynamicCode("Calls LinkSearch.Services.RerankConnectionTestService.TestConnectionAsync()")]
        public async Task<string> TestRerankConnectionAsync()
        {
#if DEBUG
            Log.Debug("开始测试Rerank连接");
#endif
            
            try
            {
                var testResult = await _rerankConnectionTestService.TestConnectionAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
                
                if (testResult.IsSuccess)
                {
                    return _settingsManager.Format(LocalizedTextKey.RerankConnectionSuccess, testResult.ResponseTimeMs);
                }
                else
                {
                    return RerankConnectionMessageFormatter.FormatFailure(testResult, _settingsManager.CurrentUiLanguage);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Log.Debug($"测试Rerank连接时发生异常: {ex.Message}");
#endif
                return _settingsManager.Format(LocalizedTextKey.RerankConnectionException, ex.Message);
            }
        }
        
        public override void UpdateSearchText(string oldSearch, string newSearch)
        {
            // 立即更新当前查询和版本号
            _currentQuery = newSearch;
            var queryVersion = Interlocked.Increment(ref _currentQueryVersion);
        
            // 直接以 fire-and-forget 的方式调用异步方法，避免额外将工作项排到线程池导致短时大量 TP worker 创建
            if (string.IsNullOrWhiteSpace(newSearch))
            {
                CancelCurrentSearch();
                // 对于空查询，直接更新UI而不使用信号量（保留原先行为）
                var t0 = UpdateItemsAsync(newSearch, queryVersion, System.Threading.CancellationToken.None);
                t0.ContinueWith(tt =>
                {
                    if (tt.IsFaulted)
                    {
                        Log.Error($"UpdateItemsAsync 未观察到的异常: {tt.Exception?.Flatten().Message}");
                    }
                }, TaskScheduler.Default);
                return;
            }
        
            // 直接调用 DebouncedUpdateItemsAsync（异步方法会在必要时释放线程）
            var t1 = DebouncedUpdateItemsAsync(newSearch, queryVersion);
            t1.ContinueWith(tt =>
            {
                if (tt.IsFaulted)
                {
                    Log.Error($"DebouncedUpdateItemsAsync 未观察到的异常: {tt.Exception?.Flatten().Message}");
                }
            }, TaskScheduler.Default);
        }
        
        private void CancelCurrentSearch()
        {
            var ctsToCancel = Interlocked.Exchange(ref _searchCancellationTokenSource, null);
            if (ctsToCancel == null)
            {
                return;
            }

            try
            {
                if (!ctsToCancel.IsCancellationRequested)
                {
                    ctsToCancel.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                ctsToCancel.Dispose();
            }
        }

        private void RaiseItemsChangedOnUiThread()
        {
            try
            {
                if (_syncContext != null)
                {
                    _syncContext.Post(_ =>
                    {
                        try { RaiseItemsChanged(0); }
                        catch (Exception ex2) { Log.Error($"RaiseItemsChanged 调用失败: {ex2.Message}"); }
                    }, null);
                }
                else
                {
                    // 无法获取到 UI 同步上下文时退化为直接调用（记录日志以便后续审计）
                    RaiseItemsChanged(0);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"在发布 RaiseItemsChanged 时发生异常: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task UpdateItemsAsync(string query, long queryVersion, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                // 清除之前的错误信息
                _lastErrorMessage = string.Empty;
                var items = await GetItemsAsync(query, cancellationToken).ConfigureAwait(false);
                if (queryVersion != Interlocked.Read(ref _currentQueryVersion))
                {
                    return;
                }

                _allItems = items;
                // 确保在 UI 同步上下文中触发 RaiseItemsChanged，避免跨线程更新导致崩溃/快捷键失效
                RaiseItemsChangedOnUiThread();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (queryVersion != Interlocked.Read(ref _currentQueryVersion))
                {
                    return;
                }

                // 记录错误信息
                _lastErrorMessage = $"搜索失败: {ex.Message}";
                _lastErrorTime = DateTime.Now;
                
#if DEBUG
                Log.Debug($"UpdateItemsAsync 中发生异常: {ex.Message}");
                Log.Debug($"异常类型: {ex.GetType().Name}");
                Log.Debug($"堆栈跟踪: {ex.StackTrace}");
#endif
                
                // 显示错误信息
                _allItems = new List<IListItem>
                {
                    new ListItem(new NoOpCommand())
                    {
                        Title = _lastErrorMessage,
                        Subtitle = "请稍后重试或检查设置",
                        Icon = Icons.LinkSearchExtIcon
                    }
                };
                // 确保在 UI 同步上下文中触发 RaiseItemsChanged，避免跨线程问题
                RaiseItemsChangedOnUiThread();

            }
        }
        
        /// <summary>
        /// 延迟搜索方法，实现防抖功能
        /// </summary>
        /// <param name="query">搜索查询</param>
        /// <param name="queryVersion">查询版本号</param>
        /// <returns>任务</returns>
        private async System.Threading.Tasks.Task DebouncedUpdateItemsAsync(string query, long queryVersion)
        {
            // 查询版本预验证：在创建CancellationTokenSource之前先验证查询版本
            if (queryVersion != _currentQueryVersion)
            {
#if DEBUG
                Log.Debug($"查询预验证失败，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                return; // 查询已过期，取消搜索
            }
            
            // 创建新的CancellationTokenSource，使用更安全的管理方式
            var localCancellationTokenSource = new System.Threading.CancellationTokenSource();
            
            // 使用原子操作设置新的CancellationTokenSource，并获取之前的
            var previousCts = System.Threading.Interlocked.Exchange(ref _searchCancellationTokenSource, localCancellationTokenSource);
            
            // 安全地取消之前的搜索任务（如果存在）
            if (previousCts != null)
            {
                try
                {
                    // 只有在之前的CancellationTokenSource未被取消时才取消
                    if (!previousCts.IsCancellationRequested)
                    {
                        previousCts.Cancel();
                    }
                    previousCts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // 对象已被释放，忽略异常
                }
            }
            
            bool semaphoreAcquired = false;
            
            try
            {
#if DEBUG
                Log.Debug($"开始延迟搜索，查询: {query}, 版本: {queryVersion}");
#endif
                
                // 在开始延迟前获取延迟时间，避免在延迟过程中访问属性导致异常
                int delayMs;
                try
                {
                    delayMs = SearchDelayMilliseconds;
#if DEBUG
                    Log.Debug($"获取到延迟时间: {delayMs}ms");
#endif
                }
                catch (Exception)
                {
#if DEBUG
                    Log.Debug($"获取延迟时间时发生异常");
                    Log.Debug($"使用默认延迟时间: 600ms");
#endif
                    delayMs = 600; // 使用默认值，从500ms增加到600ms
                }
                
                // 再次验证查询版本，确保在获取延迟时间期间查询没有变化
                if (queryVersion != _currentQueryVersion)
                {
#if DEBUG
                    Log.Debug($"获取延迟时间后查询验证失败，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                    return; // 查询已过期，取消搜索
                }
                
                // 使用更安全的延迟方式，避免在Task.Delay执行过程中意外取消任务
                try
                {
                    // 创建一个链接的CancellationToken，结合本地取消令牌和全局取消令牌
                    using var linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                        localCancellationTokenSource.Token);
                    
                    // 等待指定的延迟时间
                    await System.Threading.Tasks.Task.Delay(delayMs, linkedCts.Token);
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
#if DEBUG
                    Log.Debug($"延迟期间任务被取消，查询: {query}, 版本: {queryVersion}");
#endif
                    return; // 延迟期间被取消，直接返回
                }
                
#if DEBUG
                Log.Debug($"延迟结束，检查查询有效性");
#endif
                // 延迟后再次验证查询版本是否仍然有效（查询有效性验证）
                if (queryVersion != _currentQueryVersion)
                {
#if DEBUG
                    Log.Debug($"查询已过期，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                    return; // 查询已过期，取消搜索
                }
                
                // 延迟结束后才获取信号量，确保信号量只在需要执行搜索时才被占用
                int semaphoreTimeout;
                try
                {
                    semaphoreTimeout = GetSemaphoreTimeout();
#if DEBUG
                    Log.Debug($"获取到信号量超时时间: {semaphoreTimeout}ms");
#endif
                }
                catch (Exception)
                {
#if DEBUG
                    Log.Debug($"获取信号量超时时间时发生异常");
                    Log.Debug($"使用默认信号量超时时间: 1800ms");
#endif
                    semaphoreTimeout = 1800; // 使用默认值，从1500ms增加到1800ms
                }
                
                semaphoreAcquired = await _searchSemaphore.WaitAsync(TimeSpan.FromMilliseconds(semaphoreTimeout));
                
                if (!semaphoreAcquired)
                {
#if DEBUG
                    Log.Debug($"信号量获取超时，跳过搜索: {query}");
#endif
                    return;
                }
                
                // 使用原子操作确保同一时间只有一个搜索任务在执行
                long expectedActiveVersion = 0;
                if (System.Threading.Interlocked.CompareExchange(ref _activeSearchVersion, queryVersion, expectedActiveVersion) != expectedActiveVersion)
                {
#if DEBUG
                    Log.Debug($"已有其他搜索任务正在执行，当前活动版本: {_activeSearchVersion}, 请求版本: {queryVersion}");
#endif
                    return;
                }
                
                // 验证当前CancellationTokenSource是否仍然有效
                if (localCancellationTokenSource != _searchCancellationTokenSource || localCancellationTokenSource.Token.IsCancellationRequested)
                {
#if DEBUG
                    Log.Debug($"CancellationTokenSource已失效或任务被取消，查询: {query}, 版本: {queryVersion}");
                    Log.Debug($"本地CTS与全局CTS相同: {localCancellationTokenSource == _searchCancellationTokenSource}");
                    Log.Debug($"本地CTS取消状态: {localCancellationTokenSource.Token.IsCancellationRequested}");
#endif
                    // 重置活动搜索版本
                    System.Threading.Interlocked.Exchange(ref _activeSearchVersion, 0);
                    return;
                }
                
                // 再次验证查询版本，确保在获取信号量期间查询没有变化
                if (queryVersion != _currentQueryVersion)
                {
#if DEBUG
                    Log.Debug($"获取信号量后查询验证失败，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                    // 重置活动搜索版本
                    System.Threading.Interlocked.Exchange(ref _activeSearchVersion, 0);
                    return;
                }
                
                // 如果任务没有被取消，则执行搜索
                if (!localCancellationTokenSource.Token.IsCancellationRequested)
                {
#if DEBUG
                    Log.Debug($"执行搜索，查询: {_currentQuery}, 版本: {queryVersion}");
#endif
                    // 使用_currentQuery而不是参数query，确保使用最新的查询字符串
                    await UpdateItemsAsync(_currentQuery, queryVersion, localCancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
#if DEBUG
                Log.Debug($"搜索任务被取消，查询: {query}, 版本: {queryVersion}");
                Log.Debug($"CancellationToken状态: {localCancellationTokenSource?.Token.IsCancellationRequested ?? true}");
                Log.Debug($"查询版本是否匹配: {queryVersion == _currentQueryVersion}");
#endif
                // 任务被取消，这是正常情况，不需要处理
            }
            catch (ObjectDisposedException)
            {
#if DEBUG
                Log.Debug($"对象已释放异常，查询: {query}, 版本: {queryVersion}");
#endif
                // 对象已释放，忽略异常
            }
            catch (Exception)
            {
#if DEBUG
                Log.Debug($"搜索任务发生异常，查询: {query}, 版本: {queryVersion}");
                Log.Debug($"CancellationToken状态: {localCancellationTokenSource?.Token.IsCancellationRequested ?? true}");
                Log.Debug($"查询版本是否匹配: {queryVersion == _currentQueryVersion}");
#endif
                // 记录其他异常
            }
            finally
            {
                // 重置活动搜索版本
                System.Threading.Interlocked.Exchange(ref _activeSearchVersion, 0);
                
                // 更安全的资源清理方式
                try
                {
                    // 只有当本地引用与实例引用相同时才释放全局引用
                    if (localCancellationTokenSource == _searchCancellationTokenSource)
                    {
                        var ctsToDispose = System.Threading.Interlocked.Exchange(ref _searchCancellationTokenSource, null);
                        if (ctsToDispose != null && !ctsToDispose.IsCancellationRequested)
                        {
                            ctsToDispose.Cancel();
                        }
                        ctsToDispose?.Dispose();
                    }
                    else if (localCancellationTokenSource != null)
                    {
                        // 如果本地引用不是全局引用，则只释放本地引用
                        if (!localCancellationTokenSource.IsCancellationRequested)
                        {
                            localCancellationTokenSource.Cancel();
                        }
                        localCancellationTokenSource.Dispose();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 对象已被释放，忽略异常
                }
                catch (Exception)
                {
#if DEBUG
                    Log.Debug($"清理CancellationTokenSource时发生异常");
#endif
                    // 记录清理异常，但不影响主流程
                }
                
                // 释放信号量 - 只有在获取了信号量的情况下才释放
                if (semaphoreAcquired)
                {
                    try
                    {
                        _searchSemaphore.Release();
#if DEBUG
                        Log.Debug($"信号量已释放，查询: {query}, 版本: {queryVersion}");
#endif
                    }
                    catch (ObjectDisposedException)
                    {
                        // 信号量已被释放，忽略异常
                    }
                    catch (System.Threading.SemaphoreFullException)
                    {
                        // 信号量已满，忽略异常
                    }
                }
                
#if DEBUG
                Log.Debug($"搜索任务清理完成，查询: {query}, 版本: {queryVersion}");
#endif
            }
        }
        
        private async System.Threading.Tasks.Task<List<IListItem>> GetItemsAsync(string query, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return new List<IListItem> { _presenter.CreateEmptyQueryItem() };
                }

                var searchResult = await _linkwardenService.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                if (searchResult.Error is not null)
                {
                    return new List<IListItem>(_presenter.CreateErrorItems(searchResult.Error));
                }

                var links = searchResult.Links;
                if (_settingsManager.EnableRerank)
                {
                    links = await _rerankService.RerankLinksAsync(query, links, cancellationToken).ConfigureAwait(false);
                }

                return new List<IListItem>(_presenter.CreateResultItems(links));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Debug($"Search failed: {ex.Message}");
                var error = new LinkwardenSearchError(LinkwardenSearchErrorKind.Unexpected, LocalizedTextKey.ApiCallException, ex.Message);
                return new List<IListItem>(_presenter.CreateErrorItems(error));
            }
        }
        
        public override IListItem[] GetItems()
        {
            return _allItems.ToArray();
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 取消并释放搜索任务
            if (_searchCancellationTokenSource != null)
            {
                _searchCancellationTokenSource.Cancel();
                _searchCancellationTokenSource.Dispose();
                _searchCancellationTokenSource = null;
            }
            
            // 取消订阅设置变更事件
            if (_settingsManager != null)
            {
                _settingsManager.Settings.SettingsChanged -= OnSettingsChanged;
            }
            
            // 释放信号量资源
            _searchSemaphore.Dispose();
        }
    }
}
