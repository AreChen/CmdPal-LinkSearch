// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using LinkSearch.Helpers;
using LinkSearch.Services;
using System.Diagnostics.CodeAnalysis;



namespace LinkSearch
{
    // Linkwarden API 响应数据结构
    internal class LinkResult
    {
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public Tag[] tags { get; set; }
        public Collection collection { get; set; }
        
        // 添加构造函数来初始化引用类型属性，避免可空引用类型警告
        public LinkResult()
        {
// #if DEBUG
//             // 调试日志：验证构造函数被调用
//             System.Diagnostics.Debug.WriteLine("LinkResult 构造函数被调用");
// #endif
            
            // 初始化引用类型属性为默认值
            name = string.Empty;
            description = string.Empty;
            url = string.Empty;
            tags = Array.Empty<Tag>();
            collection = new Collection();
        }
    }

    internal class Tag
    {
        public int id { get; set; }
        public string name { get; set; }
        
        // 添加构造函数来初始化引用类型属性
        public Tag()
        {
// #if DEBUG
//             // 调试日志：验证 Tag 构造函数被调用
//             System.Diagnostics.Debug.WriteLine("Tag 构造函数被调用");
// #endif
            name = string.Empty;
        }
    }

    internal class Collection
    {
        public int id { get; set; }
        public string name { get; set; }
        
        // 添加构造函数来初始化引用类型属性
        public Collection()
        {
// #if DEBUG
//             // 调试日志：验证 Collection 构造函数被调用
//             System.Diagnostics.Debug.WriteLine("Collection 构造函数被调用");
// #endif
            name = string.Empty;
        }
    }

    // 打开链接命令
    internal partial class OpenUrlCommand : InvokableCommand
    {
        private readonly string _url;
        private static readonly HashSet<string> AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http",
            "https",
            "mailto",
            "ftp",
            "file"
        };

