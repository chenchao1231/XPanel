using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using XPluginTcpRelay.Models;

namespace XPluginTcpRelay.Services
{
    /// <summary>
    /// 配置管理服务
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private RelayConfig _config;

        public ConfigManager()
        {
            var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            
            _configFilePath = Path.Combine(configDir, "tcp_relay_config.json");
            _config = LoadConfig();
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public RelayConfig Config => _config;

        /// <summary>
        /// 加载配置
        /// </summary>
        private RelayConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonConvert.DeserializeObject<RelayConfig>(json);
                    if (config != null && config.IsValid())
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置失败: {ex.Message}");
            }

            // 返回默认配置
            return CreateDefaultConfig();
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public bool SaveConfig()
        {
            try
            {
                _config.LastModified = DateTime.Now;
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public bool UpdateConfig(RelayConfig newConfig)
        {
            if (newConfig == null || !newConfig.IsValid())
            {
                return false;
            }

            _config = newConfig;
            return SaveConfig();
        }

        /// <summary>
        /// 添加路由规则
        /// </summary>
        public bool AddRouteRule(RouteRule rule)
        {
            if (rule == null || !rule.IsValid())
            {
                return false;
            }

            // 检查是否已存在相同的规则
            if (_config.RouteRules.Any(r => r.DataSourceEndpoint == rule.DataSourceEndpoint))
            {
                return false; // 已存在相同的A方端点
            }

            _config.RouteRules.Add(rule);
            return SaveConfig();
        }

        /// <summary>
        /// 更新路由规则
        /// </summary>
        public bool UpdateRouteRule(RouteRule rule)
        {
            if (rule == null || !rule.IsValid())
            {
                return false;
            }

            var existingRule = _config.RouteRules.FirstOrDefault(r => r.Id == rule.Id);
            if (existingRule == null)
            {
                return false;
            }

            // 更新规则
            existingRule.Name = rule.Name;
            existingRule.DataSourceIp = rule.DataSourceIp;
            existingRule.DataSourcePort = rule.DataSourcePort;
            existingRule.DataType = rule.DataType;
            existingRule.LocalServerPort = rule.LocalServerPort;
            existingRule.IsEnabled = rule.IsEnabled;
            existingRule.Description = rule.Description;
            existingRule.LastModified = DateTime.Now;

            return SaveConfig();
        }

        /// <summary>
        /// 删除路由规则
        /// </summary>
        public bool RemoveRouteRule(string ruleId)
        {
            var rule = _config.RouteRules.FirstOrDefault(r => r.Id == ruleId);
            if (rule == null)
            {
                return false;
            }

            _config.RouteRules.Remove(rule);
            return SaveConfig();
        }

        /// <summary>
        /// 获取路由规则
        /// </summary>
        public RouteRule? GetRouteRule(string ruleId)
        {
            return _config.RouteRules.FirstOrDefault(r => r.Id == ruleId);
        }

        /// <summary>
        /// 根据A方端点查找路由规则
        /// </summary>
        public RouteRule? FindRouteRuleByDataSource(string ip, int port)
        {
            return _config.RouteRules.FirstOrDefault(r =>
                r.IsEnabled && r.DataSourceIp == ip && r.DataSourcePort == port);
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private RelayConfig CreateDefaultConfig()
        {
            var config = new RelayConfig();
            
            // 添加示例路由规则
            config.RouteRules.Add(new RouteRule
            {
                Name = "示例路由规则",
                DataSourceIp = "192.168.1.100",
                DataSourcePort = 8080,
                LocalServerPort = 9999,
                DataType = "realtime",
                Description = "示例TCP数据中转规则：连接数据源192.168.1.100:8080，本地服务端口9999"
            });

            return config;
        }
    }
}
