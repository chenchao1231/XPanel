using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using XPlugin;
using XPlugin.logs;

namespace XPanel.Plugins
{
    /// <summary>
    /// XPlugin插件管理器
    /// </summary>
    public class XPluginManager
    {
        private readonly string _pluginFolder;
        private readonly string _stateFilePath;
        private readonly Dictionary<string, PluginInfo> _loadedPlugins;
        private readonly Dictionary<string, bool> _pluginStates; // 插件启用状态

        public XPluginManager()
        {
            _pluginFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XPlugins");
            _stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugin_states.json");
            _loadedPlugins = new Dictionary<string, PluginInfo>();
            _pluginStates = new Dictionary<string, bool>();

            // 初始化日志系统
            InitializeLogging();

            // 确保插件目录存在
            if (!Directory.Exists(_pluginFolder))
            {
                Directory.CreateDirectory(_pluginFolder);
            }

            // 加载插件状态
            LoadPluginStates();

            Log.Info("插件管理器初始化完成");
        }

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        private void InitializeLogging()
        {
            // 注册文件日志输出
            Log.RegisterOutput(new FileLogOutput());

            // 注册控制台日志输出
            Log.RegisterOutput(new ConsoleLogOutput());
        }

        /// <summary>
        /// 加载插件状态
        /// </summary>
        private void LoadPluginStates()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    var states = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                    if (states != null)
                    {
                        foreach (var kvp in states)
                        {
                            _pluginStates[kvp.Key] = kvp.Value;
                        }
                        Log.Info($"已加载 {states.Count} 个插件状态");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"加载插件状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存插件状态
        /// </summary>
        private void SavePluginStates()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_pluginStates, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_stateFilePath, json);
                Log.Info($"已保存 {_pluginStates.Count} 个插件状态");
            }
            catch (Exception ex)
            {
                Log.Error($"保存插件状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示调试消息
        /// </summary>
        private void ShowDebugMessage(string message)
        {
            Log.Debug(message);
        }

        /// <summary>
        /// 获取所有已加载的插件
        /// </summary>
        public IEnumerable<PluginInfo> LoadedPlugins => _loadedPlugins.Values;

        /// <summary>
        /// 加载所有插件
        /// </summary>
        public void LoadAllPlugins()
        {
            try
            {
                var pluginFiles = Directory.GetFiles(_pluginFolder, "XPlugin*.dll");
                
                foreach (var dllPath in pluginFiles)
                {
                    LoadPlugin(dllPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"加载插件时发生错误: {ex.Message}");
                MessageBox.Show($"加载插件时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 加载单个插件
        /// </summary>
        /// <param name="dllPath">插件DLL路径</param>
        private bool LoadPlugin(string dllPath)
        {
            try
            {
                var fileName = Path.GetFileName(dllPath);
                ShowDebugMessage($"🔄 开始加载插件: {fileName}");

                // 检查是否已经加载
                if (_loadedPlugins.ContainsKey(fileName))
                {
                    ShowDebugMessage($"⚠️ 插件 {fileName} 已经加载，跳过");
                    return true;
                }

                // 使用 LoadFile 而不是 LoadFrom 来避免文件锁定
                // 先将文件复制到临时位置再加载
                var tempPath = Path.Combine(Path.GetTempPath(), $"temp_{Guid.NewGuid()}_{Path.GetFileName(dllPath)}");
                File.Copy(dllPath, tempPath, true);
                Assembly pluginAssembly = Assembly.LoadFile(tempPath);
                ShowDebugMessage($"📦 程序集加载成功: {pluginAssembly.FullName}");

                // 获取所有类型
                var allTypes = pluginAssembly.GetTypes();
                ShowDebugMessage($"🔍 找到 {allTypes.Length} 个类型");

                // 查找实现了 IXPanelInterface 的类型
                var panelTypes = allTypes
                    .Where(t => !t.IsAbstract && t.GetInterfaces()
                        .Any(i => i.FullName == "XPlugin.IXPanelInterface"))
                    .ToList();
                ShowDebugMessage($"🎯 找到 {panelTypes.Count} 个面板插件类型");

                // 输出详细的类型信息用于调试
                foreach (var type in allTypes)
                {
                    var interfaces = type.GetInterfaces().Select(i => i.FullName).ToArray();
                    ShowDebugMessage($"🔍 类型: {type.FullName}, 接口: [{string.Join(", ", interfaces)}]");
                }

                // 查找实现了 IServerPlugin 的类型
                var serverTypes = allTypes
                    .Where(t => !t.IsAbstract && t.GetInterfaces()
                        .Any(i => i.FullName == "XPlugin.IServerPlugin"))
                    .ToList();
                ShowDebugMessage($"🎯 找到 {serverTypes.Count} 个服务插件类型");

                if (panelTypes.Any() || serverTypes.Any())
                {
                    var pluginInfo = new PluginInfo
                    {
                        FileName = fileName,
                        FilePath = dllPath,
                        Assembly = pluginAssembly,
                        PanelTypes = panelTypes,
                        ServerTypes = serverTypes,
                        LoadTime = DateTime.Now
                    };

                    _loadedPlugins[fileName] = pluginInfo;

                    // 如果没有保存的状态，默认启用
                    if (!_pluginStates.ContainsKey(fileName))
                    {
                        _pluginStates[fileName] = true;
                        SavePluginStates(); // 保存新插件的默认状态
                    }
                    
                    ShowDebugMessage($"✅ 插件加载成功: {fileName}");
                    return true;
                }
                else
                {
                    ShowDebugMessage($"⚠️ 插件 {fileName} 未找到有效的接口实现");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ShowDebugMessage($"❌ 加载插件失败: {Path.GetFileName(dllPath)}, 原因: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 上传并安装插件
        /// </summary>
        /// <param name="sourceFilePath">源文件路径</param>
        public bool InstallPlugin(string sourceFilePath)
        {
            try
            {
                var fileName = Path.GetFileName(sourceFilePath);
                
                // 检查文件名是否符合规范
                if (!fileName.StartsWith("XPlugin") || !fileName.EndsWith(".dll"))
                {
                    MessageBox.Show("插件文件名必须以 'XPlugin' 开头，以 '.dll' 结尾", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var targetPath = Path.Combine(_pluginFolder, fileName);
                
                // 如果目标文件已存在，询问是否覆盖
                if (File.Exists(targetPath))
                {
                    ShowDebugMessage($"⚠️ 插件文件已存在: {fileName}");
                    var result = MessageBox.Show($"插件 {fileName} 已存在，是否覆盖？", "确认",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result != DialogResult.Yes)
                    {
                        ShowDebugMessage($"❌ 用户取消覆盖: {fileName}");
                        return false;
                    }

                    ShowDebugMessage($"🔄 准备覆盖插件: {fileName}");

                    // 先从内存中卸载现有插件
                    if (_loadedPlugins.ContainsKey(fileName))
                    {
                        _loadedPlugins.Remove(fileName);
                        _pluginStates.Remove(fileName);
                        ShowDebugMessage($"✅ 从内存中移除旧插件: {fileName}");
                    }

                    // 等待一下让文件句柄释放
                    System.Threading.Thread.Sleep(100);
                }

                ShowDebugMessage($"📁 开始复制插件文件: {sourceFilePath} → {targetPath}");

                // 复制文件，重试机制
                int retryCount = 3;
                bool copySuccess = false;

                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        File.Copy(sourceFilePath, targetPath, true);
                        copySuccess = true;
                        ShowDebugMessage($"✅ 文件复制成功");
                        break;
                    }
                    catch (IOException ex) when (i < retryCount - 1)
                    {
                        ShowDebugMessage($"⚠️ 文件复制失败，重试 {i + 1}/{retryCount}: {ex.Message}");
                        System.Threading.Thread.Sleep(500);
                    }
                }

                if (!copySuccess)
                {
                    ShowDebugMessage($"❌ 文件复制失败，已重试 {retryCount} 次");
                    MessageBox.Show($"无法复制插件文件，可能文件被占用。请重启程序后重试。",
                        "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                
                // 加载新插件
                bool success = LoadPlugin(targetPath);
                
                if (success)
                {
                    MessageBox.Show($"插件 {fileName} 安装成功！", "成功", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Log.Error($"安装插件失败: {ex.Message}");
                MessageBox.Show($"安装插件失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 卸载插件
        /// </summary>
        /// <param name="fileName">插件文件名</param>
        public bool UnloadPlugin(string fileName)
        {
            try
            {
                ShowDebugMessage($"🗑️ 开始卸载插件: {fileName}");

                if (_loadedPlugins.ContainsKey(fileName))
                {
                    // 通知插件被卸载
                    OnPluginDisabled?.Invoke(fileName);

                    // 从内存中移除
                    _loadedPlugins.Remove(fileName);
                    _pluginStates.Remove(fileName);
                    SavePluginStates(); // 保存状态
                    ShowDebugMessage($"✅ 从内存中移除插件: {fileName}");

                    // 删除物理文件
                    var filePath = Path.Combine(_pluginFolder, fileName);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            // 尝试删除文件
                            File.Delete(filePath);
                            ShowDebugMessage($"🗑️ 物理文件已删除: {filePath}");
                        }
                        catch (UnauthorizedAccessException)
                        {
                            ShowDebugMessage($"⚠️ 文件被占用，无法删除: {filePath}");
                            MessageBox.Show($"插件文件被占用，无法删除。请重启程序后再试。\n文件: {fileName}",
                                "卸载警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        catch (IOException ex)
                        {
                            ShowDebugMessage($"❌ 删除文件失败: {ex.Message}");
                            MessageBox.Show($"删除插件文件失败: {ex.Message}",
                                "卸载错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    ShowDebugMessage($"✅ 插件卸载完成: {fileName}");
                    return true;
                }

                ShowDebugMessage($"⚠️ 插件未找到: {fileName}");
                return false;
            }
            catch (Exception ex)
            {
                ShowDebugMessage($"❌ 卸载插件失败: {fileName}, 原因: {ex.Message}");
                MessageBox.Show($"卸载插件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 启用或禁用插件
        /// </summary>
        /// <param name="fileName">插件文件名</param>
        /// <param name="enabled">是否启用</param>
        public void SetPluginEnabled(string fileName, bool enabled)
        {
            if (_pluginStates.ContainsKey(fileName))
            {
                _pluginStates[fileName] = enabled;
                SavePluginStates(); // 保存状态
                Log.Info($"插件 {fileName} 已{(enabled ? "启用" : "禁用")}");

                // 如果禁用插件，需要通知主窗体关闭相关面板
                if (!enabled)
                {
                    OnPluginDisabled?.Invoke(fileName);
                }
            }
        }

        /// <summary>
        /// 插件被禁用时的事件
        /// </summary>
        public event Action<string>? OnPluginDisabled;

        /// <summary>
        /// 检查插件是否启用
        /// </summary>
        /// <param name="fileName">插件文件名</param>
        /// <returns>是否启用</returns>
        public bool IsPluginEnabled(string fileName)
        {
            return _pluginStates.TryGetValue(fileName, out bool enabled) && enabled;
        }

        /// <summary>
        /// 获取启用的面板插件
        /// </summary>
        /// <returns>启用的面板插件列表</returns>
        public IEnumerable<(string Name, Func<UserControl> Factory)> GetEnabledPanelPlugins()
        {
            var results = new List<(string Name, Func<UserControl> Factory)>();

            foreach (var kvp in _loadedPlugins)
            {
                if (!IsPluginEnabled(kvp.Key)) continue;

                var pluginInfo = kvp.Value;
                foreach (var panelType in pluginInfo.PanelTypes)
                {
                    try
                    {
                        var instance = Activator.CreateInstance(panelType);
                        if (instance != null)
                        {
                            // 使用反射获取Name属性
                            var nameProperty = panelType.GetProperty("Name");
                            var createPanelMethod = panelType.GetMethod("CreatePanel");

                            if (nameProperty != null && createPanelMethod != null)
                            {
                                var name = nameProperty.GetValue(instance)?.ToString() ?? "未知插件";

                                Func<UserControl> factory = () =>
                                {
                                    var pluginInstance = Activator.CreateInstance(panelType);
                                    var result = createPanelMethod.Invoke(pluginInstance, null);
                                    return result as UserControl ?? new UserControl();
                                };

                                results.Add((name, factory));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"创建面板插件实例失败: {ex.Message}");
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 获取启用的服务插件
        /// </summary>
        /// <returns>启用的服务插件列表</returns>
        public IEnumerable<IServerPlugin> GetEnabledServerPlugins()
        {
            foreach (var kvp in _loadedPlugins)
            {
                if (!IsPluginEnabled(kvp.Key)) continue;

                var pluginInfo = kvp.Value;
                foreach (var serverType in pluginInfo.ServerTypes)
                {
                    if (Activator.CreateInstance(serverType) is IServerPlugin serverPlugin)
                    {
                        yield return serverPlugin;
                    }
                }
            }
        }
    }
    

    /// <summary>
    /// 插件信息
    /// </summary>
    public class PluginInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public Assembly Assembly { get; set; } = null!;
        public List<Type> PanelTypes { get; set; } = new();
        public List<Type> ServerTypes { get; set; } = new();
        public DateTime LoadTime { get; set; }
    }
}
