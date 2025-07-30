using System;
using System.Collections.Generic;
using System.Linq;

namespace XPluginTcpRelay.Models
{
    /// <summary>
    /// 中继系统配置模型
    /// </summary>
    public class RelayConfig
    {
        /// <summary>
        /// 系统名称
        /// </summary>
        public string SystemName { get; set; } = "TCP数据转发系统";

        /// <summary>
        /// 监听端口（B方接收A方连接的端口）
        /// </summary>
        public int ListenPort { get; set; } = 9999;

        /// <summary>
        /// 监听IP地址
        /// </summary>
        public string ListenIp { get; set; } = "0.0.0.0";

        /// <summary>
        /// 最大并发连接数
        /// </summary>
        public int MaxConnections { get; set; } = 200;

        /// <summary>
        /// 心跳间隔（秒）
        /// </summary>
        public int HeartbeatInterval { get; set; } = 60;

        /// <summary>
        /// 连接超时时间（秒）
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30;

        /// <summary>
        /// 重连间隔（秒）
        /// </summary>
        public int ReconnectInterval { get; set; } = 5;

        /// <summary>
        /// 最大重连次数
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 5;

        /// <summary>
        /// 缓冲区大小（字节）
        /// </summary>
        public int BufferSize { get; set; } = 4096;

        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// 是否启用数据审计
        /// </summary>
        public bool EnableDataAudit { get; set; } = true;

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// 是否启用性能监控
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// 统计刷新间隔（秒）
        /// </summary>
        public int StatisticsRefreshInterval { get; set; } = 5;

        /// <summary>
        /// 路由规则列表
        /// </summary>
        public List<RouteRule> RouteRules { get; set; } = new List<RouteRule>();

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return ListenPort > 0 && ListenPort <= 65535 &&
                   MaxConnections > 0 &&
                   HeartbeatInterval > 0 &&
                   ConnectionTimeout > 0 &&
                   ReconnectInterval > 0 &&
                   BufferSize > 0;
        }

        /// <summary>
        /// 获取启用的路由规则
        /// </summary>
        public IEnumerable<RouteRule> GetEnabledRules()
        {
            return RouteRules.Where(r => r.IsEnabled && r.IsValid());
        }
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
