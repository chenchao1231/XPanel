using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using XPlugin.Network;
using XPlugin.Auditing;

namespace XPluginTcpRelay.Services
{
    /// <summary>
    /// 独立的数据源连接管理器
    /// 负责维护与A方数据源的持久连接，不受C方客户端影响
    /// </summary>
    public class DataSourceConnection : IDisposable
    {
        private readonly string _dataSourceIp;
        private readonly int _dataSourcePort;
        private readonly IAuditService? _auditService;
        private TcpClient? _tcpClient;
        private NetworkStream? _networkStream;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;
        private bool _disposed = false;
        private DateTime _lastActivityTime = DateTime.Now;
        private readonly Timer _reconnectTimer;
        private readonly ConcurrentQueue<byte[]> _dataBuffer = new();
        private readonly object _lockObject = new();

        // 连接状态
        public DataSourceConnectionStatus Status { get; private set; } = DataSourceConnectionStatus.Disconnected;
        public string EndPoint => $"{_dataSourceIp}:{_dataSourcePort}";
        public DateTime LastConnectedTime { get; private set; }
        public int ReconnectAttempts { get; private set; } = 0;

        // 事件
        public event EventHandler<DataSourceConnectionStatusEventArgs>? ConnectionStatusChanged;
        public event EventHandler<string>? LogMessage;
        public event EventHandler<DataReceivedEventArgs>? DataReceived;

