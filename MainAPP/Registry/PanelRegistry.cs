namespace XPanel.Registry
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using XPanel.Plugins;

    /// <summary>
    /// Defines the <see cref="PanelRegistry" />
    /// </summary>
    public static class PanelRegistry
    {
        /// <summary>
        /// 注册表：键是面板名，值是创建 UserControl 的工厂方法
        /// </summary>
        private static readonly Dictionary<string, Func<UserControl>> registry = new();

        /// <summary>
        /// 插件管理器实例
        /// </summary>
        private static XPluginManager _pluginManager = new();

        /// <summary>
        /// Initializes static members of the <see cref="PanelRegistry"/> class.
        /// </summary>
        static PanelRegistry()
        {
            // 注册默认面板
            Register("系统设置", () => new XPanel.Panels.SettingsPanel());
            Register("关于程序", () => new XPanel.Panels.AboutPanel());
            Register("插件管理", () => new XPanel.Panels.PluginManagerPanel(_pluginManager));

            // 加载插件面板
            LoadPluginPanels();
        }

        /// <summary>
        /// 注册一个面板，如果名称已存在则跳过
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="factory">The factory<see cref="Func{UserControl}"/></param>
        public static void Register(string name, Func<UserControl> factory)
        {
            if (!registry.ContainsKey(name))
            {
                registry[name] = factory;
            }
        }

        /// <summary>
        /// 通过名称创建一个面板实例（未注册返回 null）
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <returns>The <see cref="UserControl?"/></returns>
        public static UserControl? CreatePanel(string name)
        {
            return registry.TryGetValue(name, out var factory) ? factory() : null;
        }

        /// <summary>
        /// Gets the RegisteredNames
        /// 获取所有已注册面板的名称
        /// </summary>
        public static IEnumerable<string> RegisteredNames => registry.Keys;

        /// <summary>
        /// 获取插件管理器实例
        /// </summary>
        public static XPluginManager PluginManager => _pluginManager;

        /// <summary>
        /// 加载插件面板
        /// </summary>
        public static void LoadPluginPanels()
        {
            try
            {
                // 加载所有插件
                _pluginManager.LoadAllPlugins();

                // 注册启用的面板插件
                foreach (var (name, factory) in _pluginManager.GetEnabledPanelPlugins())
                {
                    Register(name, factory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("插件加载失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 刷新插件面板注册
        /// </summary>
        public static void RefreshPluginPanels()
        {
            // 清除之前注册的插件面板（保留系统面板）
            var systemPanels = new[] { "系统设置", "关于程序", "插件管理" };
            var keysToRemove = registry.Keys.Where(k => !systemPanels.Contains(k)).ToList();

            foreach (var key in keysToRemove)
            {
                registry.Remove(key);
            }

            // 重新注册启用的插件面板
            foreach (var (name, factory) in _pluginManager.GetEnabledPanelPlugins())
            {
                Register(name, factory);
            }
        }
    }
}
