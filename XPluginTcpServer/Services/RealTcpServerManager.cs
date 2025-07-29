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
    /// 真实的TCP服务器管理器 - 高可用实现
    /// </summary>
    public class RealTcpServerManager
    {
        private readonly ConcurrentDictionary<string, TcpServerInstance> _servers;
        private readonly ConcurrentDictionary<string, List<ClientConnectionInfo>> _connections;
        private readonly ConfigManager _configManager;

        public RealTcpServerManager(ConfigManager configManager)
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
        /// TCP服务器实例
        /// </summary>
        private class TcpServerInstance
        {
            public TcpListener Listener { get; set; } = null!;
            public CancellationTokenSource CancellationTokenSource { get; set; } = null!;
            public Task AcceptTask { get; set; } = null!;
            public ConcurrentDictionary<string, TcpClient> Clients { get; set; } = new();
            public bool IsRunning { get; set; }
        }

        /// <summary>
        /// 启动TCP服务器
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
                    CancellationTokenSource = cancellationTokenSource,
                    IsRunning = true
                };

                try
                {
                    // 启动监听
                    listener.Start(config.MaxConnections);
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
                catch (SocketException ex)
                {
                    Log.Error($"TCP服务器启动失败 - 端口 {config.Port} 可能被占用或地址无效: {ex.Message}");
                    Log.Error($"错误详情: ErrorCode={ex.ErrorCode}, SocketErrorCode={ex.SocketErrorCode}");

                    // 清理资源
                    try
                    {
                        listener.Stop();
                    }
                    catch { }

                    cancellationTokenSource.Dispose();
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error($"TCP服务器启动失败: {ex.Message}");

                    // 清理资源
                    try
                    {
                        listener.Stop();
                    }
                    catch { }

                    cancellationTokenSource.Dispose();
                    return false;
                }
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
                while (!cancellationToken.IsCancellationRequested && serverInstance.IsRunning)
                {
                    var tcpClient = await serverInstance.Listener.AcceptTcpClientAsync();
                    var clientEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                    
                    if (clientEndPoint != null)
                    {
                        var connectionId = Guid.NewGuid().ToString("N")[..8];
                        var connectionInfo = new ClientConnectionInfo
                        {
                            ConnectionId = connectionId,
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
                                lock (list)
                                {
                                    list.Add(connectionInfo);
                                }
                                return list;
                            });

                        // 添加到服务器实例的客户端字典
                        serverInstance.Clients.TryAdd(connectionId, tcpClient);

                        // 更新统计信息
                        _configManager.IncrementTotalConnections(serverId);
                        var currentCount = GetCurrentConnectionCount(serverId);
                        _configManager.UpdateServerStatus(serverId, true, currentCount);

                        Log.Info($"客户端已连接: {connectionInfo.ClientIp}:{connectionInfo.ClientPort} -> {config.Name} (ID: {connectionId})");
                        
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
                Log.Info($"TCP服务器监听已停止: {config.Name}");
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

                // 发送欢迎消息
                var welcomeMessage = $"欢迎连接到 {config?.Name ?? "TCP服务器"}！连接ID: {connectionInfo.ConnectionId}\r\n";
                var welcomeBytes = Encoding.UTF8.GetBytes(welcomeMessage);
                await stream.WriteAsync(welcomeBytes, 0, welcomeBytes.Length, cancellationToken);
                connectionInfo.BytesSent += welcomeBytes.Length;

                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        // 客户端正常断开连接
                        Log.Info($"客户端正常断开连接: {connectionInfo.ClientIp}:{connectionInfo.ClientPort}");
                        break;
                    }

                    // 更新接收字节数
                    connectionInfo.BytesReceived += bytesRead;

                    // 处理接收到的数据
                    var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Log.Debug($"收到数据 from {connectionInfo.ClientIp}:{connectionInfo.ClientPort}: {receivedData.Trim()}");

                    // 回显处理
                    var response = $"[{DateTime.Now:HH:mm:ss}] 服务器回显: {receivedData}";
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
                // 清理客户端连接
                await CleanupClientConnectionAsync(serverId, connectionInfo, tcpClient, stream);
            }
        }

        /// <summary>
        /// 清理客户端连接
        /// </summary>
        private async Task CleanupClientConnectionAsync(string serverId, ClientConnectionInfo connectionInfo, TcpClient tcpClient, NetworkStream? stream)
        {
            try
            {
                // 关闭网络流和TCP客户端
                stream?.Close();
                tcpClient.Close();

                // 从服务器实例的客户端字典中移除
                if (_servers.TryGetValue(serverId, out var serverInstance))
                {
                    serverInstance.Clients.TryRemove(connectionInfo.ConnectionId, out _);
                }

                // 更新连接状态
                connectionInfo.IsConnected = false;
                connectionInfo.DisconnectedTime = DateTime.Now;

                // 更新连接数
                var currentCount = GetCurrentConnectionCount(serverId);
                _configManager.UpdateServerStatus(serverId, _servers.ContainsKey(serverId), currentCount);

                var config = _configManager.GetConfig(serverId);
                Log.Info($"客户端连接已清理: {connectionInfo.ClientIp}:{connectionInfo.ClientPort} -> {config?.Name}, 持续时间: {connectionInfo.Duration}");
                
                // 触发事件
                ClientConnectionChanged?.Invoke(serverId, connectionInfo, false);
            }
            catch (Exception ex)
            {
                Log.Error($"清理客户端连接时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止TCP服务器
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

                var config = _configManager.GetConfig(serverId);
                Log.Info($"开始停止TCP服务器: {config?.Name}");

                // 标记服务器为停止状态
                serverInstance.IsRunning = false;

                // 停止接受新连接
                serverInstance.CancellationTokenSource.Cancel();

                // 停止监听器
                serverInstance.Listener.Stop();

                // 断开所有客户端连接
                var clientsToDisconnect = serverInstance.Clients.Values.ToList();
                foreach (var client in clientsToDisconnect)
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

                // 等待接受任务完成
                try
                {
                    await serverInstance.AcceptTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    Log.Error("等待接受任务完成超时");
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
                    lock (connectionList)
                    {
                        foreach (var connection in connectionList.Where(c => c.IsConnected))
                        {
                            connection.IsConnected = false;
                            connection.DisconnectedTime = DateTime.Now;
                            ClientConnectionChanged?.Invoke(serverId, connection, false);
                        }
                    }
                }

                _configManager.UpdateServerStatus(serverId, false, 0);

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
            Log.Info($"开始重启服务器: {serverId}");
            await StopServerAsync(serverId);
            await Task.Delay(1000); // 等待1秒确保资源完全释放
            return await StartServerAsync(serverId);
        }

        /// <summary>
        /// 踢出指定客户端
        /// </summary>
        public async Task<bool> KickClientAsync(string serverId, string connectionId)
        {
            try
            {
                if (!_servers.TryGetValue(serverId, out var serverInstance))
                {
                    Log.Error($"服务器未运行: {serverId}");
                    return false;
                }

                if (!serverInstance.Clients.TryRemove(connectionId, out var tcpClient))
                {
                    Log.Error($"未找到客户端连接: {connectionId}");
                    return false;
                }

                // 关闭客户端连接
                tcpClient.Close();

                // 更新连接信息
                if (_connections.TryGetValue(serverId, out var connectionList))
                {
                    lock (connectionList)
                    {
                        var connectionInfo = connectionList.FirstOrDefault(c => c.ConnectionId == connectionId);
                        if (connectionInfo != null)
                        {
                            connectionInfo.IsConnected = false;
                            connectionInfo.DisconnectedTime = DateTime.Now;

                            Log.Info($"已踢出客户端: {connectionInfo.ClientIp}:{connectionInfo.ClientPort} (ID: {connectionId})");
                            ClientConnectionChanged?.Invoke(serverId, connectionInfo, false);
                        }
                    }
                }

                // 更新连接数
                var currentCount = GetCurrentConnectionCount(serverId);
                _configManager.UpdateServerStatus(serverId, true, currentCount);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"踢出客户端失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查服务器是否运行
        /// </summary>
        public bool IsServerRunning(string serverId)
        {
            return _servers.ContainsKey(serverId) && _servers[serverId].IsRunning;
        }

        /// <summary>
        /// 获取服务器连接信息
        /// </summary>
        public List<ClientConnectionInfo> GetServerConnections(string serverId)
        {
            if (_connections.TryGetValue(serverId, out var connectionList))
            {
                lock (connectionList)
                {
                    return connectionList.ToList();
                }
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
                lock (connectionList)
                {
                    return connectionList.Count(c => c.IsConnected);
                }
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
                await Task.Delay(500); // 间隔启动，避免端口冲突
            }
        }

        /// <summary>
        /// 停止所有服务器
        /// </summary>
        public async Task StopAllServersAsync()
        {
            var runningServers = _servers.Keys.ToList();
            var stopTasks = runningServers.Select(serverId => StopServerAsync(serverId));
            await Task.WhenAll(stopTasks);
            Log.Info("所有TCP服务器已停止");
        }

        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        public async Task<bool> SendMessageToClientAsync(string serverId, string connectionId, string message)
        {
            try
            {
                if (!_servers.TryGetValue(serverId, out var serverInstance))
                {
                    return false;
                }

                if (!serverInstance.Clients.TryGetValue(connectionId, out var tcpClient))
                {
                    return false;
                }

                var stream = tcpClient.GetStream();
                var messageBytes = Encoding.UTF8.GetBytes(message + "\r\n");
                await stream.WriteAsync(messageBytes, 0, messageBytes.Length);

                // 更新发送字节数
                if (_connections.TryGetValue(serverId, out var connectionList))
                {
                    lock (connectionList)
                    {
                        var connectionInfo = connectionList.FirstOrDefault(c => c.ConnectionId == connectionId);
                        if (connectionInfo != null)
                        {
                            connectionInfo.BytesSent += messageBytes.Length;
                        }
                    }
                }

                Log.Debug($"向客户端发送消息: {connectionId} -> {message}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"向客户端发送消息失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 广播消息到所有客户端
        /// </summary>
        public async Task<int> BroadcastMessageAsync(string serverId, string message)
        {
            try
            {
                if (!_servers.TryGetValue(serverId, out var serverInstance))
                {
                    return 0;
                }

                int sentCount = 0;
                var clients = serverInstance.Clients.ToList();

                foreach (var kvp in clients)
                {
                    try
                    {
                        var stream = kvp.Value.GetStream();
                        var messageBytes = Encoding.UTF8.GetBytes($"[广播] {message}\r\n");
                        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                        sentCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"向客户端 {kvp.Key} 发送广播消息失败: {ex.Message}");
                    }
                }

                Log.Info($"广播消息到 {sentCount} 个客户端: {message}");
                return sentCount;
            }
            catch (Exception ex)
            {
                Log.Error($"广播消息失败: {ex.Message}");
                return 0;
            }
        }
    }
}
