using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XPlugin.Configuration;
using XPlugin.logs;
using XPluginTcpRelay.Models;

namespace XPluginTcpRelay.Services
{
    /// <summary>
    /// 中继配置服务 - 实现标准配置服务接口
    /// </summary>
    public class RelayConfigService : IConfigurationService<RelayConfig>
    {
        private readonly string _configPath;
        private RelayConfig? _currentConfig;
        private readonly bool _supportHotReload;

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs<RelayConfig>>? ConfigurationChanged;

        /// <summary>
        /// 当前配置
        /// </summary>
        public RelayConfig Configuration => _currentConfig ?? CreateDefault();

        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string ConfigurationPath => _configPath;

        /// <summary>
        /// 是否支持热更新
        /// </summary>
        public bool SupportsHotReload => _supportHotReload;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        /// <param name="supportHotReload">是否支持热更新</param>
        public RelayConfigService(string configPath = "tcp_relay_config.json", bool supportHotReload = true)
        {
            _configPath = configPath;
            _supportHotReload = supportHotReload;
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public async Task<RelayConfig> LoadAsync()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = await File.ReadAllTextAsync(_configPath);
                    var config = JsonConvert.DeserializeObject<RelayConfig>(json);
                    
                    if (config != null)
                    {
                        var validationResult = Validate(config);
                        if (validationResult.IsValid)
                        {
                            var oldConfig = _currentConfig;
                            _currentConfig = config;
                            Log.Info($"配置加载成功: {_configPath}");
                            
                            ConfigurationChanged?.Invoke(this, 
                                new ConfigurationChangedEventArgs<RelayConfig>(oldConfig, config, ConfigurationChangeType.Loaded));
                            
                            return config;
                        }
                        else
                        {
                            Log.Warn($"配置文件验证失败: {string.Join(", ", validationResult.Errors)}");
                        }
                    }
                }
                else
                {
                    Log.Info("配置文件不存在，创建默认配置");
                }

                // 创建默认配置
                _currentConfig = CreateDefault();
                await SaveAsync(_currentConfig);
                return _currentConfig;
            }
            catch (Exception ex)
            {
                Log.Error($"加载配置失败: {ex.Message}");
                _currentConfig = CreateDefault();
                return _currentConfig;
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public async Task<bool> SaveAsync(RelayConfig configuration)
        {
            try
            {
                var validationResult = Validate(configuration);
                if (!validationResult.IsValid)
                {
                    Log.Error($"配置验证失败: {string.Join(", ", validationResult.Errors)}");
                    return false;
                }

                configuration.LastModified = DateTime.Now;
                var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
                await File.WriteAllTextAsync(_configPath, json);

                var oldConfig = _currentConfig;
                _currentConfig = configuration;
                Log.Info($"配置保存成功: {_configPath}");
                
                ConfigurationChanged?.Invoke(this, 
                    new ConfigurationChangedEventArgs<RelayConfig>(oldConfig, configuration, ConfigurationChangeType.Saved));
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"保存配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public async Task<bool> ReloadAsync()
        {
            try
            {
                var oldConfig = _currentConfig;
                var config = await LoadAsync();
                
                if (config != null)
                {
                    ConfigurationChanged?.Invoke(this, 
                        new ConfigurationChangedEventArgs<RelayConfig>(oldConfig, config, ConfigurationChangeType.Reloaded));
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"重新加载配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        public ConfigurationValidationResult Validate(RelayConfig configuration)
        {
            if (configuration == null)
                return ConfigurationValidationResult.Failure("配置对象不能为空");

            return configuration.Validate();
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public RelayConfig CreateDefault()
        {
            return new RelayConfig
            {
                SystemName = "TCP数据转发系统",
                ListenPort = 9999,
                ListenIp = "0.0.0.0",
                MaxConnections = 200,
                HeartbeatInterval = 60,
                ConnectionTimeout = 30,
                ReconnectInterval = 5,
                MaxReconnectAttempts = 5,
                BufferSize = 4096,
                EnableLogging = true,
                EnableDataAudit = true,
                LogLevel = LogLevel.Info,
                EnablePerformanceMonitoring = true,
                StatisticsRefreshInterval = 5,
                RouteRules = new System.Collections.Generic.List<RouteRule>(),
                CreatedTime = DateTime.Now,
                LastModified = DateTime.Now,
                Version = "1.0.0",
                Description = "TCP数据中继系统默认配置"
            };
        }

        /// <summary>
        /// 备份当前配置
        /// </summary>
        public async Task<bool> BackupAsync()
        {
            try
            {
                if (_currentConfig == null)
                    return false;

                var backupPath = $"{_configPath}.backup.{DateTime.Now:yyyyMMdd_HHmmss}";
                var json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
                await File.WriteAllTextAsync(backupPath, json);

                Log.Info($"配置备份成功: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"备份配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复配置
        /// </summary>
        public async Task<bool> RestoreAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    Log.Error($"备份文件不存在: {backupPath}");
                    return false;
                }

                var json = await File.ReadAllTextAsync(backupPath);
                var config = JsonConvert.DeserializeObject<RelayConfig>(json);

                if (config != null)
                {
                    var validationResult = Validate(config);
                    if (validationResult.IsValid)
                    {
                        var oldConfig = _currentConfig;
                        await SaveAsync(config);
                        
                        ConfigurationChanged?.Invoke(this, 
                            new ConfigurationChangedEventArgs<RelayConfig>(oldConfig, config, ConfigurationChangeType.Restored));
                        
                        Log.Info($"配置恢复成功: {backupPath}");
                        return true;
                    }
                    else
                    {
                        Log.Error($"备份配置验证失败: {string.Join(", ", validationResult.Errors)}");
                        return false;
                    }
                }
                else
                {
                    Log.Error("备份配置解析失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"恢复配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 配置管理器通常不需要特殊的资源清理
        }
    }
}
