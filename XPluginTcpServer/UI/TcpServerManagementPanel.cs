using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XPlugin.Theme;
using XPluginTcpServer.Services;
using XPluginTcpServer.Models;
using XPlugin.logs;

namespace XPluginTcpServer.UI
{
    /// <summary>
    /// TCP服务器管理面板
    /// </summary>
    public class TcpServerManagementPanel : UserControl
    {
        private readonly ConfigManager _configManager;
        private readonly RealTcpServerManager _serverManager;
        private ListView _serverListView = null!;
        private TextBox _logTextBox = null!;
        private Timer _refreshTimer = null!;
        private readonly Dictionary<string, List<string>> _serverLogs = new Dictionary<string, List<string>>();
        private string _currentLogServerId = "全部";

        public TcpServerManagementPanel()
        {
            _configManager = new ConfigManager();
            _serverManager = new RealTcpServerManager(_configManager);
            InitializeComponent();
            LoadServerList();

            // 注册事件
            _serverManager.ServerStatusChanged += OnServerStatusChanged;
            _serverManager.ClientConnectionChanged += OnClientConnectionChanged;

            // 注册日志输出
            Log.RegisterOutput(new UILogOutput(message => AppendLog(message)));
            AppendLog("TCP服务器管理面板已启动");

            // 注册面板关闭事件
            this.HandleDestroyed += TcpServerManagementPanel_HandleDestroyed;

            // 启动自动启动的服务器
            _ = Task.Run(async () => await _serverManager.StartAutoStartServersAsync());

            // 应用当前主题
            ThemeManager.ApplyTheme(this);

            // 监听主题变化
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void InitializeComponent()
        {
            Size = new Size(1200, 800);
            BackColor = Color.White;
            Dock = DockStyle.Fill; // 支持窗口最大化时同步放大

            // 标题
            var titleLabel = new Label
            {
                Text = "TCP服务器管理面板",
                Font = new Font("Microsoft YaHei", 16F, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(300, 30),
                ForeColor = Color.DarkBlue,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // 左侧面板 - 服务器和客户端信息
            var leftPanel = new Panel
            {
                Location = new Point(10, 50),
                Size = new Size(480, 740),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };

            // 服务器管理组
            var serverGroup = new GroupBox
            {
                Text = "TCP服务器管理",
                Location = new Point(5, 5),
                Size = new Size(465, 360),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // 服务器列表
            _serverListView = new ListView
            {
                Location = new Point(10, 25),
                Size = new Size(445, 280),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _serverListView.Columns.Add("名称", 100);
            _serverListView.Columns.Add("地址", 100);
            _serverListView.Columns.Add("端口", 60);
            _serverListView.Columns.Add("状态", 60);
            _serverListView.Columns.Add("自动启动", 70);
            _serverListView.Columns.Add("描述", 100);

            // 服务器操作按钮
            var serverButtonPanel = new Panel
            {
                Location = new Point(10, 315),
                Size = new Size(445, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var addButton = new Button
            {
                Text = "添加服务器",
                Location = new Point(0, 5),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };
            addButton.Click += AddButton_Click;

            var startButton = new Button
            {
                Text = "启动服务器",
                Location = new Point(90, 5),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };
            startButton.Click += StartButton_Click;

            var stopButton = new Button
            {
                Text = "停止服务器",
                Location = new Point(180, 5),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };
            stopButton.Click += StopButton_Click;

            var editButton = new Button
            {
                Text = "编辑",
                Location = new Point(270, 5),
                Size = new Size(50, 25),
                UseVisualStyleBackColor = true
            };
            editButton.Click += EditButton_Click;

            var deleteButton = new Button
            {
                Text = "删除",
                Location = new Point(330, 5),
                Size = new Size(50, 25),
                UseVisualStyleBackColor = true
            };
            deleteButton.Click += DeleteButton_Click;

            serverButtonPanel.Controls.AddRange(new Control[] { addButton, startButton, stopButton, editButton, deleteButton });
            serverGroup.Controls.AddRange(new Control[] { _serverListView, serverButtonPanel });

            // 客户端连接组
            var clientGroup = new GroupBox
            {
                Text = "客户端连接监控",
                Location = new Point(5, 375),
                Size = new Size(465, 360),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var clientListView = new ListView
            {
                Location = new Point(10, 25),
                Size = new Size(445, 280),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            clientListView.Columns.Add("连接ID", 80);
            clientListView.Columns.Add("客户端IP", 100);
            clientListView.Columns.Add("端口", 60);
            clientListView.Columns.Add("连接时间", 120);
            clientListView.Columns.Add("状态", 60);

            var clientButtonPanel = new Panel
            {
                Location = new Point(10, 315),
                Size = new Size(445, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var refreshButton = new Button
            {
                Text = "刷新连接",
                Location = new Point(0, 5),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };

            var kickButton = new Button
            {
                Text = "断开连接",
                Location = new Point(90, 5),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };

            clientButtonPanel.Controls.AddRange(new Control[] { refreshButton, kickButton });
            clientGroup.Controls.AddRange(new Control[] { clientListView, clientButtonPanel });

            leftPanel.Controls.AddRange(new Control[] { serverGroup, clientGroup });

            // 右侧面板 - 日志区域
            var rightPanel = new Panel
            {
                Location = new Point(500, 50),
                Size = new Size(690, 740),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var logGroup = new GroupBox
            {
                Text = "服务器日志监控",
                Location = new Point(5, 5),
                Size = new Size(675, 725),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // 日志过滤器
            var filterPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(655, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var filterLabel = new Label
            {
                Text = "日志过滤:",
                Location = new Point(0, 8),
                Size = new Size(60, 20),
                Font = new Font("Microsoft YaHei", 9F)
            };

            var filterComboBox = new ComboBox
            {
                Location = new Point(65, 5),
                Size = new Size(120, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            filterComboBox.Items.AddRange(new[] { "全部", "INFO", "WARN", "ERROR" });
            filterComboBox.SelectedIndex = 0;

            var clearLogButton = new Button
            {
                Text = "清空日志",
                Location = new Point(200, 5),
                Size = new Size(70, 25),
                UseVisualStyleBackColor = true
            };
            clearLogButton.Click += ClearLogButton_Click;

            filterPanel.Controls.AddRange(new Control[] { filterLabel, filterComboBox, clearLogButton });

            // 日志文本框
            _logTextBox = new TextBox
            {
                Location = new Point(10, 70),
                Size = new Size(655, 645),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LimeGreen,
                Font = new Font("Consolas", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            logGroup.Controls.AddRange(new Control[] { filterPanel, _logTextBox });
            rightPanel.Controls.Add(logGroup);

            // 添加所有控件到主面板
            Controls.AddRange(new Control[] { titleLabel, leftPanel, rightPanel });

            // 启动定时器
            _refreshTimer = new Timer
            {
                Interval = 2000 // 2秒刷新一次
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void OnThemeChanged(ThemeConfig theme)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ThemeConfig>(OnThemeChanged), theme);
                return;
            }

            ThemeManager.ApplyTheme(this);
        }

        private void LoadServerList()
        {
            try
            {
                _serverListView.Items.Clear();
                var configs = _configManager.GetAllConfigs();

                foreach (var config in configs)
                {
                    var item = new ListViewItem(config.Name);
                    item.SubItems.Add(config.IpAddress);
                    item.SubItems.Add(config.Port.ToString());
                    item.SubItems.Add(config.IsRunning ? "运行中" : "已停止");
                    item.SubItems.Add(config.AutoStart ? "是" : "否");
                    item.SubItems.Add(config.Description);
                    item.Tag = config.Id;

                    _serverListView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 加载服务器列表失败: {ex.Message}");
            }
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendLog), message);
                return;
            }

            try
            {
                if (_logTextBox != null)
                {
                    _logTextBox.AppendText($"{DateTime.Now:HH:mm:ss} {message}\r\n");
                    _logTextBox.SelectionStart = _logTextBox.Text.Length;
                    _logTextBox.ScrollToCaret();

                    // 限制日志长度，避免内存占用过大
                    if (_logTextBox.Lines.Length > 1000)
                    {
                        var lines = _logTextBox.Lines.Skip(200).ToArray();
                        _logTextBox.Lines = lines;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"日志输出失败: {ex.Message}");
            }
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dialog = new TcpServerConfigDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var config = dialog.GetServerConfig();
                    _configManager.AddConfig(config);
                    LoadServerList();
                    AppendLog($"INFO: 添加服务器配置: {config.Name}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 添加服务器失败: {ex.Message}");
                MessageBox.Show($"添加服务器失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartButton_Click(object? sender, EventArgs e)
        {
            StartSelectedServers();
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            StopSelectedServers();
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            if (_serverListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要编辑的服务器", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var selectedItem = _serverListView.SelectedItems[0];
                var serverId = selectedItem.Tag?.ToString();
                if (string.IsNullOrEmpty(serverId)) return;

                var config = _configManager.GetConfig(serverId);
                if (config == null)
                {
                    MessageBox.Show("服务器配置不存在", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using var dialog = new TcpServerConfigDialog(config);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var updatedConfig = dialog.GetServerConfig();
                    updatedConfig.Id = serverId;
                    _configManager.UpdateConfig(updatedConfig);
                    LoadServerList();
                    AppendLog($"INFO: 更新服务器配置: {updatedConfig.Name}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 编辑服务器失败: {ex.Message}");
                MessageBox.Show($"编辑服务器失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (_serverListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要删除的服务器", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("确定要删除选中的服务器配置吗？", "确认删除", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                try
                {
                    var selectedItem = _serverListView.SelectedItems[0];
                    var serverId = selectedItem.Tag?.ToString();
                    if (string.IsNullOrEmpty(serverId)) return;

                    // 先停止服务器
                    _ = Task.Run(async () => await _serverManager.StopServerAsync(serverId));

                    // 删除配置
                    _configManager.DeleteConfig(serverId);
                    LoadServerList();
                    AppendLog($"INFO: 删除服务器配置: {serverId}");
                }
                catch (Exception ex)
                {
                    AppendLog($"ERROR: 删除服务器失败: {ex.Message}");
                    MessageBox.Show($"删除服务器失败: {ex.Message}", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ClearLogButton_Click(object? sender, EventArgs e)
        {
            _logTextBox.Clear();
            AppendLog("INFO: 日志已清空");
        }

        private void StartSelectedServers()
        {
            if (_serverListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要启动的服务器", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (ListViewItem item in _serverListView.SelectedItems)
            {
                var serverId = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(serverId))
                {
                    _ = Task.Run(async () => await _serverManager.StartServerAsync(serverId));
                }
            }
        }

        private void StopSelectedServers()
        {
            if (_serverListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要停止的服务器", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (ListViewItem item in _serverListView.SelectedItems)
            {
                var serverId = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(serverId))
                {
                    _ = Task.Run(async () => await _serverManager.StopServerAsync(serverId));
                }
            }
        }

        private void OnServerStatusChanged(string serverId, bool isRunning)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, bool>(OnServerStatusChanged), serverId, isRunning);
                return;
            }

            LoadServerList();
            AppendLog($"INFO: 服务器 {serverId} 状态变更: {(isRunning ? "启动" : "停止")}");
        }

        private void OnClientConnectionChanged(string serverId, ClientConnectionInfo connectionInfo, bool isConnected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, ClientConnectionInfo, bool>(OnClientConnectionChanged), serverId, connectionInfo, isConnected);
                return;
            }

            AppendLog($"INFO: 客户端连接变更 - 服务器: {serverId}, 连接: {connectionInfo.ConnectionId}, 状态: {(isConnected ? "连接" : "断开")}");
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                LoadServerList();
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 刷新界面失败: {ex.Message}");
            }
        }

        private void TcpServerManagementPanel_HandleDestroyed(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("TCP服务器管理面板Handle被销毁事件触发");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleDestroyed处理失败: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("TCP服务器管理面板Dispose被调用");

                    // 停止定时器
                    _refreshTimer?.Stop();
                    _refreshTimer?.Dispose();

                    // 停止所有TCP服务器
                    _ = Task.Run(async () => await _serverManager.StopAllServersAsync());

                    // 取消事件注册
                    _serverManager.ServerStatusChanged -= OnServerStatusChanged;
                    _serverManager.ClientConnectionChanged -= OnClientConnectionChanged;
                    this.HandleDestroyed -= TcpServerManagementPanel_HandleDestroyed;
                    ThemeManager.ThemeChanged -= OnThemeChanged;

                    System.Diagnostics.Debug.WriteLine("TCP服务器管理面板Dispose完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Dispose处理失败: {ex.Message}");
                }
            }
            base.Dispose(disposing);
        }
    }
}
