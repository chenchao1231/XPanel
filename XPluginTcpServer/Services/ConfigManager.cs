using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using XPluginTcpServer.Models;
using XPlugin.logs;

namespace XPluginTcpServer.Services
{
    /// <summary>
    /// 配置管理器
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private readonly object _lockObject = new object();
        private List<TcpServerConfig> _configs;

        public ConfigManager()
        {
            var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            _configFilePath = Path.Combine(configDir, "tcp_servers.json");
            _configs = new List<TcpServerConfig>();
            LoadConfigs();
        }

        /// <summary>
        /// 获取所有配置
        /// </summary>
        public List<TcpServerConfig> GetAllConfigs()
        {
            lock (_lockObject)
            {
                return _configs.Select(c => c.Clone()).ToList();
            }
        }

        /// <summary>
        /// 根据ID获取配置
        /// </summary>
        public TcpServerConfig? GetConfig(string id)
        {
            lock (_lockObject)
            {
                return _configs.FirstOrDefault(c => c.Id == id)?.Clone();
            }
        }

        /// <summary>
        /// 添加配置
        /// </summary>
        public void AddConfig(TcpServerConfig config)
        {
            lock (_lockObject)
            {
                config.Id = Guid.NewGuid().ToString();
                config.CreatedTime = DateTime.Now;
                config.LastModified = DateTime.Now;
                _configs.Add(config);
                SaveConfigs();
                Log.Info($"添加TCP服务器配置: {config.Name} ({config.IpAddress}:{config.Port})");
            }
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public bool UpdateConfig(TcpServerConfig config)
        {
            lock (_lockObject)
            {
                var existingConfig = _configs.FirstOrDefault(c => c.Id == config.Id);
                if (existingConfig != null)
                {
                    var index = _configs.IndexOf(existingConfig);
                    config.LastModified = DateTime.Now;
                    _configs[index] = config;
                    SaveConfigs();
                    Log.Info($"更新TCP服务器配置: {config.Name} ({config.IpAddress}:{config.Port})");
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 删除配置
        /// </summary>
        public bool DeleteConfig(string id)
        {
            lock (_lockObject)
            {
                var config = _configs.FirstOrDefault(c => c.Id == id);
                if (config != null)
                {
                    _configs.Remove(config);
                    SaveConfigs();
                    Log.Info($"删除TCP服务器配置: {config.Name}");
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 获取自动启动的配置
        /// </summary>
        public List<TcpServerConfig> GetAutoStartConfigs()
        {
            lock (_lockObject)
            {
                return _configs.Where(c => c.AutoStart).Select(c => c.Clone()).ToList();
            }
        }

        /// <summary>
        /// 更新服务器运行状态
        /// </summary>
        public void UpdateServerStatus(string id, bool isRunning, int currentConnections = 0)
        {
            lock (_lockObject)
            {
                var config = _configs.FirstOrDefault(c => c.Id == id);
                if (config != null)
                {
                    config.IsRunning = isRunning;
                    config.CurrentConnections = currentConnections;
                    config.LastModified = DateTime.Now;
                    SaveConfigs();
                }
            }
        }

        /// <summary>
        /// 增加总连接数
        /// </summary>
        public void IncrementTotalConnections(string id)
        {
            lock (_lockObject)
            {
                var config = _configs.FirstOrDefault(c => c.Id == id);
                if (config != null)
                {
                    config.TotalConnections++;
                    SaveConfigs();
                }
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfigs()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var configs = JsonConvert.DeserializeObject<List<TcpServerConfig>>(json);
                    if (configs != null)
                    {
                        _configs = configs;
                        // 重置运行状态
                        foreach (var config in _configs)
                        {
                            config.IsRunning = false;
                            config.CurrentConnections = 0;
                        }
                        Log.Info($"加载了 {_configs.Count} 个TCP服务器配置");
                    }
                }
                else
                {
                    // 创建默认配置
                    _configs = new List<TcpServerConfig>
                    {
                        new TcpServerConfig
                        {
                            Name = "默认TCP服务器",
                            IpAddress = "127.0.0.1",
                            Port = 8080,
                            Description = "默认的TCP服务器配置"
                        }
                    };
                    SaveConfigs();
                    Log.Info("创建了默认TCP服务器配置");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"加载TCP服务器配置失败: {ex.Message}");
                _configs = new List<TcpServerConfig>();
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfigs()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_configs, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"保存TCP服务器配置失败: {ex.Message}");
            }
        }
    }
}
