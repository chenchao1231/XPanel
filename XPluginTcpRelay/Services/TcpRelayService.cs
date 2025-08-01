using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XPlugin.Auditing;
using XPlugin.Network;
using XPlugin.logs;
using XPlugin.Utils;
using XPluginTcpRelay.Models;

namespace XPluginTcpRelay.Services
{
    /// <summary>
    /// TCP数据中继服务 - 实现标准接口
    /// 修复版本：解决连接耦合问题，实现真正的中继架构
    /// </summary>
    public class TcpRelayService : IDataRelayService
    {
        private readonly ConcurrentDictionary<string, IRelayRule> _relayRules = new();
        private readonly ConcurrentDictionary<string, RelaySession> _activeSessions = new();
        private readonly ConcurrentDictionary<string, DataSourceConnection> _dataSourceConnections = new();
        private readonly IAuditService? _auditService;
        private RelayStatistics _statistics = new();
        private CancellationTokenSource _cancellationTokenSource = new();
        private bool _isRunning = false;
        private bool _disposed = false;

        /// <summary>
        /// 连接变化事件
        /// </summary>
        public event EventHandler<XPlugin.Network.ConnectionEventArgs>? ConnectionChanged;

        /// <summary>
        /// 数据传输事件
        /// </summary>
        public event EventHandler<XPlugin.Network.DataTransferEventArgs>? DataTransferred;

        /// <summary>
        /// 日志消息事件
        /// </summary>
        public event EventHandler<string>? LogMessage;

        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="auditService">审计服务</param>
        public TcpRelayService(IAuditService? auditService = null)
        {
            _auditService = auditService;
            _statistics.StartTime = DateTime.Now;
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning)
                return true;

