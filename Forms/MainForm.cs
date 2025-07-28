namespace XPanel.Forms
{
    using XPlugin;
    using XPanel.Layout;
    using XPanel.Registry;
    using XPanel.Tabs;

    /// <summary>
    /// Defines the <see cref="MainForm" />
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// Defines the tabManager
        /// </summary>
        private TabManager tabManager;

        /// <summary>
        /// Defines the layout
        /// </summary>
        private UILayoutBuilder layout;

        /// <summary>
        /// Defines the _plugins
        /// </summary>
        private List<IServerPlugin> _plugins = new();

        /// <summary>
        /// Defines the _pluginFolder
        /// </summary>
        private readonly string _pluginFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XPlugin");

        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            InitializeLayout();
        }

        /// <summary>
        /// The InitializeLayout
        /// </summary>
        private void InitializeLayout()
        {
            layout = new UILayoutBuilder();
            layout.Build(this);

            // 初始化 Tab 管理器
            tabManager = new TabManager(layout.TabControl, layout.StatusLabel);

            // 绑定菜单事件
            BindMenuEvents();

            // 初始化插件菜单
            RefreshPluginMenu();

            // 监听插件管理面板的变化
            SetupPluginManagerEvents();

            // 监听插件禁用/卸载事件
            PanelRegistry.PluginManager.OnPluginDisabled += OnPluginDisabled;
        }

        /// <summary>
        /// 绑定菜单事件
        /// </summary>
        private void BindMenuEvents()
        {
            foreach (ToolStripMenuItem item in layout.MenuStrip.Items)
            {
                foreach (ToolStripItem subItem in item.DropDownItems)
                {
                    if (subItem is ToolStripMenuItem menuItem)
                    {
                        menuItem.Click += (s, e) => { OpenPanel(menuItem.Text); };
                    }
                }
            }
        }

        /// <summary>
        /// 刷新插件菜单
        /// </summary>
        private void RefreshPluginMenu()
        {
            // 找到插件菜单
            var pluginMenu = layout.MenuStrip.Items.Cast<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Text == "插件");

            if (pluginMenu == null) return;

            // 清除插件菜单项（保留插件管理和分隔符）
            var itemsToRemove = pluginMenu.DropDownItems.Cast<ToolStripItem>()
                .Where(item => item.Text != "插件管理" && !(item is ToolStripSeparator))
                .ToList();

            foreach (var item in itemsToRemove)
            {
                pluginMenu.DropDownItems.Remove(item);
            }

            // 添加启用的插件面板菜单项
            foreach (var (Name, Factory) in PanelRegistry.PluginManager.GetEnabledPanelPlugins())
            {
                var menuItem = new ToolStripMenuItem(Name);
                menuItem.Click += (s, e) => OpenPanel(Name);
                pluginMenu.DropDownItems.Add(menuItem);
            }
        }

        /// <summary>
        /// 设置插件管理器事件
        /// </summary>
        private void SetupPluginManagerEvents()
        {
            // 这里需要在插件管理面板创建时设置事件监听
            // 由于面板是动态创建的，我们需要在OpenPanel方法中处理
        }

        /// <summary>
        /// The OpenPanel
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        private void OpenPanel(string name)
        {
            // 检查是否已有同名页签，如果有则激活
            foreach (TabPage tab in layout.TabControl.TabPages)
            {
                if (tab.Text == name)
                {
                    layout.TabControl.SelectedTab = tab;
                    return;
                }
            }

            var panel = PanelRegistry.CreatePanel(name);
            if (panel == null)
            {
                MessageBox.Show($"未注册面板: {name}");
                return;
            }

            // 如果是插件管理面板，设置事件监听
            if (panel is XPanel.Panels.PluginManagerPanel pluginManagerPanel)
            {
                pluginManagerPanel.OnPluginChanged += () =>
                {
                    PanelRegistry.RefreshPluginPanels();
                    RefreshPluginMenu();
                };
            }

            var newTab = new TabPage(name)
            {
                Padding = new Padding(3)
            };
            panel.Dock = DockStyle.Fill;
            newTab.Controls.Add(panel);
            panel.BackColor = Color.LightYellow;
            layout.TabControl.TabPages.Add(newTab);
            layout.TabControl.SelectedTab = newTab;
        }

        /// <summary>
        /// 处理插件被禁用或卸载事件
        /// </summary>
        /// <param name="pluginFileName">插件文件名</param>
        private void OnPluginDisabled(string pluginFileName)
        {
            // 关闭所有与该插件相关的标签页
            var tabsToRemove = new List<TabPage>();

            foreach (TabPage tab in layout.TabControl.TabPages)
            {
                // 检查标签页是否属于被禁用的插件
                if (IsPluginTab(tab, pluginFileName))
                {
                    tabsToRemove.Add(tab);
                }
            }

            // 移除标签页
            foreach (var tab in tabsToRemove)
            {
                layout.TabControl.TabPages.Remove(tab);
                tab.Dispose();
            }

            // 刷新插件菜单
            RefreshPluginMenu();
        }

        /// <summary>
        /// 检查标签页是否属于指定插件
        /// </summary>
        /// <param name="tab">标签页</param>
        /// <param name="pluginFileName">插件文件名</param>
        /// <returns>是否属于该插件</returns>
        private bool IsPluginTab(TabPage tab, string pluginFileName)
        {
            // 通过标签页名称或控件类型判断是否属于插件
            // 这里可以根据实际需要调整判断逻辑
            if (tab.Controls.Count > 0)
            {
                var control = tab.Controls[0];
                var controlType = control.GetType();

                // 检查控件类型是否来自指定插件程序集
                var assembly = controlType.Assembly;
                var assemblyName = assembly.GetName().Name;

                // 如果程序集名称与插件文件名匹配（去掉.dll扩展名）
                var pluginName = Path.GetFileNameWithoutExtension(pluginFileName);
                return assemblyName?.Equals(pluginName, StringComparison.OrdinalIgnoreCase) == true;
            }

            return false;
        }
    }
}