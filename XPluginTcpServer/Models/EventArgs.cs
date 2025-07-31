using System;

namespace XPluginTcpServer.Models
{
    /// <summary>
    /// 服务器状态变化事件参数
    /// </summary>
    public class ServerStatusEventArgs : EventArgs
    {
        /// <summary>
        /// 服务器ID
        /// </summary>
        public string ServerId { get; set; } = string.Empty;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 事件发生时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public ServerStatusEventArgs()
        {
        }

        public ServerStatusEventArgs(string serverId, bool isRunning, string message = "")
        {
            ServerId = serverId;
            IsRunning = isRunning;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 客户端连接变化事件参数
    /// </summary>
    public class ClientConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// 服务器ID
        /// </summary>
        public string ServerId { get; set; } = string.Empty;

        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 客户端端点信息
        /// </summary>
        public string ClientEndPoint { get; set; } = string.Empty;

        /// <summary>
        /// 事件发生时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 连接持续时间（仅在断开时有效）
        /// </summary>
        public TimeSpan? Duration { get; set; }

        public ClientConnectionEventArgs()
        {
        }

        public ClientConnectionEventArgs(string serverId, string connectionId, bool isConnected, string clientEndPoint = "")
        {
            ServerId = serverId;
            ConnectionId = connectionId;
            IsConnected = isConnected;
            ClientEndPoint = clientEndPoint;
            Timestamp = DateTime.Now;
        }
    }
}