        public OpenUrlCommand(string url)
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
        }
            

        public override string Id => _url;
        public override string Name => "打开链接";
        public override IconInfo Icon => Icons.LinkSearchExtIcon;


        // 实现 SDK 要求的 PropChanged 事件 (WinRT 风格)
        // 使用 new 关键字隐藏基类事件，因为基类事件不是 virtual 的
        public new event Windows.Foundation.TypedEventHandler<object, Microsoft.CommandPalette.Extensions.IPropChangedEventArgs>? PropChanged;

        // 保护方法用于触发属性变更事件
        protected new virtual void OnPropertyChanged(string propertyName)
        {
            PropChanged?.Invoke(this, new Microsoft.CommandPalette.Extensions.Toolkit.PropChangedEventArgs(propertyName));
        }

        // 在构造函数中触发属性变更事件，确保事件被使用
        public OpenUrlCommand(string url, bool triggerPropertyChange = false) : this(url)
        {
            if (triggerPropertyChange)
            {
                OnPropertyChanged(nameof(Id));
            }
        }

        public override CommandResult Invoke()
        {
            try
            {
                if (Uri.TryCreate(_url, UriKind.Absolute, out var uri))
                {
                    // 优先使用Windows.System.Launcher.LaunchUriAsync
                    bool success = Windows.System.Launcher.LaunchUriAsync(uri).GetAwaiter().GetResult();
                    if (!success)
                    {
                        System.Diagnostics.Debug.WriteLine($"无法打开链接: {_url}");
                        return CommandResult.KeepOpen();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"无效的URL: {_url}");
                    return CommandResult.KeepOpen();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开链接时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"URL: {_url}");
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
        private readonly RerankService _rerankService;
        private readonly RerankConnectionTestService _rerankConnectionTestService;
        private System.Threading.CancellationTokenSource? _searchCancellationTokenSource;
        // 搜索延迟时间（毫秒）- 现在从设置中获取
        private int SearchDelayMilliseconds => _settingsManager.SearchDelayMilliseconds;
        private string _lastErrorMessage = string.Empty;
        private DateTime _lastErrorTime = DateTime.MinValue;
        private readonly System.Threading.SemaphoreSlim _searchSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        
        public LinkSearchPage() : this(new SettingsManager(), new RerankService(new SettingsManager()), new RerankConnectionTestService(new SettingsManager()))
        {
        }
        
        public LinkSearchPage(SettingsManager settingsManager) : this(settingsManager, new RerankService(settingsManager), new RerankConnectionTestService(settingsManager))
        {
        }
        
        public LinkSearchPage(SettingsManager settingsManager, RerankService rerankService) : this(settingsManager, rerankService, new RerankConnectionTestService(settingsManager))
        {
        }
        
        public LinkSearchPage(SettingsManager settingsManager, RerankService rerankService, RerankConnectionTestService rerankConnectionTestService)
        {
            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
            Title = "LinkSearch";
            Name = "Open";
            PlaceholderText = "请输入关键词进行 Linkwarden 检索";
            
            EmptyContent = new ListItem(new NoOpCommand())
            {
                Title = "未找到相关结果",
                Subtitle = "请尝试其他关键词",
                Icon = Icons.LinkSearchExtIcon
            };
            
            // 使用传入的服务
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _rerankService = rerankService ?? throw new ArgumentNullException(nameof(rerankService));
            _rerankConnectionTestService = rerankConnectionTestService ?? throw new ArgumentNullException(nameof(rerankConnectionTestService));
            
            // 订阅设置变更事件
            _settingsManager.Settings.SettingsChanged += OnSettingsChanged;
        }
        
        /// <summary>
        /// 设置变更事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnSettingsChanged(object? sender, Microsoft.CommandPalette.Extensions.Toolkit.Settings e)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("设置发生变更，重新加载当前搜索结果");
#endif
            
            // 如果当前有搜索查询，则重新加载结果
            if (!string.IsNullOrWhiteSpace(_currentQuery))
            {
                // 取消当前的延迟搜索，然后重新开始
                _ = DebouncedUpdateItemsAsync(_currentQuery, _currentQueryVersion);
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
            System.Diagnostics.Debug.WriteLine("开始测试Rerank连接");
#endif
            
            try
            {
                var testResult = await _rerankConnectionTestService.TestConnectionAsync();
                
                if (testResult.IsSuccess)
                {
                    return $"连接成功！响应时间: {testResult.ResponseTimeMs}ms";
                }
                else
                {
                    return $"连接失败: {testResult.ErrorType} - {testResult.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"测试Rerank连接时发生异常: {ex.Message}");
#endif
                return $"测试连接时发生异常: {ex.Message}";
            }
        }
        
        public override void UpdateSearchText(string oldSearch, string newSearch)
        {
            // 立即更新当前查询和版本号
            _currentQuery = newSearch;
            Interlocked.Increment(ref _currentQueryVersion);
            
            // 使用 Task.Run 来捕获和处理异常
            Task.Run(async () =>
            {
                // 检查查询是否有效，避免不必要的信号量等待
                if (string.IsNullOrWhiteSpace(newSearch))
                {
                    // 对于空查询，直接更新UI而不使用信号量
                    await UpdateItemsAsync(newSearch);
                    return;
                }
                
                // 直接执行延迟搜索，不在这里获取信号量
                // 信号量获取将移到延迟结束后、实际搜索前
                try
                {
                    // 执行延迟搜索，传递当前查询版本号
                    await DebouncedUpdateItemsAsync(newSearch, _currentQueryVersion);
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"搜索任务被取消: {newSearch}");
#endif
                    // 任务被取消，这是正常情况，不需要处理
                }
                catch (ObjectDisposedException)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"对象已释放异常: {newSearch}");
#endif
                    // 对象已释放，忽略异常
                }
                catch (Exception)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"UpdateSearchText 中发生未处理的异常");
#endif
                }
            });
        }
        
        private async System.Threading.Tasks.Task UpdateItemsAsync(string query)
        {
            try
            {
                // 清除之前的错误信息
                _lastErrorMessage = string.Empty;
                _allItems = await GetItemsAsync(query);
                RaiseItemsChanged(0);
            }
            catch (Exception ex)
            {
                // 记录错误信息
                _lastErrorMessage = $"搜索失败: {ex.Message}";
                _lastErrorTime = DateTime.Now;
                
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"UpdateItemsAsync 中发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
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
                RaiseItemsChanged(0);
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
                System.Diagnostics.Debug.WriteLine($"查询预验证失败，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
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
                System.Diagnostics.Debug.WriteLine($"开始延迟搜索，查询: {query}, 版本: {queryVersion}");
#endif
                
                // 在开始延迟前获取延迟时间，避免在延迟过程中访问属性导致异常
                int delayMs;
                try
                {
                    delayMs = SearchDelayMilliseconds;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"获取到延迟时间: {delayMs}ms");
#endif
                }
                catch (Exception)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"获取延迟时间时发生异常");
                    System.Diagnostics.Debug.WriteLine($"使用默认延迟时间: 600ms");
#endif
                    delayMs = 600; // 使用默认值，从500ms增加到600ms
                }
                
                // 再次验证查询版本，确保在获取延迟时间期间查询没有变化
                if (queryVersion != _currentQueryVersion)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"获取延迟时间后查询验证失败，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
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
                    System.Diagnostics.Debug.WriteLine($"延迟期间任务被取消，查询: {query}, 版本: {queryVersion}");
