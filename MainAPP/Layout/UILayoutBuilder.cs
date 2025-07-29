namespace XPanel.Layout
{
    using System;
    using XPanel.Panels;

    /// <summary>
    /// Defines the <see cref="UILayoutBuilder" />
    /// </summary>
    public class UILayoutBuilder
    {
        /// <summary>
        /// Gets the MenuStrip
        /// </summary>
        public MenuStrip MenuStrip { get; private set; }

        /// <summary>
        /// Gets the TabControl
        /// </summary>
        public TabControl TabControl { get; private set; }

        /// <summary>
        /// Gets the StatusStrip
        /// </summary>
        public StatusStrip StatusStrip { get; private set; }

        /// <summary>
        /// Gets the TableLayoutPanel
        /// </summary>
        public TableLayoutPanel TableLayoutPanel { get; private set; }

        /// <summary>
        /// Gets the StatusLabel
        /// </summary>
        public ToolStripStatusLabel StatusLabel { get; private set; }

        /// <summary>
        /// The Build
        /// </summary>
        /// <param name="form">The form<see cref="Form"/></param>
        public void Build(Form form)
        {
            // 设置窗体基本属性
            form.Text = "XPanel - 插件化管理面板";
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(1280, 720);

            BuildTableLayoutPanel();
            BuildMenu();
            BuildTabControl();
            BuildStatusBar();

            // 控件添加顺序很重要
            form.Controls.Add(TableLayoutPanel); //添加柵格佈局
            TableLayoutPanel.Controls.Add(MenuStrip, 0, 0);
            TableLayoutPanel.Controls.Add(TabControl, 0, 1);
            TableLayoutPanel.Controls.Add(StatusStrip, 0, 2);

            
            TableLayoutPanel.BackColor = Color.LightYellow;
        }

        /// <summary>
        /// The BuildMenu
        /// </summary>
        private void BuildMenu()
        {
            MenuStrip = new MenuStrip();
            MenuStrip.Dock = DockStyle.Fill;


            var appMenu = new ToolStripMenuItem("程序");
            var configMenu = new ToolStripMenuItem("配置");
            var pluginMenu = new ToolStripMenuItem("插件");
            var aboutMenu = new ToolStripMenuItem("关于");


            // 暂不绑定事件，由 MainForm 或 TabManager 外部处理
            appMenu.DropDownItems.Add("测试程序1");
            appMenu.DropDownItems.Add("测试程序2");

            configMenu.DropDownItems.Add("系统设置");

            pluginMenu.DropDownItems.Add("插件管理");
            pluginMenu.DropDownItems.Add(new ToolStripSeparator());
            // 插件菜单项将由MainForm动态添加

            aboutMenu.DropDownItems.Add("关于程序");

            MenuStrip.Items.Add(appMenu);
            MenuStrip.Items.Add(configMenu);
            MenuStrip.Items.Add(pluginMenu);
            MenuStrip.Items.Add(aboutMenu);
        }

        /// <summary>
        /// The BuildTabControl
        /// </summary>
        private void BuildTabControl()
        {
            TabControl = new TabControl
            {
                Dock = DockStyle.Fill,
            };
            TabControl.Appearance = TabAppearance.Buttons;
        }

        /// <summary>
        /// The BuildStatusBar
        /// </summary>
        private void BuildStatusBar()
        {
            StatusStrip = new StatusStrip();
            StatusStrip.Dock = DockStyle.Bottom;

            StatusLabel = new ToolStripStatusLabel("状态：就绪");
            StatusStrip.Items.Add(StatusLabel);
        }

        /// <summary>
        /// The BuildTableLayoutPanel
        /// </summary>
        private void BuildTableLayoutPanel()
        {
            TableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            };
            TableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 0: 菜单栏
            TableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Row 1: 内容区
            TableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 2: 状态栏
        }


    }
}
