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
        private const int PreserveSelectionRefresh = -2;
        private const int MinimumSearchQueryLength = 2;

        private List<IListItem> _allItems = new List<IListItem>();
        private string _currentQuery = string.Empty;
        private long _currentQueryVersion; // 查询版本号，用于验证查询有效性
        private readonly SettingsManager _settingsManager;
        private readonly LinkwardenService _linkwardenService;
        private readonly RerankService _rerankService;
        private readonly RerankConnectionTestService _rerankConnectionTestService;
        private readonly SearchResultPresenter _presenter;
        private readonly object _searchDebounceLock = new object();
        private System.Threading.Timer? _searchDebounceTimer;
        private bool _hasScheduledSearch;
        private long _scheduledSearchVersion;
        private long _scheduledSearchDueTick;
        private long _lastStartedSearchVersion;
        private int _cachedSearchDelayMilliseconds;
        // 搜索延迟时间（毫秒）- 现在从设置中获取
        private int SearchDelayMilliseconds => _settingsManager.SearchDelayMilliseconds;
        private string _lastErrorMessage = string.Empty;
        private DateTime _lastErrorTime = DateTime.MinValue;
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
            _cachedSearchDelayMilliseconds = ReadSearchDelayMilliseconds();
            
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
            _cachedSearchDelayMilliseconds = ReadSearchDelayMilliseconds();
            Name = _settingsManager.Text(LocalizedTextKey.PageName);
            PlaceholderText = _settingsManager.Text(LocalizedTextKey.SearchPlaceholder);
            EmptyContent = _presenter.CreateEmptyResultItem();
            var queryVersion = Interlocked.Increment(ref _currentQueryVersion);
            
            if (IsEmptyQuery(_currentQuery))
            {
                StopDebouncedSearch();
                _allItems = new List<IListItem> { _presenter.CreateEmptyQueryItem() };
                RaiseItemsChangedOnUiThread();
            }
            else
            {
                // 重新安排防抖搜索；已发出的旧请求自然完成，结果会被版本号丢弃。
                ScheduleDebouncedSearch(queryVersion);
            }
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
            var effectiveNewSearch = newSearch ?? string.Empty;
            _currentQuery = effectiveNewSearch;
            var queryVersion = Interlocked.Increment(ref _currentQueryVersion);

            if (IsEmptyQuery(effectiveNewSearch))
            {
                StopDebouncedSearch();
                QueueEmptyQueryUpdate(queryVersion);
                return;
            }

            ScheduleDebouncedSearch(queryVersion);
        }

        private void QueueEmptyQueryUpdate(long queryVersion)
        {
            ThreadPool.QueueUserWorkItem(static state =>
            {
                var item = ((LinkSearchPage Page, long QueryVersion))state!;
                var task = item.Page.UpdateItemsAsync(string.Empty, item.QueryVersion, System.Threading.CancellationToken.None);
                task.ContinueWith(tt =>
                {
                    if (tt.IsFaulted)
                    {
                        Log.Error($"UpdateItemsAsync 未观察到的异常: {tt.Exception?.Flatten().Message}");
                    }
                }, TaskScheduler.Default);
            }, (this, queryVersion));
        }

        private static bool IsEmptyQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query) || query.Trim().Length < MinimumSearchQueryLength;
        }

        private int ReadSearchDelayMilliseconds()
        {
            try
            {
                return SearchDelayMilliseconds;
            }
            catch (Exception)
            {
                return 600;
            }
        }

        private void ScheduleDebouncedSearch(long queryVersion)
        {
            var delayMs = Volatile.Read(ref _cachedSearchDelayMilliseconds);

            lock (_searchDebounceLock)
            {
                if (queryVersion != Interlocked.Read(ref _currentQueryVersion))
                {
                    return;
                }

                _hasScheduledSearch = true;
                _scheduledSearchVersion = queryVersion;
                _scheduledSearchDueTick = Environment.TickCount64 + delayMs;
                _searchDebounceTimer ??= new System.Threading.Timer(static state =>
                {
                    ((LinkSearchPage)state!).StartDebouncedSearch();
                }, this, Timeout.Infinite, Timeout.Infinite);

                _searchDebounceTimer.Change(delayMs, Timeout.Infinite);
            }
        }

        private void StopDebouncedSearch()
        {
            lock (_searchDebounceLock)
            {
                _hasScheduledSearch = false;
                _searchDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void StartDebouncedSearch()
        {
            long queryVersion;

            lock (_searchDebounceLock)
            {
                if (!_hasScheduledSearch)
                {
                    return;
                }

                var remainingMs = _scheduledSearchDueTick - Environment.TickCount64;
                if (remainingMs > 0)
                {
                    _searchDebounceTimer?.Change((int)Math.Min(remainingMs, int.MaxValue), Timeout.Infinite);
                    return;
                }

                _hasScheduledSearch = false;
                queryVersion = _scheduledSearchVersion;
            }

            if (queryVersion != Interlocked.Read(ref _currentQueryVersion))
            {
                return;
            }

            var query = _currentQuery;
            if (IsEmptyQuery(query) || Interlocked.Exchange(ref _lastStartedSearchVersion, queryVersion) == queryVersion)
            {
                return;
            }

            var task = ExecuteSearchAsync(query, queryVersion);
            task.ContinueWith(tt =>
            {
                if (tt.IsFaulted)
                {
                    Log.Error($"ExecuteSearchAsync 未观察到的异常: {tt.Exception?.Flatten().Message}");
                }
            }, TaskScheduler.Default);
        }

        private void RaiseItemsChangedOnUiThread()
        {
            try
            {
                if (_syncContext != null)
                {
                    _syncContext.Post(_ =>
                    {
                        try { RaiseItemsChanged(PreserveSelectionRefresh); }
                        catch (Exception ex2) { Log.Error($"RaiseItemsChanged 调用失败: {ex2.Message}"); }
                    }, null);
                }
                else
                {
                    // -2 matches CmdPal's incremental refresh mode, preserving selection during async result updates.
                    RaiseItemsChanged(PreserveSelectionRefresh);
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
        
        private async System.Threading.Tasks.Task ExecuteSearchAsync(string query, long queryVersion)
        {
            // 查询版本预验证：过期查询不应发起网络请求。
            if (queryVersion != Interlocked.Read(ref _currentQueryVersion))
            {
#if DEBUG
                Log.Debug($"查询预验证失败，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                return; // 查询已过期，取消搜索
            }

            try
            {
                if (IsEmptyQuery(query))
                {
                    return;
                }

                if (queryVersion != Interlocked.Read(ref _currentQueryVersion))
                {
#if DEBUG
                    Log.Debug($"查询已过期，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                    return; // 查询已过期，取消搜索
                }

#if DEBUG
                Log.Debug($"执行搜索，查询: {query}, 版本: {queryVersion}");
#endif
                await UpdateItemsAsync(query, queryVersion, System.Threading.CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
#if DEBUG
                Log.Debug($"搜索任务被取消，查询: {query}, 版本: {queryVersion}");
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
                Log.Debug($"查询版本是否匹配: {queryVersion == _currentQueryVersion}");
#endif
                // 记录其他异常
            }
        }
        
        private async System.Threading.Tasks.Task<List<IListItem>> GetItemsAsync(string query, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                if (IsEmptyQuery(query))
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

                var resultItems = new List<IListItem>(_presenter.CreateResultItems(links));
                return resultItems;
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
            lock (_searchDebounceLock)
            {
                _searchDebounceTimer?.Dispose();
                _searchDebounceTimer = null;
            }

            // 取消订阅设置变更事件
            if (_settingsManager != null)
            {
                _settingsManager.Settings.SettingsChanged -= OnSettingsChanged;
            }
        }
    }
}
