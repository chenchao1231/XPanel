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
            if (_config.RouteRules.Any(r => r.AEndpoint == rule.AEndpoint))
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
            existingRule.ASourceIp = rule.ASourceIp;
            existingRule.ASourcePort = rule.ASourcePort;
            existingRule.CTargetIp = rule.CTargetIp;
            existingRule.CTargetPort = rule.CTargetPort;
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
        public RouteRule? FindRouteRuleByAEndpoint(string ip, int port)
        {
            return _config.RouteRules.FirstOrDefault(r => 
                r.IsEnabled && r.ASourceIp == ip && r.ASourcePort == port);
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
                ASourceIp = "192.168.1.100",
                ASourcePort = 8080,
                CTargetIp = "10.0.0.50",
                CTargetPort = 8080,
                Description = "这是一个示例路由规则，A方192.168.1.100:8080的数据转发到C方10.0.0.50:8080"
            });

            return config;
        }
    }
}