#endif
                    return; // 延迟期间被取消，直接返回
                }
                
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"延迟结束，检查查询有效性");
#endif
                // 延迟后再次验证查询版本是否仍然有效（查询有效性验证）
                if (queryVersion != _currentQueryVersion)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"查询已过期，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                    return; // 查询已过期，取消搜索
                }
                
                // 延迟结束后才获取信号量，确保信号量只在需要执行搜索时才被占用
                int semaphoreTimeout;
                try
                {
                    semaphoreTimeout = GetSemaphoreTimeout();
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"获取到信号量超时时间: {semaphoreTimeout}ms");
#endif
                }
                catch (Exception)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"获取信号量超时时间时发生异常");
                    System.Diagnostics.Debug.WriteLine($"使用默认信号量超时时间: 1800ms");
#endif
                    semaphoreTimeout = 1800; // 使用默认值，从1500ms增加到1800ms
                }
                
                semaphoreAcquired = await _searchSemaphore.WaitAsync(TimeSpan.FromMilliseconds(semaphoreTimeout));
                
                if (!semaphoreAcquired)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"信号量获取超时，跳过搜索: {query}");
#endif
                    return;
                }
                
                // 使用原子操作确保同一时间只有一个搜索任务在执行
                long expectedActiveVersion = 0;
                if (System.Threading.Interlocked.CompareExchange(ref _activeSearchVersion, queryVersion, expectedActiveVersion) != expectedActiveVersion)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"已有其他搜索任务正在执行，当前活动版本: {_activeSearchVersion}, 请求版本: {queryVersion}");
#endif
                    return;
                }
                
                // 验证当前CancellationTokenSource是否仍然有效
                if (localCancellationTokenSource != _searchCancellationTokenSource || localCancellationTokenSource.Token.IsCancellationRequested)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"CancellationTokenSource已失效或任务被取消，查询: {query}, 版本: {queryVersion}");
                    System.Diagnostics.Debug.WriteLine($"本地CTS与全局CTS相同: {localCancellationTokenSource == _searchCancellationTokenSource}");
                    System.Diagnostics.Debug.WriteLine($"本地CTS取消状态: {localCancellationTokenSource.Token.IsCancellationRequested}");
#endif
                    // 重置活动搜索版本
                    System.Threading.Interlocked.Exchange(ref _activeSearchVersion, 0);
                    return;
                }
                
                // 再次验证查询版本，确保在获取信号量期间查询没有变化
                if (queryVersion != _currentQueryVersion)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"获取信号量后查询验证失败，当前版本: {_currentQueryVersion}, 请求版本: {queryVersion}");
#endif
                    // 重置活动搜索版本
                    System.Threading.Interlocked.Exchange(ref _activeSearchVersion, 0);
                    return;
                }
                
                // 如果任务没有被取消，则执行搜索
                if (!localCancellationTokenSource.Token.IsCancellationRequested)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"执行搜索，查询: {_currentQuery}, 版本: {queryVersion}");
