using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using XPlugin.Configuration;
using XPlugin.Network;

namespace XPluginTcpRelay.Models
{
    /// <summary>
    /// TCP数据中转平台路由规则模型 - 实现标准接口
    /// 架构：数据源A(TCP Server) ← 本系统(Client/Server) → 消费端C(TCP Client)
    /// </summary>
    public class RouteRule : IRelayRule, IValidatableConfiguration, ICloneableConfiguration<RouteRule>
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

        #region IRelayRule 接口实现

        /// <summary>
        /// 源端点（数据源A方）
        /// </summary>
        [JsonIgnore]
        public IPEndPoint SourceEndPoint => new IPEndPoint(IPAddress.Parse(DataSourceIp), DataSourcePort);

        /// <summary>
        /// 目标端点（本地服务端口，C方连接的目标）
        /// </summary>
        [JsonIgnore]
        public IPEndPoint TargetEndPoint => new IPEndPoint(IPAddress.Any, LocalServerPort);

        #endregion

        #region IValidatableConfiguration 接口实现

        /// <summary>
        /// 验证规则配置是否有效
        /// </summary>
        public bool IsValid()
        {
            var result = Validate();
            return result.IsValid;
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        /// <returns>验证结果</returns>
        public ConfigurationValidationResult Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // 验证名称
            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("规则名称不能为空");

            // 验证数据源IP和端口
            if (string.IsNullOrWhiteSpace(DataSourceIp))
                errors.Add("数据源IP地址不能为空");
            else if (!IPAddress.TryParse(DataSourceIp, out _))
                errors.Add("数据源IP地址格式无效");

            if (DataSourcePort <= 0 || DataSourcePort > 65535)
                errors.Add("数据源端口号必须在1-65535范围内");

            // 验证本地服务端口
            if (LocalServerPort <= 0 || LocalServerPort > 65535)
                errors.Add("本地服务端口号必须在1-65535范围内");

            // 验证最大连接数
            if (MaxConsumerConnections <= 0)
                errors.Add("最大消费端连接数必须大于0");

            // 验证数据类型
            if (!string.IsNullOrWhiteSpace(DataType) &&
                DataType != "realtime" && DataType != "unrealtime")
                warnings.Add("数据类型建议使用 'realtime' 或 'unrealtime'");

            // 验证报文大小
            if (PacketSize <= 0 || PacketSize > 1024 * 1024) // 最大1MB
                warnings.Add("报文传输大小建议在1-1048576字节范围内");

            // 警告检查
            if (string.IsNullOrWhiteSpace(Description))
                warnings.Add("建议添加规则描述");

            return errors.Count > 0
                ? ConfigurationValidationResult.Failure(errors, warnings)
                : ConfigurationValidationResult.Success(warnings);
        }

        #endregion

        #region ICloneableConfiguration 接口实现

        /// <summary>
        /// 克隆规则
        /// </summary>
        /// <returns>克隆的规则</returns>
        public RouteRule Clone()
        {
            return new RouteRule
            {
                Id = this.Id,
                Name = this.Name,
                DataSourceIp = this.DataSourceIp,
                DataSourcePort = this.DataSourcePort,
                LocalServerPort = this.LocalServerPort,
                DataType = this.DataType,
                IsEnabled = this.IsEnabled,
                Description = this.Description,
                MaxConsumerConnections = this.MaxConsumerConnections,
                EnableBuffering = this.EnableBuffering,
                PacketSize = this.PacketSize,
                CreatedTime = this.CreatedTime,
                LastModified = DateTime.Now
            };
        }

        #endregion

        public override string ToString()
        {
            return $"{Name}: {DataSourceEndpoint} ← TDP → :{LocalServerPort} ({DataType})";
        }
    }
}
