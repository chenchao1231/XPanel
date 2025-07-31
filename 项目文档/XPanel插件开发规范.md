# XPanel插件开发规范

**版本**: 2.0.0  
**制定日期**: 2025年7月31日  
**适用范围**: 所有XPanel插件开发  

---

## 一、概述

本文档定义了XPanel插件系统的开发规范，所有插件开发必须严格遵循此规范，以确保插件的质量、兼容性和可维护性。

## 二、目录结构规范

### 2.1 标准目录结构

所有插件必须按照以下目录结构组织代码：

```
XPlugin[PluginName]/
├── Models/                          # 数据模型层
│   ├── [PluginName]Config.cs       # 插件配置模型
│   ├── [PluginName]Data.cs         # 业务数据模型
│   └── ...                         # 其他数据模型
├── Services/                        # 业务服务层
│   ├── ConfigManager.cs            # 配置管理服务
│   ├── [PluginName]Manager.cs      # 主要业务管理器
│   └── ...                         # 其他业务服务
├── UI/                             # 用户界面层
│   ├── [PluginName]MainPanel.cs    # 主面板（必需）
│   ├── [PluginName]ConfigDialog.cs # 配置对话框
│   └── ...                         # 其他UI组件
├── Utils/                          # 工具类库
│   ├── [PluginName]Helper.cs       # 插件专用工具类
│   └── ...                         # 其他工具类
├── 插件文档/                        # 插件文档
│   ├── 插件开发说明.md              # 开发说明文档
│   ├── API文档.md                  # API接口文档
│   └── 使用手册.md                 # 用户使用手册
├── [PluginName]Plugin.cs           # 插件主类（必需）
├── XPlugin[PluginName].csproj      # 项目文件
└── README.md                       # 项目说明
```

### 2.2 目录说明

#### Models 目录
- **用途**: 存放所有数据模型和配置类
- **命名**: 以插件名称开头，如`TcpServerConfig.cs`
- **职责**: 定义数据结构、验证规则、序列化逻辑

#### Services 目录
- **用途**: 存放业务逻辑和服务类
- **命名**: 以功能描述命名，如`ConfigManager.cs`
- **职责**: 实现业务逻辑、数据处理、外部服务调用

#### UI 目录
- **用途**: 存放所有用户界面组件
- **命名**: 以插件名称开头，如`TcpServerMainPanel.cs`
- **职责**: 用户界面实现、事件处理、数据绑定

#### Utils 目录
- **用途**: 存放工具类和辅助功能
- **命名**: 以Helper或Utility结尾
- **职责**: 通用工具方法、数据转换、格式化

#### 插件文档 目录
- **用途**: 存放插件相关文档
- **必需文件**: 插件开发说明.md、API文档.md、使用手册.md
- **职责**: 提供完整的插件文档

## 三、命名规范

### 3.1 项目命名
- **格式**: `XPlugin[PluginName]`
- **示例**: `XPluginTcpServer`、`XPluginTcpRelay`
- **要求**: PluginName使用PascalCase，简洁明了

### 3.2 类命名
- **插件主类**: `[PluginName]Plugin`
- **主面板**: `[PluginName]MainPanel`或`[PluginName]ManagementPanel`
- **配置类**: `[PluginName]Config`
- **数据模型**: `[PluginName]Data`或具体业务名称
- **服务类**: `[PluginName]Manager`或`[PluginName]Service`
- **工具类**: `[PluginName]Helper`或`[PluginName]Utility`

### 3.3 文件命名
- **C#文件**: 与类名保持一致，使用PascalCase
- **配置文件**: 使用snake_case，如`tcp_server_config.json`
- **文档文件**: 使用中文描述性名称

### 3.4 成员命名
- **公共属性**: PascalCase（如：`ServerName`）
- **公共方法**: PascalCase（如：`StartServer`）
- **私有字段**: camelCase，以下划线开头（如：`_serverManager`）
- **常量**: UPPER_CASE（如：`MAX_CONNECTION_COUNT`）
- **事件**: PascalCase，以动词过去式结尾（如：`ServerStarted`）

## 四、接口实现规范

### 4.1 必需接口

所有插件必须实现`IXPanelInterface`接口：

```csharp
public class [PluginName]Plugin : IXPanelInterface
{
    private static [PluginName]MainPanel? _currentPanel;

    public string Name => "插件显示名称";

    public UserControl CreatePanel()
    {
        try
        {
            // 清理旧实例
            if (_currentPanel != null)
            {
                _currentPanel.Dispose();
                _currentPanel = null;
            }

            // 创建新实例
            _currentPanel = new [PluginName]MainPanel();
            return _currentPanel;
        }
        catch (Exception ex)
        {
            return CreateErrorPanel(ex);
        }
    }

    // 错误处理和资源清理方法...
}
```

### 4.2 可选接口

如果插件提供服务功能，可以实现`IServerPlugin`接口：

