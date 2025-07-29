using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XPanel.Plugins;
using XPlugin.logs;

namespace XPanel.Panels
{
    /// <summary>
    /// 插件管理面板
    /// </summary>
    public partial class PluginManagerPanel : UserControl
    {
        private readonly XPluginManager _pluginManager;
        private ListView _pluginListView;
        private Button _uploadButton;
        private Button _refreshButton;
        private Button _enableButton;
        private Button _disableButton;
        private Button _uninstallButton;
        private TextBox _logTextBox;

        public PluginManagerPanel(XPluginManager pluginManager)
        {
            _pluginManager = pluginManager;
            InitializeComponent();
            LoadPluginList();

            // 注册UI日志输出
            Log.RegisterOutput(new UILogOutput(AppendLogToUI));
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // 设置面板属性
            Size = new Size(800, 600);
            BackColor = Color.White;

            // 创建标题
            var titleLabel = new Label
            {
                Text = "插件管理",
                Font = new Font("Microsoft YaHei", 16F, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(200, 30),
                ForeColor = Color.DarkBlue
            };

            // 创建按钮面板
            var buttonPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(760, 50),
                BackColor = Color.LightGray
            };

            _uploadButton = new Button
            {
                Text = "上传插件",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            _uploadButton.Click += UploadButton_Click;

            _refreshButton = new Button
            {
                Text = "刷新列表",
                Location = new Point(120, 10),
                Size = new Size(100, 30),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };
            _refreshButton.Click += RefreshButton_Click;

            _enableButton = new Button
            {
                Text = "启用",
                Location = new Point(230, 10),
                Size = new Size(80, 30),
                BackColor = Color.LightYellow,
                UseVisualStyleBackColor = false
            };
            _enableButton.Click += EnableButton_Click;

            _disableButton = new Button
            {
                Text = "禁用",
                Location = new Point(320, 10),
                Size = new Size(80, 30),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };
            _disableButton.Click += DisableButton_Click;

            _uninstallButton = new Button
            {
                Text = "卸载",
                Location = new Point(410, 10),
                Size = new Size(80, 30),
                BackColor = Color.IndianRed,
                UseVisualStyleBackColor = false
            };
            _uninstallButton.Click += UninstallButton_Click;

            buttonPanel.Controls.AddRange(new Control[] 
            { 
                _uploadButton, _refreshButton, _enableButton, _disableButton, _uninstallButton 
            });

            // 创建插件列表
            _pluginListView = new ListView
            {
                Location = new Point(20, 120),
                Size = new Size(400, 300),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            // 添加列
            _pluginListView.Columns.Add("插件名称", 120);
            _pluginListView.Columns.Add("文件名", 120);
            _pluginListView.Columns.Add("状态", 60);
            _pluginListView.Columns.Add("类型", 80);

            // 创建日志显示区域
            var logLabel = new Label
            {
                Text = "操作日志:",
                Location = new Point(440, 120),
                Size = new Size(80, 20),
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold)
            };

            _logTextBox = new TextBox
            {
                Location = new Point(440, 145),
                Size = new Size(340, 275),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 8F)
            };

            // 添加控件到面板
            Controls.AddRange(new Control[] { titleLabel, buttonPanel, _pluginListView, logLabel, _logTextBox });

            ResumeLayout(false);
        }

        private void LoadPluginList()
        {
            _pluginListView.Items.Clear();

            foreach (var plugin in _pluginManager.LoadedPlugins)
            {
                var item = new ListViewItem(plugin.FileName);
                item.SubItems.Add(plugin.FileName);
                item.SubItems.Add(_pluginManager.IsPluginEnabled(plugin.FileName) ? "启用" : "禁用");
                
                // 确定插件类型
                string pluginType = "";
                if (plugin.PanelTypes.Any() && plugin.ServerTypes.Any())
                    pluginType = "面板+服务";
                else if (plugin.PanelTypes.Any())
                    pluginType = "面板";
                else if (plugin.ServerTypes.Any())
                    pluginType = "服务";
                
                item.SubItems.Add(pluginType);
                item.SubItems.Add(plugin.LoadTime.ToString("yyyy-MM-dd HH:mm:ss"));
                
                // 设置状态颜色
                if (_pluginManager.IsPluginEnabled(plugin.FileName))
                {
                    item.BackColor = Color.LightGreen;
                }
                else
                {
                    item.BackColor = Color.LightGray;
                }

                item.Tag = plugin;
                _pluginListView.Items.Add(item);
            }
        }

        private void UploadButton_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "插件文件 (XPlugin*.dll)|XPlugin*.dll|所有文件 (*.*)|*.*";
                openFileDialog.Title = "选择插件文件";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    MessageBox.Show($"准备安装插件: {openFileDialog.FileName}", "调试信息",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    bool success = _pluginManager.InstallPlugin(openFileDialog.FileName);

                    MessageBox.Show($"插件安装结果: {(success ? "成功" : "失败")}", "调试信息",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (success)
                    {
                        LoadPluginList();
                        // 通知主窗体刷新菜单
                        OnPluginChanged?.Invoke();

                        MessageBox.Show($"插件列表已刷新，当前插件数量: {_pluginManager.LoadedPlugins.Count()}", "调试信息",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            _pluginManager.LoadAllPlugins();
            LoadPluginList();
            OnPluginChanged?.Invoke();
        }

        private void EnableButton_Click(object sender, EventArgs e)
        {
            if (_pluginListView.SelectedItems.Count > 0)
            {
                var selectedItem = _pluginListView.SelectedItems[0];
                var plugin = (PluginInfo)selectedItem.Tag;
                
                _pluginManager.SetPluginEnabled(plugin.FileName, true);
                LoadPluginList();
                OnPluginChanged?.Invoke();
            }
        }

        private void DisableButton_Click(object sender, EventArgs e)
        {
            if (_pluginListView.SelectedItems.Count > 0)
            {
                var selectedItem = _pluginListView.SelectedItems[0];
                var plugin = (PluginInfo)selectedItem.Tag;
                
                _pluginManager.SetPluginEnabled(plugin.FileName, false);
                LoadPluginList();
                OnPluginChanged?.Invoke();
            }
        }

        private void UninstallButton_Click(object sender, EventArgs e)
        {
            if (_pluginListView.SelectedItems.Count > 0)
            {
                var selectedItem = _pluginListView.SelectedItems[0];
                var plugin = (PluginInfo)selectedItem.Tag;
                
                var result = MessageBox.Show($"确定要卸载插件 {plugin.FileName} 吗？", "确认卸载", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    _pluginManager.UnloadPlugin(plugin.FileName);
                    LoadPluginList();
                    OnPluginChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// 插件状态改变事件
        /// </summary>
        public event Action OnPluginChanged;

        /// <summary>
        /// 向UI日志区域添加日志
        /// </summary>
        /// <param name="message">日志消息</param>
        private void AppendLogToUI(string message)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new Action<string>(AppendLogToUI), message);
                return;
            }

            _logTextBox.AppendText(message + Environment.NewLine);
            _logTextBox.SelectionStart = _logTextBox.Text.Length;
            _logTextBox.ScrollToCaret();
        }
    }
}