        public DataSourceConnection(string dataSourceIp, int dataSourcePort, IAuditService? auditService = null)
        {
            _dataSourceIp = dataSourceIp;
            _dataSourcePort = dataSourcePort;
            _auditService = auditService;
            
            // 初始化重连定时器（每5秒检查一次）
            _reconnectTimer = new Timer(CheckConnectionAndReconnect, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// 启动数据源连接
        /// </summary>
        public async Task<bool> StartAsync(CancellationToken cancellationToken)
        {
            if (_isRunning) return true;

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isRunning = true;

            LogMessage?.Invoke(this, $"启动数据源连接管理器: {EndPoint}");

            // 立即尝试连接
            _ = Task.Run(async () => await ConnectToDataSourceAsync());

            return true;
        }

        /// <summary>
        /// 停止数据源连接 - 修复版本：确保真正关闭连接
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            try
            {
                lock (_lockObject)
                {
                    // 先关闭网络流
                    if (_networkStream != null)
                    {
                        try
                        {
                            _networkStream.Close();
                            _networkStream.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogMessage?.Invoke(this, $"关闭网络流异常: {ex.Message}");
                        }
                        _networkStream = null;
                    }

                    // 再关闭TCP客户端
                    if (_tcpClient != null)
                    {
                        try
                        {
                            if (_tcpClient.Connected)
                            {
                                _tcpClient.GetStream().Close();
                            }
                            _tcpClient.Close();
                            _tcpClient.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogMessage?.Invoke(this, $"关闭TCP客户端异常: {ex.Message}");
                        }
                        _tcpClient = null;
                    }
                }

                Status = DataSourceConnectionStatus.Disconnected;
                OnConnectionStatusChanged("数据源连接已完全停止");

                LogMessage?.Invoke(this, $"数据源连接管理器已停止: {EndPoint}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"停止数据源连接异常: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送数据到数据源
        /// </summary>
        public async Task<bool> SendDataAsync(byte[] data)
        {
            if (!IsConnected || _networkStream == null) return false;

            try
            {
                await _networkStream.WriteAsync(data, 0, data.Length);
                await _networkStream.FlushAsync();
                _lastActivityTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"发送数据到数据源失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否已连接
        /// </summary>
        public bool IsConnected => Status == DataSourceConnectionStatus.Connected && 
                                   _tcpClient?.Connected == true && 
                                   _networkStream != null;

        /// <summary>
        /// 连接到数据源
        /// </summary>
        private async Task ConnectToDataSourceAsync()
        {
            if (!_isRunning || _cancellationTokenSource?.Token.IsCancellationRequested == true) return;

            try
            {
                Status = DataSourceConnectionStatus.Connecting;
                OnConnectionStatusChanged($"正在连接到数据源 (第{ReconnectAttempts + 1}次尝试)");

                lock (_lockObject)
                {
                    _tcpClient?.Close();
                    _tcpClient = new TcpClient();
                    _tcpClient.ReceiveTimeout = 30000; // 30秒超时
                    _tcpClient.SendTimeout = 30000;
                }

                await _tcpClient.ConnectAsync(_dataSourceIp, _dataSourcePort);

                lock (_lockObject)
                {
                    _networkStream = _tcpClient.GetStream();
                }

                Status = DataSourceConnectionStatus.Connected;
                LastConnectedTime = DateTime.Now;
                ReconnectAttempts = 0;
                _lastActivityTime = DateTime.Now;

                OnConnectionStatusChanged("数据源连接成功");
                LogMessage?.Invoke(this, $"数据源连接成功: {EndPoint}");

                // 启动数据接收任务
                _ = Task.Run(async () => await ReceiveDataAsync());
            }
            catch (Exception ex)
            {
                Status = DataSourceConnectionStatus.Disconnected;
                ReconnectAttempts++;
                OnConnectionStatusChanged($"数据源连接失败: {ex.Message}");
                LogMessage?.Invoke(this, $"数据源连接失败: {EndPoint} - {ex.Message}");
            }
        }

        /// <summary>
        /// 接收数据源数据
        /// </summary>
        private async Task ReceiveDataAsync()
        {
            var buffer = new byte[4096];

            try
            {
                while (_isRunning && IsConnected && _cancellationTokenSource?.Token.IsCancellationRequested == false)
                {
                    var bytesRead = await _networkStream!.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token);
                    if (bytesRead == 0)
                    {
                        LogMessage?.Invoke(this, $"数据源连接已关闭: {EndPoint}");
                        break;
                    }

                    _lastActivityTime = DateTime.Now;

                    // 复制数据并触发事件
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        Data = data,
                        SourceEndPoint = EndPoint,
                        Timestamp = DateTime.Now
                    });

                    // 记录审计日志
                    if (_auditService != null)
                    {
                        try
                        {
                            await _auditService.LogDataTransferAsync(EndPoint, XPlugin.Auditing.DataDirection.Received, data);
                        }
                        catch (Exception auditEx)
                        {
                            LogMessage?.Invoke(this, $"审计日志记录失败: {auditEx.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不记录错误
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"接收数据源数据异常: {EndPoint} - {ex.Message}");
            }
            finally
            {
                if (Status == DataSourceConnectionStatus.Connected)
                {
                    Status = DataSourceConnectionStatus.Disconnected;
                    OnConnectionStatusChanged("数据源连接断开");
                }
            }
        }

        /// <summary>
        /// 检查连接状态并重连
        /// </summary>
        private void CheckConnectionAndReconnect(object? state)
        {
            if (!_isRunning || _disposed) return;

            try
            {
                // 检查连接是否仍然有效
                if (Status == DataSourceConnectionStatus.Connected)
                {
                    if (_tcpClient?.Connected != true || _networkStream == null)
                    {
                        Status = DataSourceConnectionStatus.Disconnected;
                        OnConnectionStatusChanged("检测到数据源连接断开");
                        LogMessage?.Invoke(this, $"数据源连接断开，准备重连: {EndPoint}");
                    }
                    else
                    {
                        // 检查是否超时无活动（延长到10分钟）
                        var timeSinceLastActivity = DateTime.Now - _lastActivityTime;
                        if (timeSinceLastActivity.TotalMinutes > 10) // 10分钟无活动
                        {
                            LogMessage?.Invoke(this, $"数据源连接超时无活动: {EndPoint}");
                            Status = DataSourceConnectionStatus.Disconnected;
                            OnConnectionStatusChanged("数据源连接超时");
                        }
                    }
                }

                // 如果未连接且不在连接中，则尝试重连（无限重连）
                if (Status == DataSourceConnectionStatus.Disconnected)
                {
                    // 重置重连次数，实现无限重连
                    if (ReconnectAttempts >= 50) // 每50次重置一次计数器
                    {
                        ReconnectAttempts = 0;
                        LogMessage?.Invoke(this, $"重置重连计数器，继续尝试连接: {EndPoint}");
                    }

                    _ = Task.Run(async () => await ConnectToDataSourceAsync());
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"连接检查异常: {EndPoint} - {ex.Message}");
            }
        }

        /// <summary>
        /// 触发连接状态变化事件
        /// </summary>
        private void OnConnectionStatusChanged(string message)
        {
            ConnectionStatusChanged?.Invoke(this, new DataSourceConnectionStatusEventArgs
            {
                EndPoint = EndPoint,
                Status = Status,
                Message = message,
                Timestamp = DateTime.Now
            });
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _reconnectTimer?.Dispose();
            _ = Task.Run(async () => await StopAsync());
        }
    }

    /// <summary>
    /// 数据源连接状态
    /// </summary>
    public enum DataSourceConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    /// <summary>
    /// 数据源连接状态变化事件参数
    /// </summary>
    public class DataSourceConnectionStatusEventArgs : EventArgs
    {
        public string EndPoint { get; set; } = string.Empty;
        public DataSourceConnectionStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string SourceEndPoint { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