```csharp
public class [PluginName]Plugin : IXPanelInterface, IServerPlugin
{
    public void Start() { /* 启动服务 */ }
    public void Stop() { /* 停止服务 */ }
    public void Start(string ip, int port, ILogOutput output) { /* 带参数启动 */ }
    public void InitializePorts(IEnumerable<int> ports) { /* 初始化端口 */ }
}
```

## 五、UI设计规范

### 5.1 主面板设计

主面板必须遵循以下设计原则：

```csharp
public class [PluginName]MainPanel : UserControl
{
    public [PluginName]MainPanel()
    {
        InitializeComponent();
        
        // 应用主题
        ThemeManager.ApplyTheme(this);
        
        // 监听主题变化
        ThemeManager.ThemeChanged += OnThemeChanged;
        
        // 注册日志输出
        Log.RegisterOutput(new UILogOutput(AppendLog));
    }

    private void InitializeComponent()
    {
        // 基本属性设置
        Size = new Size(1200, 800);
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // 布局实现...
    }

    // 主题变化处理
    private void OnThemeChanged(ThemeConfig theme)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<ThemeConfig>(OnThemeChanged), theme);
            return;
        }
        ThemeManager.ApplyTheme(this);
    }

    // 资源清理
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            // 其他资源清理...
        }
        base.Dispose(disposing);
    }
}
```

### 5.2 布局规范

- **主面板尺寸**: 默认1200x800，支持Dock.Fill
- **控件间距**: 统一使用10px间距
- **字体**: 统一使用"Microsoft YaHei"
- **颜色**: 支持主题系统，不硬编码颜色值

### 5.3 主题支持

所有UI组件必须支持主题系统：

```csharp
// 应用主题
ThemeManager.ApplyTheme(this);

// 监听主题变化
ThemeManager.ThemeChanged += OnThemeChanged;

// 主题变化处理
private void OnThemeChanged(ThemeConfig theme)
{
    if (InvokeRequired)
    {
        Invoke(new Action<ThemeConfig>(OnThemeChanged), theme);
        return;
    }
    ThemeManager.ApplyTheme(this);
}
```

## 六、配置管理规范

### 6.1 配置文件格式

所有配置文件必须使用JSON格式：

```json
{
  "ConfigVersion": "1.0.0",
  "PluginName": "插件名称",
  "Settings": {
    "AutoStart": true,
    "LogLevel": "Info",
    "MaxConnections": 1000
  },
  "LastModified": "2025-07-31T14:30:00Z"
}
```

### 6.2 配置管理器实现

```csharp
public class ConfigManager
{
    private readonly string _configFilePath;
    private readonly object _lockObject = new object();

    public ConfigManager()
    {
        _configFilePath = Path.Combine("Config", "[plugin_name]_config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
    }

    public T LoadConfig<T>() where T : class, new()
    {
        lock (_lockObject)
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    return JsonSerializer.Deserialize<T>(json) ?? new T();
                }
                return new T();
            }
            catch (Exception ex)
            {
                Log.Error($"加载配置失败: {ex.Message}");
                return new T();
            }
        }
    }

    public void SaveConfig<T>(T config) where T : class
    {
        lock (_lockObject)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_configFilePath, json);
                Log.Info("配置保存成功");
            }
            catch (Exception ex)
            {
                Log.Error($"保存配置失败: {ex.Message}");
            }
        }
    }
}
```

## 七、日志系统规范

### 7.1 日志使用

所有插件必须使用统一的日志系统：

```csharp
// 注册日志输出
Log.RegisterOutput(new UILogOutput(AppendLog));

// 记录日志
Log.Info("插件启动成功");
Log.Warn("配置文件不存在，使用默认配置");
Log.Error($"操作失败: {ex.Message}");
```

### 7.2 UI日志输出实现

```csharp
public class UILogOutput : ILogOutput
{
    private readonly Action<string> _appendAction;

    public UILogOutput(Action<string> appendAction)
    {
        _appendAction = appendAction;
    }

    public void AppendLog(string message)
    {
        _appendAction?.Invoke(message);
    }
}
```

## 八、异常处理规范

### 8.1 异常处理原则

1. **捕获所有可能的异常**
2. **记录详细的错误信息**
3. **提供用户友好的错误提示**
4. **实现优雅的错误恢复**

### 8.2 异常处理模式

```csharp
public bool SomeOperation()
{
    try
    {
        // 业务逻辑
        return true;
    }
    catch (SpecificException ex)
    {
        Log.Error($"特定异常: {ex.Message}");
        // 特定处理逻辑
        return false;
    }
    catch (Exception ex)
    {
        Log.Error($"未知异常: {ex.Message}\n堆栈跟踪: {ex.StackTrace}");
        MessageBox.Show($"操作失败: {ex.Message}", "错误", 
            MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }
}
```

## 九、资源管理规范

### 9.1 IDisposable实现

所有需要清理资源的类必须实现IDisposable：

