using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XPluginTcpServer.Models;
using XPlugin.logs;

namespace XPluginTcpServer.Services
{
    /// <summary>
    /// TCP服务器管理器 - 完整实现版本
    /// </summary>
    public class TcpServerManager
    {
        private readonly ConcurrentDictionary<string, TcpServerInstance> _servers;
        private readonly ConcurrentDictionary<string, List<ClientConnectionInfo>> _connections;
        private readonly ConfigManager _configManager;

        /// <summary>
        /// TCP服务器实例
        /// </summary>
        private class TcpServerInstance
        {
            public TcpListener Listener { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Task AcceptTask { get; set; }
            public List<TcpClient> Clients { get; set; } = new List<TcpClient>();
            public object ClientsLock { get; set; } = new object();
        }

        public TcpServerManager(ConfigManager configManager)
        {
            _servers = new ConcurrentDictionary<string, TcpServerInstance>();
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

                if (_servers.ContainsKey(serverId))
                {
                    Log.Info($"服务器已在运行: {config.Name}");
                    return true;
                }

                // 创建TCP监听器
                var ipAddress = IPAddress.Parse(config.IpAddress);
                var listener = new TcpListener(ipAddress, config.Port);
                var cancellationTokenSource = new CancellationTokenSource();

                var serverInstance = new TcpServerInstance
                {
                    Listener = listener,
                    CancellationTokenSource = cancellationTokenSource
                };

                // 启动监听
                listener.Start();
                Log.Info($"TCP服务器开始监听: {config.IpAddress}:{config.Port}");

                // 开始接受连接
                serverInstance.AcceptTask = AcceptClientsAsync(serverId, serverInstance, cancellationTokenSource.Token);

                _servers.TryAdd(serverId, serverInstance);
                _configManager.UpdateServerStatus(serverId, true, 0);

                Log.Info($"TCP服务器启动成功: {config.Name} ({config.IpAddress}:{config.Port})");

                // 触发状态变化事件
                ServerStatusChanged?.Invoke(serverId, true);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"启动TCP服务器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 接受客户端连接
        /// </summary>
        private async Task AcceptClientsAsync(string serverId, TcpServerInstance serverInstance, CancellationToken cancellationToken)
        {
            var config = _configManager.GetConfig(serverId);
            if (config == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await serverInstance.Listener.AcceptTcpClientAsync();
                    var clientEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;

                    if (clientEndPoint != null)
                    {
                        var connectionInfo = new ClientConnectionInfo
                        {
                            ConnectionId = Guid.NewGuid().ToString("N")[..8],
                            ClientIp = clientEndPoint.Address.ToString(),
                            ClientPort = clientEndPoint.Port,
                            ConnectedTime = DateTime.Now,
                            IsConnected = true
                        };

                        // 添加到连接列表
                        _connections.AddOrUpdate(serverId,
                            new List<ClientConnectionInfo> { connectionInfo },
                            (key, list) =>
                            {
                                list.Add(connectionInfo);
                                return list;
                            });

                        // 添加到服务器实例的客户端列表
                        lock (serverInstance.ClientsLock)
                        {
                            serverInstance.Clients.Add(tcpClient);
                        }

                        // 更新统计信息
                        _configManager.IncrementTotalConnections(serverId);
                        var currentCount = _connections[serverId].Count(c => c.IsConnected);
                        _configManager.UpdateServerStatus(serverId, true, currentCount);

                        Log.Info($"客户端已连接: {connectionInfo.ClientIp}:{connectionInfo.ClientPort} -> {config.Name}");

                        // 触发事件
                        ClientConnectionChanged?.Invoke(serverId, connectionInfo, true);

                        // 处理客户端通信
                        _ = Task.Run(async () => await HandleClientAsync(serverId, tcpClient, connectionInfo, cancellationToken));
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // 服务器已停止，正常情况
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Log.Error($"接受客户端连接时发生错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 处理客户端通信
        /// </summary>
        private async Task HandleClientAsync(string serverId, TcpClient tcpClient, ClientConnectionInfo connectionInfo, CancellationToken cancellationToken)
        {
            var config = _configManager.GetConfig(serverId);
            NetworkStream? stream = null;

            try
            {
                stream = tcpClient.GetStream();
                var buffer = new byte[config?.BufferSize ?? 1024];

                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        // 客户端断开连接
                        break;
                    }

                    // 更新接收字节数
                    connectionInfo.BytesReceived += bytesRead;

                    // 处理接收到的数据
                    var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Log.Debug($"收到数据 from {connectionInfo.ClientIp}:{connectionInfo.ClientPort}: {receivedData.Trim()}");

                    // 简单回显
                    var response = $"Echo: {receivedData}";
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);

                    // 更新发送字节数
                    connectionInfo.BytesSent += responseBytes.Length;
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Log.Error($"处理客户端通信时发生错误: {ex.Message}");
                }
            }
            finally
            {
                // 清理资源
                try
                {
                    stream?.Close();
                    tcpClient.Close();

                    // 从服务器实例的客户端列表中移除
                    if (_servers.TryGetValue(serverId, out var serverInstance))
                    {
                        lock (serverInstance.ClientsLock)
                        {
                            serverInstance.Clients.Remove(tcpClient);
                        }
                    }

                    // 更新连接状态
                    connectionInfo.IsConnected = false;
                    connectionInfo.DisconnectedTime = DateTime.Now;

                    // 更新连接数
                    if (_connections.TryGetValue(serverId, out var connectionList))
                    {
                        var currentCount = connectionList.Count(c => c.IsConnected);
                        _configManager.UpdateServerStatus(serverId, true, currentCount);
                    }

                    Log.Info($"客户端已断开: {connectionInfo.ClientIp}:{connectionInfo.ClientPort} -> {config?.Name}, 持续时间: {connectionInfo.Duration}");

                    // 触发事件
                    ClientConnectionChanged?.Invoke(serverId, connectionInfo, false);
                }
                catch (Exception ex)
                {
                    Log.Error($"清理客户端资源时发生错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public async Task<bool> StopServerAsync(string serverId)
        {
            try
            {
                if (!_servers.TryRemove(serverId, out var serverInstance))
                {
                    Log.Info($"服务器未在运行: {serverId}");
                    return true;
                }

                // 停止接受新连接
                serverInstance.CancellationTokenSource.Cancel();

                // 停止监听器
                serverInstance.Listener.Stop();

                // 断开所有客户端连接
                lock (serverInstance.ClientsLock)
                {
                    foreach (var client in serverInstance.Clients.ToList())
                    {
                        try
                        {
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"关闭客户端连接时发生错误: {ex.Message}");
                        }
                    }
                    serverInstance.Clients.Clear();
                }

                // 等待接受任务完成
                try
                {
                    await serverInstance.AcceptTask;
                }
                catch (Exception ex)
                {
                    Log.Debug($"等待接受任务完成时发生错误: {ex.Message}");
                }

                // 释放资源
                serverInstance.CancellationTokenSource.Dispose();

                // 更新所有连接状态为断开
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
            await Task.Delay(1000); // 等待1秒
            return await StartServerAsync(serverId);
        }

        /// <summary>
        /// 检查服务器是否运行
        /// </summary>
        public bool IsServerRunning(string serverId)
        {
            return _servers.ContainsKey(serverId);
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
            var runningServers = _servers.Keys.ToList();
            foreach (var serverId in runningServers)
            {
                await StopServerAsync(serverId);
            }
        }
    }
}
