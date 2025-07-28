# XPanel - 插件化管理面板

XPanel 是一个基于 .NET 8.0 和 WinForms 的插件化管理面板系统，支持动态加载插件，提供可视化的插件管理功能。

## 项目结构

```
XPanel/
├── Forms/                  # 主窗体
├── Layout/                 # UI布局管理
├── Panels/                 # 内置面板
├── Plugins/                # 插件管理
├── Registry/               # 面板注册
├── Tabs/                   # 标签页管理
├── XPlugin/                # 插件接口定义
├── XPluginSample/          # 示例插件
└── XPlugin/                # 运行时插件目录
```

## 功能特性

- ✅ 插件动态加载和卸载
- ✅ 插件启用/禁用管理
- ✅ 可视化插件管理界面
- ✅ 规范的插件开发模式
- ✅ 支持面板插件和服务插件
- ✅ 插件上传安装功能

## 插件开发规范

### 1. 插件命名规范

- 插件DLL文件必须以 `XPlugin` 开头
- 例如：`XPluginSample.dll`、`XPluginTcpServer.dll`

### 2. 插件接口

#### 面板插件接口 (IXPanelInterface)

```csharp
public interface IXPanelInterface
{
    string Name { get; }                    // 插件名称
    UserControl CreatePanel();             // 创建面板
}
```

#### 服务插件接口 (IServerPlugin)

```csharp
public interface IServerPlugin
{
    string Name { get; }
    void Start();
    void Start(string ip, int port, ILogOutput output);
    void Stop();
    void InitializePorts(IEnumerable<int> ports);
}
```

### 3. 插件开发步骤

1. **创建插件项目**
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net8.0-windows</TargetFramework>
       <UseWindowsForms>true</UseWindowsForms>
       <AssemblyName>XPluginYourName</AssemblyName>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="..\XPlugin\XPlugin.csproj" />
     </ItemGroup>
   </Project>
   ```

2. **实现插件接口**
   ```csharp
   public class YourPlugin : IXPanelInterface
   {
       public string Name => "您的插件名称";
       
       public UserControl CreatePanel()
       {
           return new YourPanel();
       }
   }
   ```

3. **创建用户控件**
   ```csharp
   public class YourPanel : UserControl
   {
       public YourPanel()
       {
           InitializeComponent();
       }
       
       private void InitializeComponent()
       {
           // 设计您的界面
       }
   }
   ```

4. **编译和部署**
   - 编译项目生成 `XPluginYourName.dll`
   - 通过主程序的"插件管理"上传安装

### 4. 示例插件

项目中包含了一个完整的示例插件 `XPluginSample`，展示了：
- 基本的插件结构
- 界面设计
- 事件处理
- 插件信息展示

## 使用说明

### 主程序功能

1. **插件管理**
   - 菜单：插件 → 插件管理
   - 功能：上传、启用、禁用、卸载插件

2. **面板切换**
   - 通过顶部菜单打开不同面板
   - 支持多标签页管理
   - 可关闭标签页

### 插件安装

1. 点击"插件"菜单 → "插件管理"
2. 点击"上传插件"按钮
3. 选择符合命名规范的插件DLL文件
4. 插件自动安装并可在菜单中使用

### 插件管理

- **启用/禁用**：控制插件是否在菜单中显示
- **刷新列表**：重新扫描插件目录
- **卸载插件**：从系统中移除插件

## 技术架构

- **框架**：.NET 8.0 + WinForms
- **插件系统**：基于反射的动态加载
- **界面管理**：TabControl + MenuStrip
- **插件存储**：XPlugin 文件夹

## 开发环境

- Visual Studio 2022 或 JetBrains Rider
- .NET 8.0 SDK
- Windows 操作系统

## 扩展功能

系统支持进一步扩展：
- 插件配置管理
- 插件依赖管理
- 插件版本控制
- 插件市场集成
- 远程插件下载

## 注意事项

1. 插件文件名必须以 `XPlugin` 开头
2. 插件需要引用 `XPlugin` 项目
3. 插件卸载后需要重启程序才能完全释放资源
4. 建议在开发插件时遵循单一职责原则

## 许可证

本项目采用 MIT 许可证，详见 LICENSE 文件。