```csharp
public class SomeService : IDisposable
{
    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 清理托管资源
            }
            // 清理非托管资源
            _disposed = true;
        }
    }
}
```

### 9.2 静态引用管理

插件主类必须管理静态引用：

```csharp
public class [PluginName]Plugin : IXPanelInterface
{
    private static [PluginName]MainPanel? _currentPanel;

    public static void Cleanup()
    {
        try
        {
            if (_currentPanel != null)
            {
                _currentPanel.Dispose();
                _currentPanel = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"清理资源失败: {ex.Message}");
        }
    }

    internal static void OnPanelDisposed([PluginName]MainPanel panel)
    {
        if (_currentPanel == panel)
        {
            _currentPanel = null;
        }
    }
}
```

## 十、性能优化规范

### 10.1 异步编程

- 所有耗时操作必须使用异步方法
- UI操作必须在UI线程执行
- 使用CancellationToken支持取消操作

```csharp
public async Task<bool> SomeAsyncOperation(CancellationToken cancellationToken = default)
{
    try
    {
        await Task.Run(() => {
            // 耗时操作
        }, cancellationToken);
        
        return true;
    }
    catch (OperationCanceledException)
    {
        Log.Info("操作已取消");
        return false;
    }
    catch (Exception ex)
    {
        Log.Error($"异步操作失败: {ex.Message}");
        return false;
    }
}
```

### 10.2 线程安全

- 使用ConcurrentDictionary等线程安全集合
- 使用lock保护共享资源
- UI更新使用Invoke机制

```csharp
private readonly ConcurrentDictionary<string, object> _cache = new();
private readonly object _lockObject = new object();

private void UpdateUI(string message)
{
    if (InvokeRequired)
    {
        Invoke(new Action<string>(UpdateUI), message);
        return;
    }
    
    // UI更新逻辑
}
```

## 十一、测试规范

### 11.1 单元测试

每个插件应包含单元测试项目：

```
XPlugin[PluginName].Tests/
├── Models/
├── Services/
├── Utils/
└── XPlugin[PluginName].Tests.csproj
```

### 11.2 测试覆盖率

- 业务逻辑代码覆盖率 ≥ 80%
- 关键功能代码覆盖率 ≥ 95%
- 所有公共API必须有测试

## 十二、文档规范

### 12.1 必需文档

每个插件必须包含以下文档：

1. **插件开发说明.md** - 详细的开发说明
2. **API文档.md** - 公共接口文档
3. **使用手册.md** - 用户使用指南
4. **README.md** - 项目概述

### 12.2 代码注释

- 所有公共类和方法必须有XML文档注释
- 复杂逻辑必须有行内注释
- 使用中文注释，简洁明了

```csharp
/// <summary>
/// TCP服务器管理器
/// </summary>
public class TcpServerManager
{
    /// <summary>
    /// 启动TCP服务器
    /// </summary>
    /// <param name="config">服务器配置</param>
    /// <returns>true表示启动成功</returns>
    public async Task<bool> StartServerAsync(TcpServerConfig config)
    {
        // 实现逻辑...
    }
}
```

## 十三、版本管理规范

### 13.1 版本号格式

使用语义化版本号：`主版本.次版本.修订版本`

- **主版本**: 不兼容的API修改
- **次版本**: 向下兼容的功能性新增
- **修订版本**: 向下兼容的问题修正

### 13.2 变更日志

每个版本必须维护CHANGELOG.md文件：

```markdown
# 变更日志

## [1.2.0] - 2025-07-31

### 新增
- 添加数据导出功能
- 支持批量操作

### 修改
- 优化界面布局
- 提升性能

### 修复
- 修复内存泄漏问题
- 修复配置保存异常
```

## 十四、发布规范

### 14.1 编译配置

- **目标框架**: .NET 8.0
- **输出目录**: `XPlugin`
- **文件命名**: `XPlugin[PluginName].dll`

### 14.2 依赖管理

- 尽量减少外部依赖
- 必需依赖必须在文档中说明
- 使用NuGet包管理依赖

### 14.3 发布检查清单

- [ ] 代码编译无错误无警告
- [ ] 单元测试全部通过
- [ ] 文档完整且最新
- [ ] 配置文件格式正确
- [ ] 资源清理正常
- [ ] 主题支持正常
- [ ] 性能测试通过

---

## 十五、规范执行

### 15.1 代码审查

所有插件代码必须经过代码审查，确保符合本规范。

### 15.2 质量检查

使用自动化工具检查代码质量：
- 代码格式检查
- 静态代码分析
- 单元测试覆盖率检查

### 15.3 持续改进

本规范将根据实际开发经验持续更新和完善。

---

**文档版本**: 2.0.0  
**最后更新**: 2025年7月31日  
**维护者**: XPanel开发团队

遵循本规范开发的插件将具有更好的质量、兼容性和可维护性，为用户提供一致的使用体验。
