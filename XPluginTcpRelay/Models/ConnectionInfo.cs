using System;
using System.Net;

namespace XPluginTcpRelay.Models
{
    /// <summary>
    /// 连接信息模型
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 连接类型（A方或C方）
        /// </summary>
        public ConnectionType Type { get; set; }

        /// <summary>
        /// 远程端点
        /// </summary>
        public IPEndPoint? RemoteEndPoint { get; set; }

        /// <summary>
        /// 本地端点
        /// </summary>
        public IPEndPoint? LocalEndPoint { get; set; }

        /// <summary>
        /// 连接状态
        /// </summary>
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 连接建立时间
        /// </summary>
        public DateTime ConnectedTime { get; set; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public DateTime LastActivityTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 接收的字节数
        /// </summary>
        public long ReceivedBytes { get; set; } = 0;

        /// <summary>
        /// 发送的字节数
        /// </summary>
        public long SentBytes { get; set; } = 0;

        /// <summary>
        /// 接收的数据包数
        /// </summary>
        public long ReceivedPackets { get; set; } = 0;

        /// <summary>
        /// 发送的数据包数
        /// </summary>
        public long SentPackets { get; set; } = 0;

        /// <summary>
        /// 关联的路由规则ID
        /// </summary>
        public string? RouteRuleId { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReconnectCount { get; set; } = 0;

        /// <summary>
        /// 获取连接持续时间
        /// </summary>
        public TimeSpan Duration => Status == ConnectionStatus.Connected 
            ? DateTime.Now - ConnectedTime 
            : TimeSpan.Zero;

        /// <summary>
        /// 获取显示名称
        /// </summary>
        public string DisplayName => $"{Type}_{RemoteEndPoint}";

        public override string ToString()
        {
            return $"{Type}: {RemoteEndPoint} [{Status}]";
        }
    }

    /// <summary>
    /// 连接类型
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// 到数据源的客户端连接（本系统作为Client连接A方）
        /// </summary>
        DataSourceClient,

        /// <summary>
        /// 来自消费端的服务端连接（本系统作为Server接受C方连接）
        /// </summary>
        ConsumerServer
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// 已断开
        /// </summary>
        Disconnected,

        /// <summary>
        /// 连接中
        /// </summary>
        Connecting,

        /// <summary>
        /// 已连接
        /// </summary>
        Connected,

        /// <summary>
        /// 重连中
        /// </summary>
        Reconnecting,

        /// <summary>
        /// 错误
        /// </summary>
        Error
    }
}
