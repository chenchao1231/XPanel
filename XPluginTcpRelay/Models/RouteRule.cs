using System;
using Newtonsoft.Json;

namespace XPluginTcpRelay.Models
{
    /// <summary>
    /// TCP数据中转平台路由规则模型
    /// 架构：数据源A(TCP Server) ← 本系统(Client/Server) → 消费端C(TCP Client)
    /// </summary>
    public class RouteRule
    {
        /// <summary>
        /// 规则ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 规则名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数据源A方的IP地址（本系统作为Client主动连接的目标）
        /// </summary>
        public string DataSourceIp { get; set; } = "192.168.1.100";

        /// <summary>
        /// 数据源A方的端口（本系统作为Client主动连接的目标）
        /// </summary>
        public int DataSourcePort { get; set; } = 8080;

        /// <summary>
        /// 本系统提供给消费端C方的监听端口（本系统作为Server）
        /// </summary>
        public int LocalServerPort { get; set; } = 9999;

        /// <summary>
        /// 数据类型（realtime/unrealtime）
        /// </summary>
        public string DataType { get; set; } = "realtime";

        /// <summary>
        /// 是否启用此规则
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 规则描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 最大消费端连接数
        /// </summary>
        public int MaxConsumerConnections { get; set; } = 100;

        /// <summary>
        /// 是否启用缓冲队列（用于非实时数据）
        /// </summary>
        public bool EnableBuffering { get; set; } = false;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 报文传输大小（字节）
        /// </summary>
        public int PacketSize { get; set; } = 4096;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// 转发的数据包数量
        /// </summary>
        [JsonIgnore]
        public long ForwardedPackets { get; set; } = 0;

        /// <summary>
        /// 转发的数据字节数
        /// </summary>
        [JsonIgnore]
        public long ForwardedBytes { get; set; } = 0;

        /// <summary>
        /// 当前活跃的消费端连接数
        /// </summary>
        [JsonIgnore]
        public int ActiveConsumerConnections { get; set; } = 0;

        /// <summary>
        /// 数据源连接状态
        /// </summary>
        [JsonIgnore]
        public bool IsDataSourceConnected { get; set; } = false;

        /// <summary>
        /// 获取数据源端点字符串
        /// </summary>
        public string DataSourceEndpoint => $"{DataSourceIp}:{DataSourcePort}";

        /// <summary>
        /// 获取本地服务端点字符串
        /// </summary>
        public string LocalServerEndpoint => $"0.0.0.0:{LocalServerPort}";

        /// <summary>
        /// 验证规则配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Name) &&
                   !string.IsNullOrEmpty(DataSourceIp) &&
                   DataSourcePort > 0 && DataSourcePort <= 65535 &&
                   LocalServerPort > 0 && LocalServerPort <= 65535 &&
                   MaxConsumerConnections > 0;
        }

        public override string ToString()
        {
            return $"{Name}: {DataSourceEndpoint} ← TDP → :{LocalServerPort} ({DataType})";
        }
    }
}
