using System;
using Newtonsoft.Json;

namespace XPluginTcpRelay.Models
{
    /// <summary>
    /// 路由规则模型
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
        /// A方（数据源）IP地址
        /// </summary>
        public string ASourceIp { get; set; } = "192.168.1.100";

        /// <summary>
        /// A方（数据源）端口
        /// </summary>
        public int ASourcePort { get; set; } = 8080;

        /// <summary>
        /// C方（目标）IP地址
        /// </summary>
        public string CTargetIp { get; set; } = "10.0.0.50";

        /// <summary>
        /// C方（目标）端口
        /// </summary>
        public int CTargetPort { get; set; } = 8080;

        /// <summary>
        /// 是否启用此规则
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 规则描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

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
        /// 获取A方端点字符串
        /// </summary>
        public string AEndpoint => $"{ASourceIp}:{ASourcePort}";

        /// <summary>
        /// 获取C方端点字符串
        /// </summary>
        public string CEndpoint => $"{CTargetIp}:{CTargetPort}";

        /// <summary>
        /// 验证规则配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Name) &&
                   !string.IsNullOrEmpty(ASourceIp) &&
                   !string.IsNullOrEmpty(CTargetIp) &&
                   ASourcePort > 0 && ASourcePort <= 65535 &&
                   CTargetPort > 0 && CTargetPort <= 65535;
        }

        public override string ToString()
        {
            return $"{Name}: {AEndpoint} → {CEndpoint}";
        }
    }
}
