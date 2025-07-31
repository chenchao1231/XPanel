using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XPluginTcpRelay.Models;

namespace XPluginTcpRelay.Services
{
    /// <summary>
    /// TCP数据中转管理器 - 正确架构实现
    /// 本系统作为中转方：主动连接A方(数据源)，同时提供Server供C方(消费端)连接
    /// 数据流：A ↔ 本系统 ↔ C
    /// </summary>
    public class TcpDataRelayManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, RelayChannel> _relayChannels;
        private readonly ConcurrentDictionary<string, ConnectionInfo> _connections;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private bool _disposed;

        public event EventHandler<ConnectionEventArgs>? ConnectionChanged;
        public event EventHandler<DataTransferEventArgs>? DataTransferred;
        public event EventHandler<string>? LogMessage;

        public TcpDataRelayManager()
        {
            _relayChannels = new ConcurrentDictionary<string, RelayChannel>();
            _connections = new ConcurrentDictionary<string, ConnectionInfo>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 启动中转服务
        /// </summary>
        public async Task<bool> StartAsync(RouteRule rule)
        {
            try
            {
                if (_relayChannels.ContainsKey(rule.Id))
                {
                    LogMessage?.Invoke(this, $"规则 {rule.Name} 已经在运行中");
                    return false;
                }

                var channel = new RelayChannel(rule, _cancellationTokenSource.Token);
                channel.ConnectionChanged += OnConnectionChanged;
                channel.DataTransferred += OnDataTransferred;
                channel.LogMessage += OnLogMessage;

                var success = await channel.StartAsync();
                if (success)
                {
                    _relayChannels[rule.Id] = channel;
                    _isRunning = true;
                    LogMessage?.Invoke(this, $"成功启动中转规则: {rule.Name}");
                    return true;
                }
                else
                {
                    LogMessage?.Invoke(this, $"启动中转规则失败: {rule.Name}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"启动中转规则异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止中转服务
        /// </summary>
        public async Task<bool> StopAsync(string ruleId)
        {
            try
            {
                if (_relayChannels.TryRemove(ruleId, out var channel))
                {
                    await channel.StopAsync();
                    channel.Dispose();
                    LogMessage?.Invoke(this, $"已停止中转规则: {ruleId}");
                    
                    if (_relayChannels.IsEmpty)
                    {
                        _isRunning = false;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"停止中转规则异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止所有中转服务
        /// </summary>
        public async Task StopAllAsync()
        {
            var tasks = new List<Task>();
            foreach (var channel in _relayChannels.Values)
            {
                tasks.Add(channel.StopAsync());
            }
            
            await Task.WhenAll(tasks);
            
            foreach (var channel in _relayChannels.Values)
            {
                channel.Dispose();
            }
            
            _relayChannels.Clear();
            _connections.Clear();
            _isRunning = false;
            
            LogMessage?.Invoke(this, "已停止所有中转服务");
        }

        /// <summary>
        /// 获取所有连接信息
        /// </summary>
        public IEnumerable<ConnectionInfo> GetAllConnections()
        {
            return _connections.Values.ToList();
        }

        /// <summary>
        /// 获取指定规则的连接信息
        /// </summary>
        public IEnumerable<ConnectionInfo> GetConnectionsByRule(string ruleId)
        {
            return _connections.Values.Where(c => c.RouteRuleId == ruleId).ToList();
        }

        /// <summary>
        /// 获取运行状态
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取活跃规则数量
        /// </summary>
        public int ActiveRulesCount => _relayChannels.Count;

        /// <summary>
        /// 获取总连接数
        /// </summary>
        public int TotalConnectionsCount => _connections.Count;

        /// <summary>
        /// 检查指定规则是否正在运行
        /// </summary>
        public bool IsRuleRunning(string ruleId)
        {
            return _relayChannels.ContainsKey(ruleId);
        }

        private void OnConnectionChanged(object? sender, ConnectionEventArgs e)
        {
            if (e.Connection != null)
            {
                if (e.IsConnected)
                {
                    _connections[e.Connection.Id] = e.Connection;
                }
                else
                {
                    _connections.TryRemove(e.Connection.Id, out _);
                }
            }
            ConnectionChanged?.Invoke(this, e);
        }

        private void OnDataTransferred(object? sender, DataTransferEventArgs e)
        {
            DataTransferred?.Invoke(this, e);
        }

        private void OnLogMessage(object? sender, string message)
        {
            LogMessage?.Invoke(this, message);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                
                Task.Run(async () => await StopAllAsync()).Wait(5000);
                
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 连接事件参数
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        public ConnectionInfo? Connection { get; set; }
        public bool IsConnected { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// 数据传输事件参数
    /// </summary>
    public class DataTransferEventArgs : EventArgs
    {
        public string? ConnectionId { get; set; }
        public long BytesTransferred { get; set; }
        public string? Direction { get; set; } // "A->C" or "C->A"
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
