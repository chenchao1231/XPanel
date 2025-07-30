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
    /// TCP中继管理器 - 核心转发引擎
    /// </summary>
    public class TcpRelayManager : IDisposable
    {
        private readonly ConfigManager _configManager;
        private readonly ConcurrentDictionary<string, ConnectionInfo> _connections;
        private readonly ConcurrentDictionary<string, Socket> _sockets;
        private Socket? _listenSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<ConnectionInfo>? ConnectionStatusChanged;

        /// <summary>
        /// 数据转发事件
        /// </summary>
        public event Action<string, byte[], string>? DataForwarded;

        /// <summary>
        /// 日志事件
        /// </summary>
        public event Action<string>? LogMessage;

        public TcpRelayManager(ConfigManager configManager)
        {
            _configManager = configManager;
            _connections = new ConcurrentDictionary<string, ConnectionInfo>();
            _sockets = new ConcurrentDictionary<string, Socket>();
        }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取所有连接信息
        /// </summary>
        public IEnumerable<ConnectionInfo> GetAllConnections()
        {
            return _connections.Values.ToList();
        }

        /// <summary>
        /// 启动中继服务
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isRunning)
            {
                LogMessage?.Invoke("中继服务已在运行中");
                return false;
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var config = _configManager.Config;

                // 创建监听Socket
                _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                var listenEndPoint = new IPEndPoint(IPAddress.Parse(config.ListenIp), config.ListenPort);
                _listenSocket.Bind(listenEndPoint);
                _listenSocket.Listen(config.MaxConnections);

                _isRunning = true;
                LogMessage?.Invoke($"TCP中继服务已启动，监听 {listenEndPoint}");

                // 开始接受连接
                _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"启动中继服务失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止中继服务
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            try
            {
                _isRunning = false;
                _cancellationTokenSource?.Cancel();

                // 关闭监听Socket
                _listenSocket?.Close();
                _listenSocket?.Dispose();
                _listenSocket = null;

                // 关闭所有连接
                foreach (var socket in _sockets.Values)
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Close();
                    }
                    catch { }
                }

                _sockets.Clear();
                _connections.Clear();

                LogMessage?.Invoke("TCP中继服务已停止");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"停止中继服务时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 接受连接的异步方法
        /// </summary>
        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    if (_listenSocket == null) break;

                    var clientSocket = await AcceptAsync(_listenSocket);
                    if (clientSocket != null)
                    {
                        _ = Task.Run(() => HandleClientConnectionAsync(clientSocket, cancellationToken));
                    }
                }
                catch (ObjectDisposedException)
                {
                    break; // Socket已被释放
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"接受连接时发生错误: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // 等待1秒后重试
                }
            }
        }

        /// <summary>
        /// Socket异步接受连接的包装方法
        /// </summary>
        private Task<Socket> AcceptAsync(Socket socket)
        {
            return Task.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        private async Task HandleClientConnectionAsync(Socket clientSocket, CancellationToken cancellationToken)
        {
            var connectionId = Guid.NewGuid().ToString();
            ConnectionInfo? connectionInfo = null;

            try
            {
                var remoteEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint!;
                LogMessage?.Invoke($"新的A方连接: {remoteEndPoint}");

                // 查找匹配的路由规则
                var routeRule = _configManager.Config.RouteRules
                    .FirstOrDefault(r => r.IsEnabled && r.ASourceIp == remoteEndPoint.Address.ToString());

                if (routeRule == null)
                {
                    LogMessage?.Invoke($"未找到匹配的路由规则，拒绝连接: {remoteEndPoint}");
                    clientSocket.Close();
                    return;
                }

                // 创建连接信息
                connectionInfo = new ConnectionInfo
                {
                    Id = connectionId,
                    Type = ConnectionType.ASource,
                    RemoteEndPoint = remoteEndPoint,
                    LocalEndPoint = (IPEndPoint)clientSocket.LocalEndPoint!,
                    Status = ConnectionStatus.Connected,
                    ConnectedTime = DateTime.Now,
                    RouteRuleId = routeRule.Id
                };

                _connections[connectionId] = connectionInfo;
                _sockets[connectionId] = clientSocket;
                ConnectionStatusChanged?.Invoke(connectionInfo);

                // 建立到C方的连接
                var targetSocket = await ConnectToCTargetAsync(routeRule, cancellationToken);
                if (targetSocket == null)
                {
                    LogMessage?.Invoke($"无法连接到C方目标: {routeRule.CEndpoint}");
                    clientSocket.Close();
                    return;
                }

                var targetConnectionId = Guid.NewGuid().ToString();
                var targetConnectionInfo = new ConnectionInfo
                {
                    Id = targetConnectionId,
                    Type = ConnectionType.CTarget,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse(routeRule.CTargetIp), routeRule.CTargetPort),
                    LocalEndPoint = (IPEndPoint)targetSocket.LocalEndPoint!,
                    Status = ConnectionStatus.Connected,
                    ConnectedTime = DateTime.Now,
                    RouteRuleId = routeRule.Id
                };

                _connections[targetConnectionId] = targetConnectionInfo;
                _sockets[targetConnectionId] = targetSocket;
                ConnectionStatusChanged?.Invoke(targetConnectionInfo);

                // 开始双向数据转发
                var forwardTask1 = ForwardDataAsync(clientSocket, targetSocket, connectionInfo, targetConnectionInfo, cancellationToken);
                var forwardTask2 = ForwardDataAsync(targetSocket, clientSocket, targetConnectionInfo, connectionInfo, cancellationToken);

                await Task.WhenAny(forwardTask1, forwardTask2);

                LogMessage?.Invoke($"连接已断开: {remoteEndPoint}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"处理连接时发生错误: {ex.Message}");
            }
            finally
            {
                // 清理连接
                if (connectionInfo != null)
                {
                    connectionInfo.Status = ConnectionStatus.Disconnected;
                    ConnectionStatusChanged?.Invoke(connectionInfo);
                    _connections.TryRemove(connectionId, out _);
                    _sockets.TryRemove(connectionId, out _);
                }

                try
                {
                    clientSocket.Close();
                }
                catch { }
            }
        }

        /// <summary>
        /// 连接到C方目标
        /// </summary>
        private async Task<Socket?> ConnectToCTargetAsync(RouteRule routeRule, CancellationToken cancellationToken)
        {
            try
            {
                var targetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var targetEndPoint = new IPEndPoint(IPAddress.Parse(routeRule.CTargetIp), routeRule.CTargetPort);

                await ConnectAsync(targetSocket, targetEndPoint);
                LogMessage?.Invoke($"已连接到C方目标: {routeRule.CEndpoint}");
                return targetSocket;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"连接C方目标失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Socket异步连接的包装方法
        /// </summary>
        private Task ConnectAsync(Socket socket, EndPoint endPoint)
        {
            return Task.Factory.FromAsync(
                (callback, state) => socket.BeginConnect(endPoint, callback, state),
                socket.EndConnect,
                null);
        }

        /// <summary>
        /// 数据转发
        /// </summary>
        private async Task ForwardDataAsync(Socket fromSocket, Socket toSocket, 
            ConnectionInfo fromConnection, ConnectionInfo toConnection, CancellationToken cancellationToken)
        {
            var buffer = new byte[_configManager.Config.BufferSize];

            try
            {
                while (!cancellationToken.IsCancellationRequested && fromSocket.Connected && toSocket.Connected)
                {
                    var bytesReceived = await ReceiveAsync(fromSocket, buffer);
                    if (bytesReceived == 0)
                    {
                        break; // 连接已关闭
                    }

                    var dataToForward = new byte[bytesReceived];
                    Array.Copy(buffer, dataToForward, bytesReceived);

                    await SendAsync(toSocket, dataToForward);

                    // 更新统计信息
                    fromConnection.ReceivedBytes += bytesReceived;
                    fromConnection.ReceivedPackets++;
                    toConnection.SentBytes += bytesReceived;
                    toConnection.SentPackets++;
                    fromConnection.LastActivityTime = DateTime.Now;
                    toConnection.LastActivityTime = DateTime.Now;

                    // 触发数据转发事件
                    var direction = fromConnection.Type == ConnectionType.ASource ? "A→C" : "C→A";
                    DataForwarded?.Invoke(direction, dataToForward, fromConnection.Id);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"数据转发错误: {ex.Message}");
            }
        }

        /// <summary>
        /// Socket异步接收的包装方法
        /// </summary>
        private Task<int> ReceiveAsync(Socket socket, byte[] buffer)
        {
            return Task.Factory.FromAsync(
                (callback, state) => socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, callback, state),
                socket.EndReceive,
                null);
        }

        /// <summary>
        /// Socket异步发送的包装方法
        /// </summary>
        private Task<int> SendAsync(Socket socket, byte[] data)
        {
            return Task.Factory.FromAsync(
                (callback, state) => socket.BeginSend(data, 0, data.Length, SocketFlags.None, callback, state),
                socket.EndSend,
                null);
        }

        public void Dispose()
        {
            StopAsync().Wait();
            _cancellationTokenSource?.Dispose();
        }
    }
}
