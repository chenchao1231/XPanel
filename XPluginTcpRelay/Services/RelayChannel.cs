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
    /// 中转通道 - 实现单个路由规则的数据中转
    /// 架构：数据源A(TCP Server) ← 本系统(Client/Server) → 消费端C(TCP Client)
    /// </summary>
    public class RelayChannel : IDisposable
    {
        private readonly RouteRule _rule;
        private readonly CancellationToken _cancellationToken;
        private readonly ConcurrentDictionary<string, TcpClient> _consumerClients;
        
        private TcpListener? _localServer;
        private TcpClient? _dataSourceClient;
        private NetworkStream? _dataSourceStream;
        private bool _isRunning;
        private bool _disposed;
        private Task? _serverTask;
        private Task? _dataSourceTask;
        private Task? _reconnectTask;

        public event EventHandler<ConnectionEventArgs>? ConnectionChanged;
        public event EventHandler<DataTransferEventArgs>? DataTransferred;
        public event EventHandler<string>? LogMessage;

        public RelayChannel(RouteRule rule, CancellationToken cancellationToken)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _cancellationToken = cancellationToken;
            _consumerClients = new ConcurrentDictionary<string, TcpClient>();
        }

        /// <summary>
        /// 启动中转通道
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                if (_isRunning) return false;

                // 1. 启动本地服务器供消费端连接
                if (!await StartLocalServerAsync())
                {
                    LogMessage?.Invoke(this, $"启动本地服务器失败: {_rule.LocalServerPort}");
                    return false;
                }

                // 2. 连接到数据源
                if (!await ConnectToDataSourceAsync())
                {
                    LogMessage?.Invoke(this, $"连接数据源失败: {_rule.DataSourceEndpoint}");
                    await StopAsync();
                    return false;
                }

                _isRunning = true;
                LogMessage?.Invoke(this, $"中转通道启动成功: {_rule.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"启动中转通道异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动本地服务器
        /// </summary>
        private async Task<bool> StartLocalServerAsync()
        {
            try
            {
                _localServer = new TcpListener(IPAddress.Any, _rule.LocalServerPort);
                _localServer.Start();

                LogMessage?.Invoke(this, $"本地服务器启动成功，监听端口: {_rule.LocalServerPort}");

                // 开始接受消费端连接
                _serverTask = Task.Run(AcceptConsumerConnectionsAsync, _cancellationToken);
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"启动本地服务器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 连接到数据源
        /// </summary>
        private async Task<bool> ConnectToDataSourceAsync()
        {
            try
            {
                _dataSourceClient = new TcpClient();
                await _dataSourceClient.ConnectAsync(_rule.DataSourceIp, _rule.DataSourcePort);
                _dataSourceStream = _dataSourceClient.GetStream();

                var connection = new ConnectionInfo
                {
                    Type = ConnectionType.DataSourceClient,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse(_rule.DataSourceIp), _rule.DataSourcePort),
                    LocalEndPoint = (IPEndPoint?)_dataSourceClient.Client.LocalEndPoint,
                    Status = ConnectionStatus.Connected,
                    ConnectedTime = DateTime.Now,
                    RouteRuleId = _rule.Id
                };

                ConnectionChanged?.Invoke(this, new ConnectionEventArgs 
                { 
                    Connection = connection, 
                    IsConnected = true,
                    Message = $"已连接到数据源: {_rule.DataSourceEndpoint}"
                });

                // 开始接收数据源数据
                _dataSourceTask = Task.Run(() => ReceiveFromDataSourceAsync(connection), _cancellationToken);

                LogMessage?.Invoke(this, $"已连接到数据源: {_rule.DataSourceEndpoint}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"连接数据源失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 接受消费端连接
        /// </summary>
        private async Task AcceptConsumerConnectionsAsync()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested && _localServer != null)
                {
                    var tcpClient = await _localServer.AcceptTcpClientAsync();
                    var connectionId = Guid.NewGuid().ToString();
                    
                    _consumerClients[connectionId] = tcpClient;

                    var connection = new ConnectionInfo
                    {
                        Id = connectionId,
                        Type = ConnectionType.ConsumerServer,
                        RemoteEndPoint = (IPEndPoint?)tcpClient.Client.RemoteEndPoint,
                        LocalEndPoint = (IPEndPoint?)tcpClient.Client.LocalEndPoint,
                        Status = ConnectionStatus.Connected,
                        ConnectedTime = DateTime.Now,
                        RouteRuleId = _rule.Id
                    };

                    ConnectionChanged?.Invoke(this, new ConnectionEventArgs 
                    { 
                        Connection = connection, 
                        IsConnected = true,
                        Message = $"消费端已连接: {connection.RemoteEndPoint}"
                    });

                    // 为每个消费端启动数据接收任务
                    _ = Task.Run(() => ReceiveFromConsumerAsync(connectionId, tcpClient, connection), _cancellationToken);

                    LogMessage?.Invoke(this, $"消费端连接: {connection.RemoteEndPoint}");
                }
            }
            catch (ObjectDisposedException)
            {
                // 正常关闭，不记录日志
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，正常情况，不记录日志
            }
            catch (Exception ex) when (!_cancellationToken.IsCancellationRequested)
            {
                // 只有在非取消状态下才记录异常
                LogMessage?.Invoke(this, $"接受消费端连接异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 从数据源接收数据并转发给所有消费端
        /// </summary>
        private async Task ReceiveFromDataSourceAsync(ConnectionInfo connection)
        {
            var buffer = new byte[_rule.PacketSize];
            
            try
            {
                while (!_cancellationToken.IsCancellationRequested && _dataSourceStream != null)
                {
                    var bytesRead = await _dataSourceStream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken);
                    if (bytesRead == 0) break; // 连接关闭

                    // 转发给所有消费端
                    await ForwardToAllConsumersAsync(buffer, bytesRead);

                    // 更新统计
                    connection.ReceivedBytes += bytesRead;
                    connection.ReceivedPackets++;
                    connection.LastActivityTime = DateTime.Now;

                    DataTransferred?.Invoke(this, new DataTransferEventArgs
                    {
                        ConnectionId = connection.Id,
                        BytesTransferred = bytesRead,
                        Direction = $"{_rule.DataSourceIp}:{_rule.DataSourcePort} -> 消费端客户端"
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，正常情况，不记录日志
            }
            catch (ObjectDisposedException)
            {
                // 连接已释放，正常情况，不记录日志
            }
            catch (System.IO.IOException ex) when (ex.Message.Contains("由于线程退出或应用程序请求，已中止 I/O 操作") ||
                                                   ex.Message.Contains("Unable to read data from the transport connection"))
            {
                // I/O操作被中止，通常是正常关闭，不记录日志
            }
            catch (Exception ex) when (!_cancellationToken.IsCancellationRequested)
            {
                // 只有在非取消状态下才记录异常
                LogMessage?.Invoke(this, $"从数据源接收数据异常: {ex.Message}");
            }
            finally
            {
                connection.Status = ConnectionStatus.Disconnected;
                ConnectionChanged?.Invoke(this, new ConnectionEventArgs
                {
                    Connection = connection,
                    IsConnected = false,
                    Message = "数据源连接已断开"
                });

                // 启动自动重连
                if (_isRunning && !_cancellationToken.IsCancellationRequested)
                {
                    _reconnectTask = Task.Run(AutoReconnectToDataSourceAsync, _cancellationToken);
                }
            }
        }

        /// <summary>
        /// 从消费端接收数据并转发给数据源
        /// </summary>
        private async Task ReceiveFromConsumerAsync(string connectionId, TcpClient client, ConnectionInfo connection)
        {
            var buffer = new byte[_rule.PacketSize];
            
            try
            {
                var stream = client.GetStream();
                while (!_cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken);
                    if (bytesRead == 0) break; // 连接关闭

                    // 转发给数据源
                    if (_dataSourceStream != null)
                    {
                        await _dataSourceStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
                    }

                    // 更新统计
                    connection.SentBytes += bytesRead;
                    connection.SentPackets++;
                    connection.LastActivityTime = DateTime.Now;

                    DataTransferred?.Invoke(this, new DataTransferEventArgs
                    {
                        ConnectionId = connectionId,
                        BytesTransferred = bytesRead,
                        Direction = $"消费端客户端 -> {_rule.DataSourceIp}:{_rule.DataSourcePort}"
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，正常情况，不记录日志
            }
            catch (ObjectDisposedException)
            {
                // 连接已释放，正常情况，不记录日志
            }
            catch (System.IO.IOException ex) when (ex.Message.Contains("由于线程退出或应用程序请求，已中止 I/O 操作") ||
                                                   ex.Message.Contains("Unable to read data from the transport connection"))
            {
                // I/O操作被中止，通常是正常关闭，不记录日志
            }
            catch (Exception ex) when (!_cancellationToken.IsCancellationRequested)
            {
                // 只有在非取消状态下才记录异常
                LogMessage?.Invoke(this, $"从消费端接收数据异常: {ex.Message}");
            }
            finally
            {
                // 清理连接
                _consumerClients.TryRemove(connectionId, out _);
                client.Close();
                
                connection.Status = ConnectionStatus.Disconnected;
                ConnectionChanged?.Invoke(this, new ConnectionEventArgs 
                { 
                    Connection = connection, 
                    IsConnected = false,
                    Message = $"消费端连接已断开: {connection.RemoteEndPoint}"
                });
            }
        }

        /// <summary>
        /// 转发数据给所有消费端
        /// </summary>
        private async Task ForwardToAllConsumersAsync(byte[] data, int length)
        {
            var tasks = new List<Task>();
            
            foreach (var kvp in _consumerClients)
            {
                var client = kvp.Value;
                if (client.Connected)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var stream = client.GetStream();
                            await stream.WriteAsync(data, 0, length, _cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // 操作被取消，正常情况，不记录日志
                        }
                        catch (ObjectDisposedException)
                        {
                            // 连接已释放，正常情况，不记录日志
                        }
                        catch (System.IO.IOException ex) when (ex.Message.Contains("由于线程退出或应用程序请求，已中止 I/O 操作"))
                        {
                            // I/O操作被中止，通常是正常关闭，不记录日志
                        }
                        catch (Exception ex) when (!_cancellationToken.IsCancellationRequested)
                        {
                            // 只有在非取消状态下才记录异常
                            LogMessage?.Invoke(this, $"转发数据到消费端失败: {ex.Message}");
                        }
                    }));
                }
            }
            
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// 自动重连到数据源
        /// </summary>
        private async Task AutoReconnectToDataSourceAsync()
        {
            var reconnectDelay = 5000; // 5秒重连间隔
            var maxReconnectAttempts = 10;
            var reconnectAttempts = 0;

            while (_isRunning && !_cancellationToken.IsCancellationRequested && reconnectAttempts < maxReconnectAttempts)
            {
                try
                {
                    await Task.Delay(reconnectDelay, _cancellationToken);

                    LogMessage?.Invoke(this, $"尝试重连数据源: {_rule.DataSourceEndpoint} (第{reconnectAttempts + 1}次)");

                    if (await ConnectToDataSourceAsync())
                    {
                        LogMessage?.Invoke(this, $"数据源重连成功: {_rule.DataSourceEndpoint}");
                        return;
                    }

                    reconnectAttempts++;
                    reconnectDelay = Math.Min(reconnectDelay * 2, 30000); // 最大30秒间隔
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"重连数据源异常: {ex.Message}");
                    reconnectAttempts++;
                }
            }

            if (reconnectAttempts >= maxReconnectAttempts)
            {
                LogMessage?.Invoke(this, $"数据源重连失败，已达到最大重试次数: {_rule.DataSourceEndpoint}");
            }
        }

        /// <summary>
        /// 停止中转通道
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _isRunning = false;

                // 停止本地服务器
                _localServer?.Stop();

                // 关闭数据源连接
                _dataSourceStream?.Close();
                _dataSourceClient?.Close();

                // 关闭所有消费端连接
                foreach (var client in _consumerClients.Values)
                {
                    client.Close();
                }
                _consumerClients.Clear();

                // 等待任务完成
                if (_serverTask != null)
                {
                    await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                if (_dataSourceTask != null)
                {
                    await _dataSourceTask.WaitAsync(TimeSpan.FromSeconds(5));
                }

                LogMessage?.Invoke(this, $"中转通道已停止: {_rule.Name}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"停止中转通道异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Task.Run(async () => await StopAsync()).Wait(1000);
                _disposed = true;
            }
        }
    }
}
