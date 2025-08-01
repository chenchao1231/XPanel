using System;
using System.Collections.Generic;
using System.Linq;
using XPlugin.Configuration;

namespace XPluginTcpRelay.Models
{
    /// <summary>
    /// 中继系统配置模型 - 实现标准配置接口
    /// </summary>
    public class RelayConfig : ConfigurationBase
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
        public new DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public new DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid()
        {
            var result = Validate();
            return result.IsValid;
        }

        /// <summary>
        /// 获取启用的路由规则
        /// </summary>
        public IEnumerable<RouteRule> GetEnabledRules()
        {
            return RouteRules.Where(r => r.IsEnabled && r.IsValid());
        }

        /// <summary>
        /// 根据ID查找规则
        /// </summary>
        /// <param name="id">规则ID</param>
        /// <returns>找到的规则，不存在返回null</returns>
        public RouteRule? FindRuleById(string id)
        {
            return RouteRules.FirstOrDefault(r => r.Id == id);
        }

        /// <summary>
        /// 添加路由规则
        /// </summary>
        /// <param name="rule">要添加的规则</param>
        /// <returns>true表示添加成功</returns>
        public bool AddRule(RouteRule rule)
        {
            if (rule == null || !rule.IsValid())
                return false;

            // 检查名称是否重复
            if (RouteRules.Any(r => r.Name == rule.Name))
                return false;

            // 检查端口是否冲突
            if (RouteRules.Any(r => r.LocalServerPort == rule.LocalServerPort))
                return false;

            RouteRules.Add(rule);
            UpdateLastModified();
            return true;
        }

        /// <summary>
        /// 更新路由规则
        /// </summary>
        /// <param name="rule">要更新的规则</param>
        /// <returns>true表示更新成功</returns>
        public bool UpdateRule(RouteRule rule)
        {
            if (rule == null || !rule.IsValid())
                return false;

            var existingRule = FindRuleById(rule.Id);
            if (existingRule == null)
                return false;

            // 检查名称是否与其他规则重复
            if (RouteRules.Any(r => r.Id != rule.Id && r.Name == rule.Name))
                return false;

            // 检查端口是否与其他规则冲突
            if (RouteRules.Any(r => r.Id != rule.Id && r.LocalServerPort == rule.LocalServerPort))
                return false;

            var index = RouteRules.IndexOf(existingRule);
            rule.LastModified = DateTime.Now;
            RouteRules[index] = rule;
            UpdateLastModified();
            return true;
        }

        /// <summary>
        /// 删除路由规则
        /// </summary>
        /// <param name="id">规则ID</param>
        /// <returns>true表示删除成功</returns>
        public bool RemoveRule(string id)
        {
            var rule = FindRuleById(id);
            if (rule == null)
                return false;

            RouteRules.Remove(rule);
            UpdateLastModified();
            return true;
        }

        /// <summary>
        /// 核心验证逻辑
        /// </summary>
        protected override void ValidateCore(List<string> errors, List<string> warnings)
        {
            // 验证监听设置
            if (ListenPort <= 0 || ListenPort > 65535)
                errors.Add("监听端口必须在1-65535范围内");

            if (string.IsNullOrWhiteSpace(ListenIp))
                errors.Add("监听IP地址不能为空");

            // 验证连接设置
            if (MaxConnections <= 0)
                errors.Add("最大并发连接数必须大于0");

            if (ConnectionTimeout <= 0)
                errors.Add("连接超时时间必须大于0");

            if (HeartbeatInterval <= 0)
                errors.Add("心跳间隔必须大于0");

            if (ReconnectInterval <= 0)
                errors.Add("重连间隔必须大于0");

            if (MaxReconnectAttempts <= 0)
                errors.Add("最大重连次数必须大于0");

            if (BufferSize <= 0)
                errors.Add("缓冲区大小必须大于0");

            if (StatisticsRefreshInterval <= 0)
                errors.Add("统计刷新间隔必须大于0");

            // 验证路由规则
            var ruleNames = new HashSet<string>();
            var rulePorts = new HashSet<int>();

            foreach (var rule in RouteRules)
            {
                var ruleResult = rule.Validate();
                if (!ruleResult.IsValid)
                {
                    errors.AddRange(ruleResult.Errors.Select(e => $"规则 '{rule.Name}': {e}"));
                }
                warnings.AddRange(ruleResult.Warnings.Select(w => $"规则 '{rule.Name}': {w}"));

                // 检查重复名称
                if (!ruleNames.Add(rule.Name))
                {
                    errors.Add($"规则名称重复: {rule.Name}");
                }

                // 检查端口冲突
                if (!rulePorts.Add(rule.LocalServerPort))
                {
                    errors.Add($"端口冲突: {rule.LocalServerPort}");
                }
            }

            // 性能建议
            if (BufferSize < 1024)
                warnings.Add("缓冲区大小建议至少1024字节以获得更好性能");

            if (MaxConnections > 1000)
                warnings.Add("最大连接数过高可能影响性能");

            if (HeartbeatInterval < 30)
                warnings.Add("心跳间隔建议至少30秒以减少网络负载");

            // 警告检查
            if (RouteRules.Count == 0)
                warnings.Add("没有配置任何路由规则");

            if (RouteRules.Count(r => r.IsEnabled) == 0)
                warnings.Add("没有启用任何路由规则");
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