            try
            {
                LogMessage?.Invoke(this, "正在启动TCP中继服务...");

                // 重置统计信息
                _statistics = new RelayStatistics();

                // 服务启动成功，不依赖规则启动结果
                _isRunning = true;
                LogMessage?.Invoke(this, "TCP中继服务启动成功");

                if (_auditService != null)
                {
                    await _auditService.LogSystemEventAsync(SystemEventType.ServiceStarted,
                        "TCP中继服务启动");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"启动TCP中继服务失败: {ex.Message}");
                Log.Error($"启动TCP中继服务失败: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 停止服务 - 修复版本：真正关闭所有连接
        /// </summary>
        public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                return true;

            try
            {
                LogMessage?.Invoke(this, "正在停止TCP中继服务...");

                _cancellationTokenSource.Cancel();

                // 停止所有活动会话
                var stopTasks = _activeSessions.Values.Select(session => session.StopAsync()).ToArray();
                await Task.WhenAll(stopTasks);

                _activeSessions.Clear();

                // 停止并销毁所有数据源连接
                var dataSourceStopTasks = _dataSourceConnections.Values.Select(conn => conn.StopAsync()).ToArray();
                await Task.WhenAll(dataSourceStopTasks);

                // 释放所有数据源连接
                foreach (var conn in _dataSourceConnections.Values)
                {
                    conn.Dispose();
                }
                _dataSourceConnections.Clear();

                _isRunning = false;

                // 重新创建CancellationTokenSource以便后续启动
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                LogMessage?.Invoke(this, "TCP中继服务已停止，所有连接已关闭");

                if (_auditService != null)
                {
                    await _auditService.LogSystemEventAsync(SystemEventType.ServiceStopped,
                        "TCP中继服务停止");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"停止TCP中继服务失败: {ex.Message}");
                Log.Error($"停止TCP中继服务失败: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 添加中继规则
        /// </summary>
        public async Task<bool> AddRelayRuleAsync(IRelayRule rule)
        {
            if (rule == null || !rule.IsValid())
                return false;

            try
            {
                if (_relayRules.TryAdd(rule.Id, rule))
                {
                    LogMessage?.Invoke(this, $"添加中继规则: {rule.Name}");

                    // 如果服务正在运行且规则启用，立即启动该规则
                    if (_isRunning && rule.IsEnabled)
                    {
                        await StartRelayRuleAsync(rule);
                    }

                    if (_auditService != null)
                    {
                        await _auditService.LogSystemEventAsync(SystemEventType.ConfigurationSaved,
                            $"添加中继规则: {rule.Name}");
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"添加中继规则失败: {ex.Message}");
                Log.Error($"添加中继规则失败: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 停止中继规则运行 - 修复版本：只停止运行，不删除规则配置
        /// </summary>
        public async Task<bool> RemoveRelayRuleAsync(string ruleId)
        {
            try
            {
                LogMessage?.Invoke(this, $"开始停止规则运行: {ruleId}");

                // 1. 停止相关的会话（这会关闭监听器和所有C方连接）
                if (_activeSessions.TryRemove(ruleId, out var session))
                {
                    await session.StopAsync();
                    LogMessage?.Invoke(this, $"已停止会话: {ruleId}");
                }
                else
                {
                    LogMessage?.Invoke(this, $"规则未在运行: {ruleId}");
                    return true; // 规则本来就没在运行，算作成功
                }

                // 2. 获取规则信息用于数据源连接管理
                if (_relayRules.TryGetValue(ruleId, out var rule) && rule is RouteRule routeRule)
                {
                    var dataSourceKey = $"{routeRule.DataSourceIp}:{routeRule.DataSourcePort}";

                    // 检查是否还有其他活跃规则使用相同数据源
                    var otherActiveRulesUsingSameDataSource = _activeSessions.Keys
                        .Where(activeRuleId => activeRuleId != ruleId)
                        .Select(activeRuleId => _relayRules.TryGetValue(activeRuleId, out var r) ? r : null)
                        .OfType<RouteRule>()
                        .Where(r => $"{r.DataSourceIp}:{r.DataSourcePort}" == dataSourceKey)
                        .Any();

                    // 如果没有其他活跃规则使用该数据源，则关闭数据源连接
                    if (!otherActiveRulesUsingSameDataSource &&
                        _dataSourceConnections.TryRemove(dataSourceKey, out var dataSourceConnection))
                    {
                        await dataSourceConnection.StopAsync();
                        dataSourceConnection.Dispose();
                        LogMessage?.Invoke(this, $"已关闭数据源连接: {dataSourceKey}");
                    }
                    else if (otherActiveRulesUsingSameDataSource)
                    {
                        LogMessage?.Invoke(this, $"数据源连接保持活跃，其他规则仍在使用: {dataSourceKey}");
                    }

                    LogMessage?.Invoke(this, $"规则运行已停止: {rule.Name}");
                }

                if (_auditService != null)
                {
                    await _auditService.LogSystemEventAsync(SystemEventType.ConfigurationSaved,
                        $"停止中继规则运行: {rule?.Name ?? ruleId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"停止中继规则失败: {ex.Message}");
                Log.Error($"停止中继规则失败: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有中继规则
        /// </summary>
        public IEnumerable<IRelayRule> GetRelayRules()
        {
            return _relayRules.Values.ToList();
        }

        /// <summary>
        /// 删除中继规则 - 先停止运行，再从规则列表中删除
        /// </summary>
        public async Task<bool> DeleteRelayRuleAsync(string ruleId)
        {
            try
            {
                LogMessage?.Invoke(this, $"开始删除规则: {ruleId}");

                // 1. 先停止规则运行（如果正在运行）
                if (_activeSessions.ContainsKey(ruleId))
                {
                    await RemoveRelayRuleAsync(ruleId);
                }

                // 2. 从规则列表中删除
                if (_relayRules.TryRemove(ruleId, out var rule))
                {
                    LogMessage?.Invoke(this, $"已删除规则: {rule.Name}");

                    if (_auditService != null)
                    {
                        await _auditService.LogSystemEventAsync(SystemEventType.ConfigurationSaved,
                            $"删除中继规则: {rule.Name}");
                    }

                    return true;
                }

                LogMessage?.Invoke(this, $"规则不存在: {ruleId}");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"删除中继规则失败: {ex.Message}");
                Log.Error($"删除中继规则失败: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 获取网络统计信息
        /// </summary>
        public NetworkStatistics GetStatistics()
        {
            return new NetworkStatistics
            {
                TotalConnections = _statistics.TotalConnections,
                ActiveConnections = _activeSessions.Count,
                TotalBytesReceived = _statistics.TotalBytesReceived,
                TotalBytesSent = _statistics.TotalBytesSent,
                StartTime = _statistics.StartTime
            };
        }

        /// <summary>
        /// 获取中继统计信息
        /// </summary>
        public RelayStatistics GetRelayStatistics()
        {
            _statistics.ActiveRules = _relayRules.Values.Count(r => r.IsEnabled);
            _statistics.TotalRules = _relayRules.Count;
            _statistics.ActiveConnections = _activeSessions.Count;
            return _statistics;
        }



        /// <summary>
        /// 检查规则是否处于活动状态
        /// </summary>
        public bool IsRuleActive(string ruleId)
        {
            return _activeSessions.ContainsKey(ruleId);
        }

        /// <summary>
        /// 数据源连接状态变化事件处理
        /// </summary>
        private void OnDataSourceConnectionStatusChanged(object? sender, DataSourceConnectionStatusEventArgs e)
        {
            LogMessage?.Invoke(this, $"数据源 {e.EndPoint} 连接状态变化: {e.Status} - {e.Message}");

            // 通知UI更新连接状态显示
            var connectionInfo = new Models.ConnectionInfo
            {
                Id = e.EndPoint,
                Type = Models.ConnectionType.DataSourceClient,
                RemoteEndPoint = System.Net.IPEndPoint.Parse(e.EndPoint),
                Status = e.Status == DataSourceConnectionStatus.Connected ?
                    Models.ConnectionStatus.Connected : Models.ConnectionStatus.Disconnected,
                LastActivityTime = DateTime.Now
            };

            var eventType = e.Status == DataSourceConnectionStatus.Connected ?
                XPlugin.Network.ConnectionEventType.Connected : XPlugin.Network.ConnectionEventType.Disconnected;

            var connectionEvent = new XPlugin.Network.ConnectionEventArgs(connectionInfo, eventType, e.Message);

            ConnectionChanged?.Invoke(this, connectionEvent);
        }

        /// <summary>
        /// 获取数据源连接状态
        /// </summary>
        public DataSourceConnectionStatus GetDataSourceConnectionStatus(string dataSourceIp, int dataSourcePort)
        {
            var key = $"{dataSourceIp}:{dataSourcePort}";
            if (_dataSourceConnections.TryGetValue(key, out var connection))
            {
                return connection.Status;
            }
            return DataSourceConnectionStatus.Disconnected;
        }

        /// <summary>
        /// 获取所有活跃连接信息
        /// </summary>
        public IEnumerable<Models.ConnectionInfo> GetActiveConnections()
        {
            var connections = new List<Models.ConnectionInfo>();

            foreach (var session in _activeSessions.Values)
            {
                // 获取该会话的所有C方连接
                var sessionConnections = session.GetActiveConnections();
                connections.AddRange(sessionConnections);
            }

            return connections;
        }

        /// <summary>
        /// 获取指定规则的连接信息
        /// </summary>
        public IEnumerable<Models.ConnectionInfo> GetConnectionsByRule(string ruleId)
        {
            if (_activeSessions.TryGetValue(ruleId, out var session))
            {
                return session.GetActiveConnections();
            }
            return Enumerable.Empty<Models.ConnectionInfo>();
        }

        /// <summary>
        /// 启动中继规则 - 修复版本：独立的数据源连接管理
        /// </summary>
        public async Task<bool> StartRelayRuleAsync(IRelayRule rule)
        {
            try
            {
                if (rule is RouteRule routeRule)
                {
                    // 1. 首先确保数据源连接存在且正常
                    var dataSourceKey = $"{routeRule.DataSourceIp}:{routeRule.DataSourcePort}";
                    if (!_dataSourceConnections.ContainsKey(dataSourceKey))
                    {
                        var dataSourceConn = new DataSourceConnection(routeRule.DataSourceIp, routeRule.DataSourcePort, _auditService);
                        dataSourceConn.LogMessage += (s, msg) => LogMessage?.Invoke(this, msg);
                        dataSourceConn.ConnectionStatusChanged += OnDataSourceConnectionStatusChanged;

                        _dataSourceConnections.TryAdd(dataSourceKey, dataSourceConn);

                        // 启动数据源连接（独立运行，不影响会话启动）
                        _ = Task.Run(async () => await dataSourceConn.StartAsync(_cancellationTokenSource.Token));
                    }

                    // 2. 启动中继会话（监听C方连接）
                    var session = new RelaySession(routeRule, _auditService, _dataSourceConnections[dataSourceKey]);

                    // 订阅事件
                    session.LogMessage += (s, msg) => LogMessage?.Invoke(this, msg);
                    session.ConnectionChanged += (s, e) => ConnectionChanged?.Invoke(this, e);
                    session.DataTransferred += (s, e) => DataTransferred?.Invoke(this, e);

                    if (await session.StartAsync(_cancellationTokenSource.Token))
                    {
                        _activeSessions.TryAdd(rule.Id, session);
                        LogMessage?.Invoke(this, $"中继规则 '{rule.Name}' 启动成功，监听端口: {routeRule.LocalServerPort}");
                        return true;
                    }
                    else
                    {
                        LogMessage?.Invoke(this, $"中继规则 '{rule.Name}' 启动失败");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"启动中继规则 '{rule.Name}' 失败: {ex.Message}");
                Log.Error($"启动中继规则失败: {ex}");
            }

            return false;
        }

        /// <summary>
        /// 会话连接变化事件处理
        /// </summary>
        private void OnSessionConnectionChanged(object? sender, object e)
        {
            _statistics.TotalConnections++;
            // TODO: 转换事件参数类型
            // ConnectionChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 会话数据传输事件处理
        /// </summary>
        private void OnSessionDataTransferred(object? sender, object e)
        {
            // TODO: 实现数据传输统计
            _statistics.ForwardedMessages++;
            // DataTransferred?.Invoke(this, e);
        }

        /// <summary>
        /// 会话日志消息事件处理
        /// </summary>
        private void OnSessionLogMessage(object? sender, string message)
        {
            LogMessage?.Invoke(this, message);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopAsync().Wait(5000); // 等待最多5秒
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 中继会话 - 管理单个规则的中继连接（修复版本）
    /// 只负责管理C方连接，数据源连接由独立的DataSourceConnection管理
    /// </summary>
    internal class RelaySession
    {
        private readonly RouteRule _rule;
        private readonly IAuditService? _auditService;
        private readonly DataSourceConnection _dataSourceConnection;
        private bool _isRunning = false;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly List<RelayConnection> _connections = new();
        private readonly object _lockObject = new();

        // 统计信息
        public int ActiveConnectionsCount => _connections.Count;
        public long TotalBytesTransferred { get; private set; } = 0;
        public long TotalPacketsTransferred { get; private set; } = 0;

        public event EventHandler<XPlugin.Network.ConnectionEventArgs>? ConnectionChanged;
        public event EventHandler<XPlugin.Network.DataTransferEventArgs>? DataTransferred;
        public event EventHandler<string>? LogMessage;

        public RelaySession(RouteRule rule, IAuditService? auditService, DataSourceConnection dataSourceConnection)
        {
            _rule = rule;
            _auditService = auditService;
            _dataSourceConnection = dataSourceConnection;

            // 订阅数据源数据接收事件
            _dataSourceConnection.DataReceived += OnDataSourceDataReceived;
        }

        public async Task<bool> StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // 创建TCP监听器，监听本地端口等待C方连接
                _tcpListener = new TcpListener(IPAddress.Any, _rule.LocalServerPort);
                _tcpListener.Start();

                _isRunning = true;
                LogMessage?.Invoke(this, $"中继会话 '{_rule.Name}' 已启动，监听端口: {_rule.LocalServerPort}");

                // 开始接受连接
                _ = Task.Run(async () => await AcceptConnectionsAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"启动中继会话失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 处理数据源数据接收事件
        /// </summary>
        private void OnDataSourceDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (!_isRunning) return;

            // 将数据转发给所有活跃的C方连接
            lock (_lockObject)
            {
                foreach (var connection in _connections.ToList())
                {
                    _ = Task.Run(async () => await connection.SendToConsumerAsync(e.Data));
                }
            }

            // 更新统计信息
            TotalBytesTransferred += e.Data.Length;
            TotalPacketsTransferred++;

            // 触发数据传输事件
            DataTransferred?.Invoke(this, new XPlugin.Network.DataTransferEventArgs(
                _rule.Id,
                XPlugin.Network.DataDirection.Received,
                e.Data.Length,
                e.Data));
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    LogMessage?.Invoke(this, $"接受到C方连接: {tcpClient.Client.RemoteEndPoint}");

                    // 为每个C方连接创建一个中继连接（修复版本：不再创建独立的A方连接）
                    var connection = new RelayConnection(_rule, tcpClient, _auditService, _dataSourceConnection);
                    connection.LogMessage += (s, msg) => LogMessage?.Invoke(this, msg);
                    connection.DataTransferred += (s, e) => DataTransferred?.Invoke(this, e);
                    connection.ConnectionClosed += OnRelayConnectionClosed;

                    lock (_lockObject)
                    {
                        _connections.Add(connection);
                    }

                    // 通知连接建立
                    var connectionInfo = new Models.ConnectionInfo
                    {
                        Id = connection.Id,
                        Type = Models.ConnectionType.ConsumerServer,
                        RemoteEndPoint = (IPEndPoint?)tcpClient.Client.RemoteEndPoint,
                        LocalEndPoint = (IPEndPoint?)tcpClient.Client.LocalEndPoint,
                        Status = Models.ConnectionStatus.Connected,
                        ConnectedTime = DateTime.Now,
                        RouteRuleId = _rule.Id
                    };

                    ConnectionChanged?.Invoke(this, new XPlugin.Network.ConnectionEventArgs(
                        connectionInfo,
                        XPlugin.Network.ConnectionEventType.Connected,
                        "C方连接已建立"));

                    // 启动中继连接
                    _ = Task.Run(async () => await connection.StartRelayAsync(cancellationToken));
                }
                catch (ObjectDisposedException)
                {
                    // 监听器已被释放，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        LogMessage?.Invoke(this, $"接受连接时发生错误: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 处理中继连接关闭事件
        /// </summary>
        private void OnRelayConnectionClosed(object? sender, EventArgs e)
        {
            if (sender is RelayConnection connection)
            {
                lock (_lockObject)
                {
                    _connections.Remove(connection);
                }

                LogMessage?.Invoke(this, $"C方连接已关闭: {connection.Id}");

                // 通知连接断开
                var disconnectedInfo = new Models.ConnectionInfo
                {
                    Id = connection.Id,
                    Type = Models.ConnectionType.ConsumerServer,
                    Status = Models.ConnectionStatus.Disconnected,
                    RouteRuleId = _rule.Id
                };

                ConnectionChanged?.Invoke(this, new XPlugin.Network.ConnectionEventArgs(
                    disconnectedInfo,
                    XPlugin.Network.ConnectionEventType.Disconnected,
                    "C方连接已断开"));
            }
        }

        public async Task StopAsync()
        {
            _isRunning = false;

            try
            {
                _cancellationTokenSource?.Cancel();
                _tcpListener?.Stop();

                // 停止所有连接
                List<RelayConnection> connectionsToStop;
                lock (_lockObject)
                {
                    connectionsToStop = new List<RelayConnection>(_connections);
                    _connections.Clear();
                }

                var stopTasks = connectionsToStop.Select(c => c.StopAsync());
                await Task.WhenAll(stopTasks);

                LogMessage?.Invoke(this, $"中继会话 '{_rule.Name}' 已停止");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"停止中继会话时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前会话的所有活跃连接
        /// </summary>
        public IEnumerable<Models.ConnectionInfo> GetActiveConnections()
        {
            lock (_lockObject)
            {
                return _connections.Select(conn => new Models.ConnectionInfo
                {
                    Id = conn.Id,
                    Type = Models.ConnectionType.ConsumerServer,
                    RemoteEndPoint = conn.RemoteEndPoint,
                    Status = conn.IsConnected ? Models.ConnectionStatus.Connected : Models.ConnectionStatus.Disconnected,
                    ConnectedTime = conn.ConnectedTime,
                    LastActivityTime = conn.LastActivityTime,
                    RouteRuleId = _rule.Id,
                    ReceivedBytes = conn.ReceivedBytes,
                    SentBytes = conn.SentBytes
                }).ToList();
            }
        }
    }

    /// <summary>
    /// 中继连接 - 处理单个C方连接（修复版本）
    /// 只负责处理C方客户端连接，不再管理A方连接
    /// </summary>
    internal class RelayConnection
    {
        private readonly RouteRule _rule;
        private readonly TcpClient _consumerClient; // C方客户端
        private readonly IAuditService? _auditService;
        private readonly DataSourceConnection _dataSourceConnection; // 共享的数据源连接
        private NetworkStream? _consumerStream;
        private bool _isRunning = false;
        private DateTime _lastActivityTime = DateTime.Now;
        private readonly Timer _heartbeatTimer;
        private const int HeartbeatIntervalMs = 30000; // 30秒心跳检测
        private const int ConnectionTimeoutMs = 60000; // 60秒连接超时

        public string Id { get; } = Guid.NewGuid().ToString();
        public DateTime ConnectedTime { get; } = DateTime.Now;
        public DateTime LastActivityTime => _lastActivityTime;
        public bool IsConnected => _consumerClient?.Connected == true && _consumerStream != null;
        public System.Net.IPEndPoint? RemoteEndPoint => _consumerClient?.Client?.RemoteEndPoint as System.Net.IPEndPoint;

        // 修正字节统计定义：
        // ReceivedBytes: 从数据源接收的字节数（A→C方向）
        // SentBytes: 发送给数据源的字节数（C→A方向）
        public long ReceivedBytes { get; private set; } = 0;  // 从数据源接收的字节
        public long SentBytes { get; private set; } = 0;      // 发送给数据源的字节

        public event EventHandler<string>? LogMessage;
        public event EventHandler<XPlugin.Network.DataTransferEventArgs>? DataTransferred;
        public event EventHandler? ConnectionClosed;

        public RelayConnection(RouteRule rule, TcpClient consumerClient, IAuditService? auditService, DataSourceConnection dataSourceConnection)
        {
            _rule = rule;
            _consumerClient = consumerClient;
            _auditService = auditService;
            _dataSourceConnection = dataSourceConnection;

            // 设置TCP KeepAlive
            SetupTcpKeepAlive(_consumerClient);

            // 初始化心跳检测定时器
            _heartbeatTimer = new Timer(CheckConnectionHealth, null, HeartbeatIntervalMs, HeartbeatIntervalMs);
        }

        public async Task StartRelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                _isRunning = true;
                _consumerStream = _consumerClient.GetStream();

                LogMessage?.Invoke(this, $"中继连接已启动: {_consumerClient.Client.RemoteEndPoint}");

                // 只处理消费端数据，不再管理数据源连接
                await ProcessConsumerDataAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"中继连接错误: {ex.Message}");
            }
            finally
            {
                await StopAsync();
            }
        }

        /// <summary>
        /// 发送数据到消费端
        /// </summary>
        public async Task<bool> SendToConsumerAsync(byte[] data)
        {
            if (!_isRunning || _consumerStream == null || !_consumerClient.Connected) return false;

            try
            {
                await _consumerStream.WriteAsync(data, 0, data.Length);
                await _consumerStream.FlushAsync();
                _lastActivityTime = DateTime.Now;

                // 修正：这是从数据源接收并转发给消费端的数据，应该计入ReceivedBytes
                ReceivedBytes += data.Length;

                // 记录审计日志
                if (_auditService != null)
                {
                    try
                    {
                        await _auditService.LogDataTransferAsync(Id, XPlugin.Auditing.DataDirection.Sent, data);
                        LogMessage?.Invoke(this, $"B→C 数据审计记录: {data.Length} 字节");
                    }
                    catch (Exception auditEx)
                    {
                        LogMessage?.Invoke(this, $"B→C 审计日志记录失败: {auditEx.Message}");
                    }
                }

                // 触发数据传输事件
                DataTransferred?.Invoke(this, new XPlugin.Network.DataTransferEventArgs(
                    Id,
                    XPlugin.Network.DataDirection.Sent,
                    data.Length,
                    data));

                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"发送数据到消费端失败: {ex.Message}");
                return false;
            }
        }

        // ConnectToDataSourceAsync方法已移除，现在使用独立的DataSourceConnection管理数据源连接

        private async Task ProcessConsumerDataAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[_rule.PacketSize];

            try
            {
                while (_isRunning && !cancellationToken.IsCancellationRequested && _consumerClient.Connected)
                {
                    var bytesRead = await _consumerStream!.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        LogMessage?.Invoke(this, "消费端连接已关闭");
                        break;
                    }

                    // 更新活动时间和发送字节统计
                    _lastActivityTime = DateTime.Now;

                    // 修正：这是从消费端接收并发送给数据源的数据，应该计入SentBytes
                    SentBytes += bytesRead;

                    // 转发数据到数据源（如果数据源已连接）
                    if (_dataSourceConnection.IsConnected)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);

                        var success = await _dataSourceConnection.SendDataAsync(data);
                        if (success)
                        {
                            LogMessage?.Invoke(this, $"C→A 转发 {bytesRead} 字节");

                            // 记录审计日志
                            if (_auditService != null)
                            {
                                try
                                {
                                    await _auditService.LogDataTransferAsync(Id, XPlugin.Auditing.DataDirection.Forwarded, data);
                                    LogMessage?.Invoke(this, $"C→A 数据审计记录: {data.Length} 字节");
                                }
                                catch (Exception auditEx)
                                {
                                    LogMessage?.Invoke(this, $"C→A 审计日志记录失败: {auditEx.Message}");
                                }
                            }

                            // 触发数据传输事件
                            DataTransferred?.Invoke(this, new XPlugin.Network.DataTransferEventArgs(
                                Id,
                                XPlugin.Network.DataDirection.Sent,
                                bytesRead,
                                data));

                            // 更新统计信息
                            _rule.ForwardedPackets++;
                            _rule.ForwardedBytes += bytesRead;
                        }
                        else
                        {
                            LogMessage?.Invoke(this, $"转发到数据源失败，丢弃 {bytesRead} 字节数据");
                        }
                    }
                    else
                    {
                        LogMessage?.Invoke(this, $"数据源未连接，丢弃 {bytesRead} 字节数据");
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogMessage?.Invoke(this, $"处理消费端数据错误: {ex.Message}");
            }
            finally
            {
                ConnectionClosed?.Invoke(this, EventArgs.Empty);
            }
        }

        // ProcessDataSourceDataAsync方法已移除，现在由DataSourceConnection处理数据源数据

        // TriggerDataTransferEvent方法已移除，现在直接在相关位置触发事件

        private async Task ForwardDataAsync(NetworkStream fromStream, NetworkStream toStream, string direction, CancellationToken cancellationToken)
        {
            var buffer = new byte[_rule.PacketSize];

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    var bytesRead = await fromStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        LogMessage?.Invoke(this, $"{direction} 连接已关闭");
                        break;
                    }

                    await toStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    await toStream.FlushAsync(cancellationToken);

                    // 更新活动时间
                    _lastActivityTime = DateTime.Now;

                    // 更新统计信息
                    _rule.ForwardedPackets++;
                    _rule.ForwardedBytes += bytesRead;

                    // 记录审计日志
                    if (_auditService != null)
                    {
                        var data = new byte[Math.Min(bytesRead, 256)]; // 只记录前256字节
                        Array.Copy(buffer, data, data.Length);

                        await _auditService.LogDataTransferAsync(
                            _consumerClient.Client.RemoteEndPoint?.ToString() ?? "Unknown",
                            direction == "C→A" ? XPlugin.Auditing.DataDirection.Received : XPlugin.Auditing.DataDirection.Sent,
                            data
                        );
                    }

                    LogMessage?.Invoke(this, $"{direction} 转发 {bytesRead} 字节");

                    // 触发数据传输事件
                    var connectionId = _consumerClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    var dataDirection = direction == "C→A" ? XPlugin.Network.DataDirection.Received : XPlugin.Network.DataDirection.Sent;
                    var dataForEvent = new byte[Math.Min(bytesRead, 256)]; // 只传递前256字节用于事件
                    Array.Copy(buffer, dataForEvent, dataForEvent.Length);
                    var transferEvent = new XPlugin.Network.DataTransferEventArgs(connectionId, dataDirection, bytesRead, dataForEvent);
                    DataTransferred?.Invoke(this, transferEvent);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogMessage?.Invoke(this, $"{direction} 转发错误: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            _isRunning = false;

            try
            {
                // 停止心跳检测
                _heartbeatTimer?.Dispose();

                // 只关闭消费端连接，不影响数据源连接
                _consumerStream?.Close();
                _consumerClient?.Close();
                LogMessage?.Invoke(this, $"中继连接已关闭: {Id}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"关闭连接时发生错误: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 设置TCP KeepAlive
        /// </summary>
        private void SetupTcpKeepAlive(TcpClient client)
        {
            try
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // 设置KeepAlive参数 (Windows)
                var keepAliveValues = new byte[12];
                BitConverter.GetBytes(1).CopyTo(keepAliveValues, 0); // 启用KeepAlive
                BitConverter.GetBytes(30000).CopyTo(keepAliveValues, 4); // 30秒后开始发送KeepAlive
                BitConverter.GetBytes(5000).CopyTo(keepAliveValues, 8); // 每5秒发送一次

                client.Client.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
                LogMessage?.Invoke(this, "已启用TCP KeepAlive");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"设置KeepAlive失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查连接健康状态
        /// </summary>
        private void CheckConnectionHealth(object? state)
        {
            if (!_isRunning) return;

            try
            {
                var timeSinceLastActivity = DateTime.Now - _lastActivityTime;

                // 检查是否超时
                if (timeSinceLastActivity.TotalMilliseconds > ConnectionTimeoutMs)
                {
                    LogMessage?.Invoke(this, $"连接超时，最后活动时间: {_lastActivityTime:HH:mm:ss}");
                    _ = Task.Run(StopAsync);
                    return;
                }

                // 检查消费端连接状态（数据源连接由DataSourceConnection独立管理）
                if (!IsConnectionAlive(_consumerClient))
                {
                    LogMessage?.Invoke(this, "检测到消费端连接断开");
                    _ = Task.Run(StopAsync);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"连接健康检查异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查TCP连接是否存活
        /// </summary>
        private bool IsConnectionAlive(TcpClient? client)
        {
            if (client?.Client == null) return false;

            try
            {
                // 使用Poll方法检查连接状态
                return !(client.Client.Poll(1000, SelectMode.SelectRead) && client.Client.Available == 0);
            }
            catch
            {
                return false;
            }
        }


     
        /// <summary>
        /// 解析端点字符串为IPEndPoint
        /// </summary>
        private static IPEndPoint ParseEndpoint(string endpoint)
        {
            var parts = endpoint.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid endpoint format: {endpoint}");

            var address = IPAddress.Parse(parts[0]);
            var port = int.Parse(parts[1]);
            return new IPEndPoint(address, port);
        }


    }
}