#endif
                    // 使用_currentQuery而不是参数query，确保使用最新的查询字符串
                    await UpdateItemsAsync(_currentQuery);
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"搜索任务被取消，查询: {query}, 版本: {queryVersion}");
                System.Diagnostics.Debug.WriteLine($"CancellationToken状态: {localCancellationTokenSource?.Token.IsCancellationRequested ?? true}");
                System.Diagnostics.Debug.WriteLine($"查询版本是否匹配: {queryVersion == _currentQueryVersion}");
#endif
                // 任务被取消，这是正常情况，不需要处理
            }
            catch (ObjectDisposedException)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"对象已释放异常，查询: {query}, 版本: {queryVersion}");
#endif
                // 对象已释放，忽略异常
            }
            catch (Exception)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"搜索任务发生异常，查询: {query}, 版本: {queryVersion}");
                System.Diagnostics.Debug.WriteLine($"CancellationToken状态: {localCancellationTokenSource?.Token.IsCancellationRequested ?? true}");
                System.Diagnostics.Debug.WriteLine($"查询版本是否匹配: {queryVersion == _currentQueryVersion}");
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
                    System.Diagnostics.Debug.WriteLine($"清理CancellationTokenSource时发生异常");
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
                        System.Diagnostics.Debug.WriteLine($"信号量已释放，查询: {query}, 版本: {queryVersion}");
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
                System.Diagnostics.Debug.WriteLine($"搜索任务清理完成，查询: {query}, 版本: {queryVersion}");
#endif
            }
        }
        
        private string GetLinkwardenBaseUrl()
        {
            return _settingsManager.LinkwardenBaseUrl;
        }

        private async System.Threading.Tasks.Task<List<IListItem>> GetItemsAsync(string query)
        {
            // 如果查询为空，返回提示信息
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<IListItem>
                {
                    new ListItem(new NoOpCommand()) { Title = "请输入关键词进行 Linkwarden 检索", Icon = Icons.LinkSearchExtIcon }
                };
            }

            // 读取 Token
            var token = _settingsManager.LinkwardenApiKey;
            
            // 调试日志：验证Token获取
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Token获取结果: {(string.IsNullOrEmpty(token) ? "未获取到Token" : "已获取到Token")}");
#endif

            // 统一判断 token 是否为空，如果为空则返回错误提示
            if (string.IsNullOrWhiteSpace(token))
            {
                return new List<IListItem>
                {
                    new ListItem(new NoOpCommand()) { Title = "未检测到 Linkwarden API Token", Icon = Icons.LinkSearchExtIcon },
                    new ListItem(new NoOpCommand()) { Title = "请在插件设置中配置API Key", Icon = Icons.LinkSearchExtIcon },
                    new ListItem(new NoOpCommand()) { Title = "或设置环境变量: set LINKWARDEN_API_KEY=your_token", Icon = Icons.LinkSearchExtIcon },
                    new ListItem(new NoOpCommand()) { Title = "或创建配置文件: LinkSearch/config.json", Icon = Icons.LinkSearchExtIcon }
                };
            }
            
            // 验证API Key格式 - 使用类型名调用静态方法
            if (!SettingsManager.ValidateApiKey(token))
            {
                return new List<IListItem>
                {
                    new ListItem(new NoOpCommand()) { Title = "API Key 格式无效", Icon = Icons.LinkSearchExtIcon },
                    new ListItem(new NoOpCommand()) { Title = "请检查您的API Key是否正确", Icon = Icons.LinkSearchExtIcon }
                };
            }

            // 调用 API
            try
            {
                using var client = new System.Net.Http.HttpClient();
                // 设置超时时间
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                string baseUrl = GetLinkwardenBaseUrl();
                
                // 调试日志：记录Base URL
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"使用的Base URL: {baseUrl}");
#endif
                
                // 验证Base URL是否有效
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Base URL 为空或无效");
#endif
                    return new List<IListItem>
                    {
                        new ListItem(new NoOpCommand()) { Title = "Linkwarden Base URL 无效", Icon = Icons.LinkSearchExtIcon },
                        new ListItem(new NoOpCommand()) { Title = "请在插件设置中配置Base URL", Icon = Icons.LinkSearchExtIcon }
                    };
                }
                
                var url = $"{baseUrl}/api/v1/search?searchQueryString={Uri.EscapeDataString(query)}";
                
                // 调试日志：记录完整请求URL
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"完整请求URL: {url}");
#endif
                
                var resp = await client.GetAsync(url);
                
                // 调试日志：记录响应状态码
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"API响应状态码: {resp.StatusCode}");
#endif
                
                if (!resp.IsSuccessStatusCode)
                {
                    // 调试日志：记录失败原因
                    var errorContent = await resp.Content.ReadAsStringAsync();
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"API请求失败，状态码: {resp.StatusCode}, 响应内容: {errorContent}");
#endif
                    
                    return new List<IListItem>
                    {
                        new ListItem(new NoOpCommand()) { Title = $"API 请求失败: {resp.StatusCode}", Icon = Icons.LinkSearchExtIcon }
                    };
                }
                
                var json = await resp.Content.ReadAsStringAsync();
                
                // 调试提示：显示 API 响应内容（仅在开发环境）
