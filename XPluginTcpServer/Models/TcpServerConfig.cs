using System;
using System.Collections.Generic;

namespace XPluginTcpServer.Models
{
    /// <summary>
    /// TCP服务器配置类
    /// </summary>
    public class TcpServerConfig
    {
        /// <summary>
        /// 服务器唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 服务器名称
        /// </summary>
        public string Name { get; set; } = "TCP服务器";

        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// 是否自动启动
        /// </summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnections { get; set; } = 1000;

        /// <summary>
        /// 缓冲区大小
        /// </summary>
        public int BufferSize { get; set; } = 1024;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// 服务器描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 是否当前正在运行
        /// </summary>
        public bool IsRunning { get; set; } = false;

        /// <summary>
        /// 当前连接数
        /// </summary>
        public int CurrentConnections { get; set; } = 0;

        /// <summary>
        /// 总连接次数
        /// </summary>
        public long TotalConnections { get; set; } = 0;

        /// <summary>
        /// 克隆配置
        /// </summary>
        public TcpServerConfig Clone()
        {
            return new TcpServerConfig
            {
                Id = this.Id,
                Name = this.Name,
                IpAddress = this.IpAddress,
                Port = this.Port,
                AutoStart = this.AutoStart,
                EnableLogging = this.EnableLogging,
                MaxConnections = this.MaxConnections,
                BufferSize = this.BufferSize,
                CreatedTime = this.CreatedTime,
                LastModified = DateTime.Now,
                Description = this.Description,
                IsRunning = this.IsRunning,
                CurrentConnections = this.CurrentConnections,
                TotalConnections = this.TotalConnections
            };
        }
    }

    /// <summary>
    /// 客户端连接信息
    /// </summary>
    public class ClientConnectionInfo
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; set; } = "";

        /// <summary>
        /// 客户端IP地址
        /// </summary>
        public string ClientIp { get; set; } = "";

        /// <summary>
        /// 客户端端口
        /// </summary>
        public int ClientPort { get; set; }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 断开时间
        /// </summary>
        public DateTime? DisconnectedTime { get; set; }

        /// <summary>
        /// 是否仍然连接
        /// </summary>
        public bool IsConnected { get; set; } = true;

        /// <summary>
        /// 发送字节数
        /// </summary>
        public long BytesSent { get; set; } = 0;

        /// <summary>
        /// 接收字节数
        /// </summary>
        public long BytesReceived { get; set; } = 0;

        /// <summary>
        /// 连接持续时间
        /// </summary>
        public TimeSpan Duration => (DisconnectedTime ?? DateTime.Now) - ConnectedTime;
    }
}
