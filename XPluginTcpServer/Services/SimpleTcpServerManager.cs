using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XPluginTcpServer.Models;
using XPlugin.logs;

namespace XPluginTcpServer.Services
{
    /// <summary>
    /// 简化的TCP服务器管理器
    /// </summary>
    public class SimpleTcpServerManager
    {
        private readonly ConcurrentDictionary<string, bool> _runningServers;
        private readonly ConcurrentDictionary<string, List<ClientConnectionInfo>> _connections;
        private readonly ConfigManager _configManager;

        public SimpleTcpServerManager(ConfigManager configManager)
        {
            _runningServers = new ConcurrentDictionary<string, bool>();
            _connections = new ConcurrentDictionary<string, List<ClientConnectionInfo>>();
            _configManager = configManager;
        }

        /// <summary>
        /// 服务器状态变化事件
        /// </summary>
        public event Action<string, bool>? ServerStatusChanged;

        /// <summary>
        /// 客户端连接变化事件
        /// </summary>
        public event Action<string, ClientConnectionInfo, bool>? ClientConnectionChanged;

        /// <summary>
        /// 启动服务器
        /// </summary>
        public async Task<bool> StartServerAsync(string serverId)
        {
            try
            {
                var config = _configManager.GetConfig(serverId);
                if (config == null)
                {
                    Log.Error($"未找到服务器配置: {serverId}");
                    return false;
                }

                if (_runningServers.ContainsKey(serverId))
                {
                    Log.Info($"服务器已在运行: {config.Name}");
                    return true;
                }

                // 模拟启动过程
                await Task.Delay(500);

                _runningServers.TryAdd(serverId, true);
                _configManager.UpdateServerStatus(serverId, true, 0);

                Log.Info($"TCP服务器启动成功: {config.Name} ({config.IpAddress}:{config.Port})");
                
                // 触发状态变化事件
                ServerStatusChanged?.Invoke(serverId, true);

                // 模拟客户端连接
                _ = Task.Run(async () => await SimulateClientConnections(serverId));

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"启动TCP服务器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public async Task<bool> StopServerAsync(string serverId)
        {
            try
            {
                if (!_runningServers.TryRemove(serverId, out _))
                {
                    Log.Info($"服务器未在运行: {serverId}");
                    return true;
                }

                // 模拟停止过程
                await Task.Delay(200);

                // 断开所有连接
                if (_connections.TryGetValue(serverId, out var connectionList))
                {
                    foreach (var connection in connectionList.Where(c => c.IsConnected))
                    {
                        connection.IsConnected = false;
                        connection.DisconnectedTime = DateTime.Now;
                        ClientConnectionChanged?.Invoke(serverId, connection, false);
                    }
                }

                _configManager.UpdateServerStatus(serverId, false, 0);

                var config = _configManager.GetConfig(serverId);
                Log.Info($"TCP服务器已停止: {config?.Name ?? serverId}");
                
                // 触发状态变化事件
                ServerStatusChanged?.Invoke(serverId, false);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"停止TCP服务器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重启服务器
        /// </summary>
        public async Task<bool> RestartServerAsync(string serverId)
        {
            await StopServerAsync(serverId);
            await Task.Delay(1000);
            return await StartServerAsync(serverId);
        }

        /// <summary>
        /// 检查服务器是否运行
        /// </summary>
        public bool IsServerRunning(string serverId)
        {
            return _runningServers.ContainsKey(serverId);
        }

        /// <summary>
        /// 获取服务器连接信息
        /// </summary>
        public List<ClientConnectionInfo> GetServerConnections(string serverId)
        {
            if (_connections.TryGetValue(serverId, out var connectionList))
            {
                return connectionList.ToList();
            }
            return new List<ClientConnectionInfo>();
        }

        /// <summary>
        /// 获取当前连接数
        /// </summary>
        public int GetCurrentConnectionCount(string serverId)
        {
            if (_connections.TryGetValue(serverId, out var connectionList))
            {
                return connectionList.Count(c => c.IsConnected);
            }
            return 0;
        }

        /// <summary>
        /// 启动所有自动启动的服务器
        /// </summary>
        public async Task StartAutoStartServersAsync()
        {
            var autoStartConfigs = _configManager.GetAutoStartConfigs();
            foreach (var config in autoStartConfigs)
            {
                Log.Info($"自动启动TCP服务器: {config.Name}");
                await StartServerAsync(config.Id);
            }
        }

        /// <summary>
        /// 停止所有服务器
        /// </summary>
        public async Task StopAllServersAsync()
        {
            var runningServers = _runningServers.Keys.ToList();
            foreach (var serverId in runningServers)
            {
                await StopServerAsync(serverId);
            }
        }

        /// <summary>
        /// 模拟客户端连接
        /// </summary>
        private async Task SimulateClientConnections(string serverId)
        {
            await Task.Delay(3000); // 等待3秒后模拟连接
            
            if (!_runningServers.ContainsKey(serverId)) return;

            // 模拟几个客户端连接
            var random = new Random();
            var clientCount = random.Next(1, 4); // 1-3个客户端

            for (int i = 0; i < clientCount; i++)
            {
                var connectionInfo = new ClientConnectionInfo
                {
                    ConnectionId = Guid.NewGuid().ToString("N")[..8],
                    ClientIp = $"192.168.1.{100 + i}",
                    ClientPort = 12345 + i,
                    ConnectedTime = DateTime.Now,
                    IsConnected = true
                };

                _connections.AddOrUpdate(serverId, 
                    new List<ClientConnectionInfo> { connectionInfo },
                    (key, list) => 
                    {
                        list.Add(connectionInfo);
                        return list;
                    });

                _configManager.IncrementTotalConnections(serverId);
                var currentCount = _connections[serverId].Count(c => c.IsConnected);
                _configManager.UpdateServerStatus(serverId, true, currentCount);

                Log.Info($"模拟客户端已连接: {connectionInfo.ClientIp}:{connectionInfo.ClientPort}");
                ClientConnectionChanged?.Invoke(serverId, connectionInfo, true);

                await Task.Delay(1000); // 间隔1秒连接下一个客户端
            }

            // 模拟一些客户端在一段时间后断开
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000); // 10秒后开始断开一些连接
                
                if (_connections.TryGetValue(serverId, out var connectionList))
                {
                    var connectionsToDisconnect = connectionList.Where(c => c.IsConnected).Take(1).ToList();
                    foreach (var connection in connectionsToDisconnect)
                    {
                        connection.IsConnected = false;
                        connection.DisconnectedTime = DateTime.Now;
                        
                        var currentCount = connectionList.Count(c => c.IsConnected);
                        _configManager.UpdateServerStatus(serverId, true, currentCount);
                        
                        Log.Info($"模拟客户端已断开: {connection.ClientIp}:{connection.ClientPort}");
                        ClientConnectionChanged?.Invoke(serverId, connection, false);
                    }
                }
            });
        }
    }
}