// #if DEBUG
//                 System.Diagnostics.Debug.WriteLine($"API 响应: {json}");
// #endif
                
                var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
                
                // 安全检查：验证"data"节点存在性
                if (!root.TryGetProperty("data", out var dataElement))
                {
                    return new List<IListItem>
                    {
                        new ListItem(new NoOpCommand()) { Title = "API 响应格式错误: 缺少 data 节点", Icon = Icons.LinkSearchExtIcon }
                    };
                }
                
                // 检查 data 元素的类型
                JsonElement linksElement;
                
                // 调试日志：记录 data 元素的类型
// #if DEBUG
//                 System.Diagnostics.Debug.WriteLine($"API 响应中 data 元素的类型: {dataElement.ValueKind}");
// #endif
                
                // 如果 data 是一个对象且包含 links 属性，则使用 links 属性
                if (dataElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    dataElement.TryGetProperty("links", out linksElement))
                {
                    // 调试日志：确认使用对象格式的 data.links
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("使用对象格式的 data.links");
#endif
                }
                // 如果 data 本身就是一个数组，则直接使用 data
                else if (dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    linksElement = dataElement;
                    // 调试日志：确认使用数组格式的 data
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("使用数组格式的 data");
#endif
                }
                else
                {
                    // 调试日志：记录不支持的 data 格式
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"不支持的 data 格式: {dataElement.ValueKind}");
#endif
                    return new List<IListItem>
                    {
                        new ListItem(new NoOpCommand()) { Title = "API 响应格式错误: data 节点格式不正确", Icon = Icons.LinkSearchExtIcon }
                    };
                }
                
                // 安全检查：验证"links"节点为数组类型
                if (linksElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    return new List<IListItem>
                    {
                        new ListItem(new NoOpCommand()) { Title = "API 响应格式错误: links 节点不是数组类型", Icon = Icons.LinkSearchExtIcon }
                    };
                }
                
                var items = new List<IListItem>();

                // 轻量预解析：仅提取必要字段，避免未启用rerank时的对象与日志开销
                var rawList = new List<(string Name, string Desc, string Url, string Collection, string TagsStr, JsonElement? TagsElement)>();
                foreach (var link in linksElement.EnumerateArray())
                {
                    var name = link.GetProperty("name").GetString() ?? string.Empty;
                    var desc = link.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    var urlstr = link.GetProperty("url").GetString() ?? string.Empty;

                    JsonElement? tagsElement = null;
                    string tagsStr = "";
                    if (link.TryGetProperty("tags", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        tagsElement = t;
                        // 内联方法：拼接标签字符串（避免额外分配）
                        static string GetTagsString(JsonElement tagArr)
                        {
                            var tagsList = new List<string>();
                            foreach (var tag in tagArr.EnumerateArray())
                            {
                                if (tag.TryGetProperty("name", out var tn))
                                {
                                    var tagName = tn.GetString();
                                    if (!string.IsNullOrEmpty(tagName))
                                        tagsList.Add(tagName);
                                }
                            }
                            return string.Join(", ", tagsList);
                        }
                        tagsStr = GetTagsString(t);
                    }

                    var collection = link.TryGetProperty("collection", out var c) && c.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";

                    rawList.Add((name, desc, urlstr, collection, tagsStr, tagsElement));
                }

                // 未启用rerank：直接根据rawList渲染UI，避免创建LinkResult/Tag/Collection对象与日志
                if (!_settingsManager.EnableRerank || rawList.Count == 0)
                {
                    foreach (var r in rawList)
                    {
                        if (string.IsNullOrWhiteSpace(r.Url))
                            continue;

                        try
                        {
                            var display = $"{r.Name} [{r.Collection}]";
                            var detail = $"{r.Desc} {(string.IsNullOrEmpty(r.TagsStr) ? "" : $"#标签: {r.TagsStr}")}";
                            var openUrlCommand = new OpenUrlCommand(r.Url);
                            items.Add(new ListItem(openUrlCommand)
                            {
                                Title = display,
                                Subtitle = detail,
                                Icon = Icons.LinkSearchExtIcon
                            });
                        }
                        catch (ArgumentException)
                        {
                            System.Diagnostics.Debug.WriteLine($"跳过无效URL: {r.Url}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"处理完成，共找到 {items.Count} 个结果");

                    if (items.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("添加'未找到相关结果'提示");
                        items.Add(new ListItem(new NoOpCommand()) { Title = "未找到相关结果", Icon = Icons.LinkSearchExtIcon });
                    }

                    return items;
                }

                // 启用rerank：仅此分支把rawList转换为LinkResult并调用RerankService
                var objectCreationStart = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"开始创建LinkResult对象，数量: {rawList.Count}");
#endif
                
                var linkResults = new List<LinkResult>(rawList.Count);
                foreach (var r in rawList)
                {
                    var lr = new LinkResult
                    {
                        id = 0,
                        name = r.Name,
                        description = r.Desc,
                        url = r.Url,
                        tags = r.TagsElement.HasValue ? GetTagsArray(r.TagsElement.Value) : Array.Empty<Tag>(),
                        collection = new Collection { name = r.Collection }
                    };
                    linkResults.Add(lr);
                }
                
                objectCreationStart.Stop();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"LinkResult对象创建完成，耗时: {objectCreationStart.ElapsedMilliseconds}ms");
#endif

                try
                {
                    var rerankStart = System.Diagnostics.Stopwatch.StartNew();
    #if DEBUG
                    System.Diagnostics.Debug.WriteLine($"开始调用RerankService");
    #endif
                    
                    var rerankedResults = await _rerankService.RerankLinksAsync(query, linkResults);
                    
                    rerankStart.Stop();
    #if DEBUG
                    System.Diagnostics.Debug.WriteLine($"RerankService调用完成，总耗时: {rerankStart.ElapsedMilliseconds}ms");
    #endif

                    items.Clear();
                    foreach (var lr in rerankedResults)
                    {
                        if (string.IsNullOrWhiteSpace(lr.url))
                            continue;

                        try
                        {
                            var display = $"{lr.name} [{lr.collection.name}]";
                            var tagsStr2 = GetTagsStringFromArray(lr.tags);
                            var detail = $"{lr.description} {(string.IsNullOrEmpty(tagsStr2) ? "" : $"#标签: {tagsStr2}")}";
                            var openUrlCommand = new OpenUrlCommand(lr.url);
                            items.Add(new ListItem(openUrlCommand)
                            {
                                Title = display,
                                Subtitle = detail,
                                Icon = Icons.LinkSearchExtIcon
                            });
                        }
                        catch (ArgumentException)
                        {
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"跳过无效URL: {lr.url}");
#endif
                        }
                    }

    #if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Rerank完成，重排序后的结果数量: {items.Count}");
    #endif
                }
                catch (Exception)
                {
                    // 记录rerank异常，使用原始rawList回退
    #if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Rerank过程中发生异常");
                    System.Diagnostics.Debug.WriteLine($"使用原始搜索结果");
    #endif

                    items.Clear();
                    foreach (var r in rawList)
                    {
                        if (string.IsNullOrWhiteSpace(r.Url))
                            continue;

                        try
                        {
                            var display = $"{r.Name} [{r.Collection}]";
                            var detail = $"{r.Desc} {(string.IsNullOrEmpty(r.TagsStr) ? "" : $"#标签: {r.TagsStr}")}";
                            var openUrlCommand = new OpenUrlCommand(r.Url);
                            items.Add(new ListItem(openUrlCommand)
                            {
                                Title = display,
                                Subtitle = detail,
                                Icon = Icons.LinkSearchExtIcon
                            });
                        }
                        catch (ArgumentException)
                        {
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"跳过无效URL: {r.Url}");
#endif
                        }
                    }
                }

    #if DEBUG
                System.Diagnostics.Debug.WriteLine($"处理完成，共找到 {items.Count} 个结果");
    #endif

                if (items.Count == 0)
                {
    #if DEBUG
                    System.Diagnostics.Debug.WriteLine("添加'未找到相关结果'提示");
    #endif
                    items.Add(new ListItem(new NoOpCommand()) { Title = "未找到相关结果", Icon = Icons.LinkSearchExtIcon });
                }

                return items;
            }
            catch (System.Net.Http.HttpRequestException)
            {
                // 调试日志：记录HTTP请求异常详细信息
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"HTTP请求异常");
#endif
                
                // 根据异常类型提供更具体的错误提示
                string errorMessage = "网络请求失败：请检查网络连接和服务器地址";
                
                return new List<IListItem>
                {
                    new ListItem(new NoOpCommand()) { Title = errorMessage, Icon = Icons.LinkSearchExtIcon },
                    new ListItem(new NoOpCommand()) { Title = "请检查服务器地址和网络连接", Icon = Icons.LinkSearchExtIcon }
                };
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // 调试日志：记录任务取消异常（通常是超时）
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"任务取消异常（超时）");
#endif
                
                return new List<IListItem>
                {
                    new ListItem(new NoOpCommand()) { Title = "请求超时：服务器响应时间过长", Icon = Icons.LinkSearchExtIcon },
                    new ListItem(new NoOpCommand()) { Title = "请检查网络连接或稍后重试", Icon = Icons.LinkSearchExtIcon }
                };
            }
            catch (System.UriFormatException)
            {
                // 调试日志：记录URL格式异常
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"URL格式异常");
#endif
                
                return new List<IListItem>
                {
                    new ListItem(new NoOpCommand()) { Title = "URL格式错误：服务器地址无效", Icon = Icons.LinkSearchExtIcon },
                    new ListItem(new NoOpCommand()) { Title = "请在插件设置中检查服务器地址", Icon = Icons.LinkSearchExtIcon }
                };
            }
            catch (Exception ex)
            {
                // 调试日志：记录其他异常
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"未预期的异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"内部异常: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
#endif
                
                return new List<IListItem>
                {
                    new ListItem(new NoOpCommand()) { Title = $"API 调用异常: {ex.Message}", Icon = Icons.LinkSearchExtIcon }
                };
            }
        }
        
        /// <summary>
        /// 从JsonElement中提取标签数组
        /// </summary>
        /// <param name="tagElement">标签JsonElement</param>
        /// <returns>标签数组</returns>
        private static Tag[] GetTagsArray(JsonElement tagElement)
        {
            if (tagElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return Array.Empty<Tag>();
            }
            
            var tagsList = new List<Tag>();
            foreach (var tag in tagElement.EnumerateArray())
            {
                if (tag.TryGetProperty("name", out var tn))
                {
                    var tagName = tn.GetString();
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        tagsList.Add(new Tag { name = tagName });
                    }
                }
            }
            
            return tagsList.ToArray();
        }
        
        /// <summary>
        /// 从标签数组中获取标签字符串
        /// </summary>
        /// <param name="tags">标签数组</param>
        /// <returns>标签字符串</returns>
        private static string GetTagsStringFromArray(Tag[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                return string.Empty;
            }
            
            var tagNames = tags.Where(t => !string.IsNullOrEmpty(t.name)).Select(t => t.name);
            return string.Join(", ", tagNames);
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
            
            // 释放服务资源
            if (_rerankService != null)
            {
                _rerankService.Dispose();
            }
            
            if (_rerankConnectionTestService != null)
            {
                _rerankConnectionTestService.Dispose();
            }
            
            // 释放信号量资源
            if (_searchSemaphore != null)
            {
                _searchSemaphore.Dispose();
            }
        }
    }
}
