using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XPlugin;
using XPlugin.Theme;
using XPluginTcpServer.Services;
using XPlugin.logs;

namespace XPluginTcpServer
{
    /// <summary>
    /// TCP服务器插件 - 简化实用版本
    /// </summary>
    public class TcpServerPlugin : IXPanelInterface
    {
        private static SimpleTcpPanel? _currentPanel;

        public string Name => "TCP服务器管理";

        public UserControl CreatePanel()
        {
            try
            {
                // 如果已有面板实例，先清理
                if (_currentPanel != null)
                {
                    _currentPanel.Dispose();
                    _currentPanel = null;
                }

                _currentPanel = new SimpleTcpPanel();
                return _currentPanel;
            }
            catch (Exception ex)
            {
                // 如果创建面板失败，返回错误信息面板
                var errorPanel = new UserControl
                {
                    Size = new Size(800, 600),
                    BackColor = Color.LightPink
                };

                var errorLabel = new Label
                {
                    Text = $"TCP服务器插件加载失败：\n{ex.Message}",
                    Location = new Point(20, 20),
                    Size = new Size(750, 200),
                    Font = new Font("Microsoft YaHei", 10F),
                    ForeColor = Color.Red
                };

                errorPanel.Controls.Add(errorLabel);
                return errorPanel;
            }
        }

        /// <summary>
        /// 清理资源（当插件被卸载时调用）
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (_currentPanel != null)
                {
                    System.Diagnostics.Debug.WriteLine("开始清理TCP插件静态资源...");
                    _currentPanel.Dispose();
                    _currentPanel = null;
                    System.Diagnostics.Debug.WriteLine("TCP插件静态资源清理完成");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理TCP插件资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 当面板被外部释放时调用此方法清理静态引用
        /// </summary>
        internal static void OnPanelDisposed(SimpleTcpPanel panel)
        {
            try
            {
                if (_currentPanel == panel)
                {
                    System.Diagnostics.Debug.WriteLine("TCP面板被外部释放，清理静态引用");
                    
                    _currentPanel = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理TCP面板静态引用失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 简化的TCP管理面板
    /// </summary>
    public class SimpleTcpPanel : UserControl
    {
        private readonly ConfigManager _configManager;
        private readonly RealTcpServerManager _serverManager;
        private ListView _serverListView = null!;
        private TextBox _logTextBox = null!;
        private Timer _refreshTimer = null!;
        private readonly Dictionary<string, List<string>> _serverLogs = new Dictionary<string, List<string>>();
        private string _currentLogServerId = "全部";

        public SimpleTcpPanel()
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
            this.HandleDestroyed += SimpleTcpPanel_HandleDestroyed;

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

            // 服务器列表区域
            var serverGroup = new GroupBox
            {
                Text = "TCP服务器列表",
                Location = new Point(5, 5),
                Size = new Size(465, 280),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _serverListView = new ListView
            {
                Location = new Point(10, 25),
                Size = new Size(445, 200),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _serverListView.Columns.Add("名称", 120);
            _serverListView.Columns.Add("地址", 100);
            _serverListView.Columns.Add("端口", 60);
            _serverListView.Columns.Add("状态", 80);
            _serverListView.Columns.Add("连接数", 80);

            // 添加选择变化事件
            _serverListView.SelectedIndexChanged += ServerListView_SelectedIndexChanged;

            // 添加双击事件
            _serverListView.DoubleClick += ServerListView_DoubleClick;

            // 添加右键菜单
            _serverListView.MouseClick += ServerListView_MouseClick;

            // 按钮面板
            var buttonPanel = new Panel
            {
                Location = new Point(10, 230),
                Size = new Size(445, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var startButton = new Button
            {
                Text = "启动",
                Location = new Point(0, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };
            startButton.Click += StartButton_Click;

            var stopButton = new Button
            {
                Text = "停止",
                Location = new Point(80, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };
            stopButton.Click += StopButton_Click;

            var addButton = new Button
            {
                Text = "添加",
                Location = new Point(160, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            addButton.Click += AddButton_Click;

            var editButton = new Button
            {
                Text = "编辑",
                Location = new Point(240, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            editButton.Click += EditButton_Click;

            var deleteButton = new Button
            {
                Text = "删除",
                Location = new Point(320, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };
            deleteButton.Click += DeleteButton_Click;

            buttonPanel.Controls.AddRange(new Control[] { startButton, stopButton, addButton, editButton, deleteButton });
            serverGroup.Controls.AddRange(new Control[] { _serverListView, buttonPanel });

            // 客户端连接区域
            var clientGroup = new GroupBox
            {
                Text = "客户端连接信息",
                Location = new Point(5, 290),
                Size = new Size(465, 440),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var clientListView = new ListView
            {
                Name = "clientListView",
                Location = new Point(10, 25),
                Size = new Size(445, 360),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            clientListView.Columns.Add("连接ID", 80);
            clientListView.Columns.Add("客户端IP", 100);
            clientListView.Columns.Add("端口", 60);
            clientListView.Columns.Add("连接时间", 120);
            clientListView.Columns.Add("状态", 60);

            // 客户端操作按钮
            var clientButtonPanel = new Panel
            {
                Location = new Point(10, 390),
                Size = new Size(445, 40),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var refreshButton = new Button
            {
                Text = "刷新连接",
                Location = new Point(0, 5),
                Size = new Size(100, 30),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            refreshButton.Click += RefreshButton_Click;

            var kickButton = new Button
            {
                Text = "踢出客户端",
                Location = new Point(110, 5),
                Size = new Size(100, 30),
                BackColor = Color.Orange,
                UseVisualStyleBackColor = false
            };
            kickButton.Click += KickButton_Click;

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

            // 日志过滤控件
            var logFilterPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(655, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var logFilterLabel = new Label
            {
                Text = "服务器日志:",
                Location = new Point(0, 8),
                Size = new Size(80, 20),
                Font = new Font("Microsoft YaHei", 9F)
            };

            var logFilterComboBox = new ComboBox
            {
                Name = "logFilterComboBox",
                Location = new Point(85, 5),
                Size = new Size(180, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei", 9F)
            };
            logFilterComboBox.SelectedIndexChanged += LogFilterComboBox_SelectedIndexChanged;

            var allLogButton = new Button
            {
                Text = "全部",
                Location = new Point(275, 4),
                Size = new Size(50, 27),
                Font = new Font("Microsoft YaHei", 9F),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            allLogButton.Click += (s, e) => { _currentLogServerId = "全部"; RefreshLogDisplay(); };

            var systemLogButton = new Button
            {
                Text = "系统",
                Location = new Point(330, 4),
                Size = new Size(50, 27),
                Font = new Font("Microsoft YaHei", 9F),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };
            systemLogButton.Click += (s, e) => { _currentLogServerId = "系统"; RefreshLogDisplay(); };

            var clearLogButton = new Button
            {
                Text = "清空日志",
                Location = new Point(385, 4),
                Size = new Size(80, 27),
                Font = new Font("Microsoft YaHei", 9F),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };
            clearLogButton.Click += ClearLogButton_Click;

            logFilterPanel.Controls.AddRange(new Control[] { logFilterLabel, logFilterComboBox, allLogButton, systemLogButton, clearLogButton });

            _logTextBox = new TextBox
            {
                Location = new Point(10, 65),
                Size = new Size(655, 650),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            logGroup.Controls.AddRange(new Control[] { logFilterPanel, _logTextBox });
            rightPanel.Controls.Add(logGroup);

            // 添加到主面板
            Controls.AddRange(new Control[] { titleLabel, leftPanel, rightPanel });

            // 定时器 - 降低刷新频率，避免频繁重置选中状态
            _refreshTimer = new Timer { Interval = 5000, Enabled = true };
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void LoadServerList()
        {
            try
            {
                // 保存当前选中的服务器ID
                string? selectedServerId = null;
                if (_serverListView.SelectedItems.Count > 0)
                {
                    selectedServerId = _serverListView.SelectedItems[0].Tag?.ToString();
                }

                _serverListView.Items.Clear();
                var configs = _configManager.GetAllConfigs();

                ListViewItem? itemToSelect = null;
                foreach (var config in configs)
                {
                    var item = new ListViewItem(config.Name);
                    item.SubItems.Add(config.IpAddress);
                    item.SubItems.Add(config.Port.ToString());
                    item.SubItems.Add(config.IsRunning ? "运行中" : "已停止");
                    item.SubItems.Add(config.CurrentConnections.ToString());
                    item.Tag = config.Id;

                    if (config.IsRunning)
                    {
                        item.BackColor = Color.LightGreen;
                        item.ForeColor = Color.DarkGreen;
                    }
                    else
                    {
                        item.BackColor = ThemeManager.CurrentTheme.ListBackgroundColor;
                        item.ForeColor = ThemeManager.CurrentTheme.ForegroundColor;
                    }

                    _serverListView.Items.Add(item);

                    // 如果这是之前选中的项，记录下来
                    if (config.Id == selectedServerId)
                    {
                        itemToSelect = item;
                    }
                }

                // 恢复选中状态
                if (itemToSelect != null)
                {
                    itemToSelect.Selected = true;
                    itemToSelect.Focused = true;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"加载服务器列表失败: {ex.Message}");
            }
        }

        private void LoadClientList()
        {
            try
            {
                var clientListView = Controls.Find("clientListView", true).FirstOrDefault() as ListView;
                if (clientListView == null) return;

                // 保存当前选中的客户端连接ID
                string? selectedConnectionId = null;
                if (clientListView.SelectedItems.Count > 0)
                {
                    var selectedConnection = clientListView.SelectedItems[0].Tag as Models.ClientConnectionInfo;
                    selectedConnectionId = selectedConnection?.ConnectionId;
                }

                clientListView.Items.Clear();

                if (_serverListView.SelectedItems.Count > 0)
                {
                    var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                    if (!string.IsNullOrEmpty(serverId))
                    {
                        var connections = GetServerConnections(serverId);
                        ListViewItem? itemToSelect = null;

                        foreach (var connection in connections.OrderByDescending(c => c.ConnectedTime))
                        {
                            var item = new ListViewItem(connection.ConnectionId);
                            item.SubItems.Add(connection.ClientIp);
                            item.SubItems.Add(connection.ClientPort.ToString());
                            item.SubItems.Add(connection.ConnectedTime.ToString("HH:mm:ss"));
                            item.SubItems.Add(connection.IsConnected ? "已连接" : "已断开");
                            item.Tag = connection;

                            if (connection.IsConnected)
                            {
                                item.BackColor = Color.LightGreen;
                                item.ForeColor = Color.DarkGreen;
                            }
                            else
                            {
                                item.BackColor = ThemeManager.CurrentTheme.ListBackgroundColor;
                                item.ForeColor = ThemeManager.CurrentTheme.ForegroundColor;
                            }

                            clientListView.Items.Add(item);

                            // 如果这是之前选中的项，记录下来
                            if (connection.ConnectionId == selectedConnectionId)
                            {
                                itemToSelect = item;
                            }
                        }

                        // 恢复选中状态
                        if (itemToSelect != null)
                        {
                            itemToSelect.Selected = true;
                            itemToSelect.Focused = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"加载客户端列表失败: {ex.Message}");
            }
        }

        private List<Models.ClientConnectionInfo> GetServerConnections(string serverId)
        {
            return _serverManager.GetServerConnections(serverId);
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            LoadServerList();
            LoadClientList();
        }

        private void ServerListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // 只有选中有效行时才刷新客户端列表
            if (_serverListView.SelectedItems.Count > 0 && _serverListView.SelectedItems[0].Tag != null)
            {
                LoadClientList();
            }
            else
            {
                // 清空客户端列表
                var clientListView = Controls.Find("clientListView", true).FirstOrDefault() as ListView;
                clientListView?.Items.Clear();
            }
        }

        private void ServerListView_DoubleClick(object? sender, EventArgs e)
        {
            try
            {
                if (_serverListView.SelectedItems.Count > 0 && _serverListView.SelectedItems[0].Tag != null)
                {
                    var serverId = _serverListView.SelectedItems[0].Tag.ToString();
                    if (!string.IsNullOrEmpty(serverId))
                    {
                        var config = _configManager.GetConfig(serverId);
                        if (config != null)
                        {
                            if (!config.IsRunning)
                            {
                                StartButton_Click(sender, e);
                            }
                            else
                            {
                                AppendLog($"服务器 {config.Name} 已在运行中", config.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"双击启动服务器失败: {ex.Message}");
            }
        }

        private void ServerListView_MouseClick(object? sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right)
                {
                    var contextMenu = new ContextMenuStrip();

                    // 检查是否点击在有效行上
                    var hitTest = _serverListView.HitTest(e.Location);
                    bool hasValidItem = hitTest.Item != null && hitTest.Item.Tag != null;

                    if (hasValidItem)
                    {
                        // 选中右键点击的项
                        _serverListView.SelectedItems.Clear();
                        hitTest.Item.Selected = true;

                        var serverId = hitTest.Item.Tag.ToString();
                        var config = _configManager.GetConfig(serverId);

                        if (config != null)
                        {
                            // 根据服务器状态添加相应菜单项
                            if (!config.IsRunning)
                            {
                                contextMenu.Items.Add("启动", null, (s, args) => StartSelectedServers());
                            }
                            else
                            {
                                contextMenu.Items.Add("停止", null, (s, args) => StopSelectedServers());
                            }

                            contextMenu.Items.Add(new ToolStripSeparator());
                            contextMenu.Items.Add("编辑", null, (s, args) => EditButton_Click(s, args));
                            contextMenu.Items.Add("删除", null, (s, args) => DeleteSelectedServers());
                            contextMenu.Items.Add(new ToolStripSeparator());
                            contextMenu.Items.Add("查看日志", null, (s, args) => ViewServerLog(serverId));
                        }
                    }
                    else
                    {
                        // 在空行处右键，只显示添加菜单
                        contextMenu.Items.Add("添加", null, (s, args) => AddButton_Click(s, args));
                    }

                    if (contextMenu.Items.Count > 0)
                    {
                        contextMenu.Show(_serverListView, e.Location);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"显示右键菜单失败: {ex.Message}");
            }
        }

        private void StartSelectedServers()
        {
            try
            {
                var selectedItems = _serverListView.SelectedItems.Cast<ListViewItem>().ToList();
                if (selectedItems.Count == 0)
                {
                    AppendLog("请先选择要启动的服务器");
                    return;
                }

                foreach (var item in selectedItems)
                {
                    if (item.Tag != null)
                    {
                        var serverId = item.Tag.ToString();
                        var config = _configManager.GetConfig(serverId);
                        if (config != null && !config.IsRunning)
                        {
                            _ = Task.Run(async () =>
                            {
                                var success = await _serverManager.StartServerAsync(serverId);
                                if (success)
                                {
                                    AppendLog($"服务器启动成功: {config.Name}", config.Name);
                                }
                                else
                                {
                                    AppendLog($"服务器启动失败: {config.Name}", config.Name);
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"批量启动服务器失败: {ex.Message}");
            }
        }

        private void StopSelectedServers()
        {
            try
            {
                var selectedItems = _serverListView.SelectedItems.Cast<ListViewItem>().ToList();
                if (selectedItems.Count == 0)
                {
                    AppendLog("请先选择要停止的服务器");
                    return;
                }

                foreach (var item in selectedItems)
                {
                    if (item.Tag != null)
                    {
                        var serverId = item.Tag.ToString();
                        var config = _configManager.GetConfig(serverId);
                        if (config != null && config.IsRunning)
                        {
                            _ = Task.Run(async () =>
                            {
                                var success = await _serverManager.StopServerAsync(serverId);
                                if (success)
                                {
                                    AppendLog($"服务器停止成功: {config.Name}", config.Name);
                                }
                                else
                                {
                                    AppendLog($"服务器停止失败: {config.Name}", config.Name);
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"批量停止服务器失败: {ex.Message}");
            }
        }

        private void DeleteSelectedServers()
        {
            try
            {
                var selectedItems = _serverListView.SelectedItems.Cast<ListViewItem>().ToList();
                if (selectedItems.Count == 0)
                {
                    AppendLog("请先选择要删除的服务器");
                    return;
                }

                // 检查是否有正在运行的服务器
                var runningServers = new List<string>();
                foreach (var item in selectedItems)
                {
                    if (item.Tag != null)
                    {
                        var serverId = item.Tag.ToString();
                        var config = _configManager.GetConfig(serverId);
                        if (config != null && config.IsRunning)
                        {
                            runningServers.Add(config.Name);
                        }
                    }
                }

                if (runningServers.Count > 0)
                {
                    AppendLog($"无法删除正在运行的服务器: {string.Join(", ", runningServers)}");
                    return;
                }

                var serverNames = selectedItems
                    .Where(item => item.Tag != null)
                    .Select(item => _configManager.GetConfig(item.Tag.ToString())?.Name ?? "未知")
                    .ToList();

                var result = MessageBox.Show(
                    $"确定要删除以下服务器吗？\n{string.Join("\n", serverNames)}",
                    "确认删除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    foreach (var item in selectedItems)
                    {
                        if (item.Tag != null)
                        {
                            var serverId = item.Tag.ToString();
                            _configManager.DeleteConfig(serverId);
                            AppendLog($"已删除服务器配置: {_configManager.GetConfig(serverId)?.Name ?? serverId}");
                        }
                    }
                    LoadServerList();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"批量删除服务器失败: {ex.Message}");
            }
        }

        private void ViewServerLog(string serverId)
        {
            try
            {
                var config = _configManager.GetConfig(serverId);
                if (config != null)
                {
                    // 切换到对应服务器的日志视图
                    var displayText = GetLogDisplayText(serverId);
                    var logKey = GetLogKey(serverId);

                    // 确保日志键存在
                    if (!_serverLogs.ContainsKey(logKey))
                    {
                        _serverLogs[logKey] = new List<string>();
                    }

                    // 更新下拉框
                    UpdateLogFilterComboBox(serverId);

                    // 设置当前日志服务器ID并刷新显示
                    _currentLogServerId = displayText;
                    RefreshLogDisplay();

                    // 更新下拉框选中项
                    var logFilterComboBox = Controls.Find("logFilterComboBox", true).FirstOrDefault() as ComboBox;
                    if (logFilterComboBox != null && !logFilterComboBox.IsDisposed)
                    {
                        for (int i = 0; i < logFilterComboBox.Items.Count; i++)
                        {
                            if (logFilterComboBox.Items[i].ToString() == displayText)
                            {
                                logFilterComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }

                    AppendLog($"已切换到服务器 {config.Name} 的日志视图", serverId);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"查看服务器日志失败: {ex.Message}");
            }
        }

        private void OnThemeChanged(ThemeConfig theme)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<ThemeConfig>(OnThemeChanged), theme);
                    return;
                }

                ThemeManager.ApplyTheme(this);
            }
            catch (Exception ex)
            {
                AppendLog($"应用主题失败: {ex.Message}");
            }
        }

        private void LogFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            try
            {
                var comboBox = sender as ComboBox;
                if (comboBox?.SelectedItem != null)
                {
                    _currentLogServerId = comboBox.SelectedItem.ToString() ?? "全部";
                    RefreshLogDisplay();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"切换日志过滤失败: {ex.Message}");
            }
        }

        private void ClearLogButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_currentLogServerId == "全部")
                {
                    _serverLogs.Clear();
                    _logTextBox.Clear();
                    AppendLog("所有日志已清空");
                }
                else
                {
                    if (_serverLogs.ContainsKey(_currentLogServerId))
                    {
                        _serverLogs[_currentLogServerId].Clear();
                        RefreshLogDisplay();
                        AppendLog($"服务器 {_currentLogServerId} 的日志已清空");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"清空日志失败: {ex.Message}");
            }
        }

        private void RefreshLogDisplay()
        {
            try
            {
                _logTextBox.Clear();

                if (_currentLogServerId == "全部")
                {
                    // 显示所有服务器的日志
                    var allLogs = new List<(DateTime time, string message)>();
                    foreach (var kvp in _serverLogs)
                    {
                        var displayName = kvp.Key == "系统" ? "系统" : GetDisplayNameFromLogKey(kvp.Key);
                        foreach (var log in kvp.Value)
                        {
                            if (DateTime.TryParse(log.Substring(1, 19), out var logTime))
                            {
                                allLogs.Add((logTime, $"[{displayName}] {log}"));
                            }
                            else
                            {
                                allLogs.Add((DateTime.Now, $"[{displayName}] {log}"));
                            }
                        }
                    }

                    // 按时间排序
                    allLogs.Sort((a, b) => a.time.CompareTo(b.time));

                    foreach (var log in allLogs)
                    {
                        _logTextBox.AppendText(log.message + Environment.NewLine);
                    }
                }
                else
                {
                    // 显示特定服务器的日志
                    var logKey = _currentLogServerId == "系统" ? "系统" : GetLogKeyFromDisplayText(_currentLogServerId);
                    if (_serverLogs.TryGetValue(logKey, out var logs))
                    {
                        foreach (var log in logs)
                        {
                            _logTextBox.AppendText(log + Environment.NewLine);
                        }
                    }
                }

                _logTextBox.SelectionStart = _logTextBox.Text.Length;
                _logTextBox.ScrollToCaret();
            }
            catch (Exception ex)
            {
                _logTextBox.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 刷新日志显示失败: {ex.Message}{Environment.NewLine}");
            }
        }

        private string GetDisplayNameFromLogKey(string logKey)
        {
            if (logKey == "系统") return "系统";

            // 从所有配置中查找匹配的服务器
            var configs = _configManager.GetAllConfigs();
            var config = configs.FirstOrDefault(c => $"{c.IpAddress}:{c.Port}" == logKey);
            return config?.Name ?? logKey;
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var form = new Form
                {
                    Text = "添加TCP服务器",
                    Size = new Size(400, 350),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false
                };

                var nameLabel = new Label { Text = "服务器名称:", Location = new Point(20, 20), Size = new Size(80, 20) };
                var nameBox = new TextBox { Location = new Point(110, 18), Size = new Size(250, 20), Text = "TCP服务器" };

                var ipLabel = new Label { Text = "IP地址:", Location = new Point(20, 50), Size = new Size(80, 20) };
                var ipBox = new TextBox { Location = new Point(110, 48), Size = new Size(250, 20), Text = "127.0.0.1" };

                var portLabel = new Label { Text = "端口:", Location = new Point(20, 80), Size = new Size(80, 20) };
                var portBox = new NumericUpDown { Location = new Point(110, 78), Size = new Size(250, 20), Minimum = 1, Maximum = 65535, Value = 8080 };

                var autoStartBox = new CheckBox { Text = "自动启动", Location = new Point(110, 110), Size = new Size(100, 20) };

                var descLabel = new Label { Text = "描述:", Location = new Point(20, 140), Size = new Size(80, 20) };
                var descBox = new TextBox { Location = new Point(110, 138), Size = new Size(250, 60), Multiline = true };

                var okButton = new Button { Text = "确定", Location = new Point(200, 220), Size = new Size(75, 25), DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "取消", Location = new Point(285, 220), Size = new Size(75, 25), DialogResult = DialogResult.Cancel };

                form.Controls.AddRange(new Control[] { nameLabel, nameBox, ipLabel, ipBox, portLabel, portBox, autoStartBox, descLabel, descBox, okButton, cancelButton });

                if (form.ShowDialog() == DialogResult.OK)
                {
                    var config = new Models.TcpServerConfig
                    {
                        Name = nameBox.Text,
                        IpAddress = ipBox.Text,
                        Port = (int)portBox.Value,
                        AutoStart = autoStartBox.Checked,
                        Description = descBox.Text
                    };

                    _configManager.AddConfig(config);
                    LoadServerList();
                    AppendLog($"添加服务器: {config.Name} ({config.IpAddress}:{config.Port})");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"添加服务器失败: {ex.Message}");
            }
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_serverListView.SelectedItems.Count == 0)
                {
                    MessageBox.Show("请选择要编辑的服务器", "提示");
                    return;
                }

                if (_serverListView.SelectedItems.Count > 1)
                {
                    MessageBox.Show("编辑操作只能选择一个服务器", "提示");
                    return;
                }

                var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                if (string.IsNullOrEmpty(serverId)) return;

                var config = _configManager.GetConfig(serverId);
                if (config == null) return;

                if (config.IsRunning)
                {
                    MessageBox.Show("请先停止服务器再进行编辑", "提示");
                    return;
                }

                // 创建编辑对话框（复用添加对话框的逻辑）
                using var form = CreateServerConfigDialog(config);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _configManager.UpdateConfig(config);
                    LoadServerList();

                    // 刷新日志下拉框
                    RefreshLogFilterComboBox();

                    AppendLog($"更新服务器配置: {config.Name}", serverId);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"编辑服务器失败: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            DeleteSelectedServers();
        }

        private async void KickButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var clientListView = Controls.Find("clientListView", true).FirstOrDefault() as ListView;
                if (clientListView?.SelectedItems.Count == 0)
                {
                    MessageBox.Show("请选择要踢出的客户端", "提示");
                    return;
                }

                var connectionInfo = clientListView.SelectedItems[0].Tag as Models.ClientConnectionInfo;
                if (connectionInfo == null) return;

                if (_serverListView.SelectedItems.Count == 0) return;
                var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                if (string.IsNullOrEmpty(serverId)) return;

                var result = MessageBox.Show($"确定要踢出客户端 {connectionInfo.ClientIp}:{connectionInfo.ClientPort} 吗？",
                    "确认踢出", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    var success = await _serverManager.KickClientAsync(serverId, connectionInfo.ConnectionId);
                    if (success)
                    {
                        AppendLog($"已踢出客户端: {connectionInfo.ClientIp}:{connectionInfo.ClientPort}");
                        LoadClientList();
                    }
                    else
                    {
                        AppendLog($"踢出客户端失败: {connectionInfo.ClientIp}:{connectionInfo.ClientPort}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"踢出客户端失败: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            LoadClientList();
            AppendLog("刷新客户端连接列表");
        }

        private Form CreateServerConfigDialog(Models.TcpServerConfig? config = null)
        {
            var form = new Form
            {
                Text = config == null ? "添加TCP服务器" : "编辑TCP服务器",
                Size = new Size(400, 350),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            };

            var nameLabel = new Label { Text = "服务器名称:", Location = new Point(20, 20), Size = new Size(80, 20) };
            var nameBox = new TextBox { Location = new Point(110, 18), Size = new Size(250, 20), Text = config?.Name ?? "TCP服务器" };

            var ipLabel = new Label { Text = "IP地址:", Location = new Point(20, 50), Size = new Size(80, 20) };
            var ipBox = new TextBox { Location = new Point(110, 48), Size = new Size(250, 20), Text = config?.IpAddress ?? "127.0.0.1" };

            var portLabel = new Label { Text = "端口:", Location = new Point(20, 80), Size = new Size(80, 20) };
            var portBox = new NumericUpDown { Location = new Point(110, 78), Size = new Size(250, 20), Minimum = 1, Maximum = 65535, Value = config?.Port ?? 8080 };

            var autoStartBox = new CheckBox { Text = "自动启动", Location = new Point(110, 110), Size = new Size(100, 20), Checked = config?.AutoStart ?? false };

            var descLabel = new Label { Text = "描述:", Location = new Point(20, 140), Size = new Size(80, 20) };
            var descBox = new TextBox { Location = new Point(110, 138), Size = new Size(250, 60), Multiline = true, Text = config?.Description ?? "" };

            var okButton = new Button { Text = "确定", Location = new Point(200, 220), Size = new Size(75, 25), DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "取消", Location = new Point(285, 220), Size = new Size(75, 25), DialogResult = DialogResult.Cancel };

            okButton.Click += (s, e) =>
            {
                if (config == null)
                {
                    config = new Models.TcpServerConfig();
                    _configManager.AddConfig(config);
                }

                config.Name = nameBox.Text;
                config.IpAddress = ipBox.Text;
                config.Port = (int)portBox.Value;
                config.AutoStart = autoStartBox.Checked;
                config.Description = descBox.Text;
            };

            form.Controls.AddRange(new Control[] { nameLabel, nameBox, ipLabel, ipBox, portLabel, portBox, autoStartBox, descLabel, descBox, okButton, cancelButton });
            return form;
        }

        private void StartButton_Click(object? sender, EventArgs e)
        {
            StartSelectedServers();
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            StopSelectedServers();
        }

        private void AppendLog(string message, string? serverId = null)
        {
            try
            {
                // 检查控件是否已释放
                if (IsDisposed || _logTextBox == null || _logTextBox.IsDisposed)
                {
                    return;
                }

                if (InvokeRequired)
                {
                    // 使用BeginInvoke避免阻塞，并检查控件状态
                    BeginInvoke(new Action<string, string?>(AppendLog), message, serverId);
                    return;
                }

                // 再次检查控件状态
                if (IsDisposed || _logTextBox == null || _logTextBox.IsDisposed)
                {
                    return;
                }

                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

                // 添加到对应服务器的日志列表
                var targetServerId = serverId ?? "系统";
                var logKey = GetLogKey(targetServerId);

                if (!_serverLogs.ContainsKey(logKey))
                {
                    _serverLogs[logKey] = new List<string>();

                    // 更新日志过滤下拉框
                    UpdateLogFilterComboBox(targetServerId);
                }

                _serverLogs[logKey].Add(logMessage);

                // 如果当前显示的是全部日志或者是当前服务器的日志，则显示
                var currentLogKey = GetLogKeyFromDisplayText(_currentLogServerId);
                if (_currentLogServerId == "全部" || currentLogKey == logKey)
                {
                    var displayMessage = _currentLogServerId == "全部" ? $"[{GetLogDisplayText(targetServerId)}] {logMessage}" : logMessage;

                    // 最后一次检查控件状态
                    if (!IsDisposed && _logTextBox != null && !_logTextBox.IsDisposed)
                    {
                        _logTextBox.AppendText(displayMessage + Environment.NewLine);
                        _logTextBox.SelectionStart = _logTextBox.Text.Length;
                        _logTextBox.ScrollToCaret();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // 控件已释放，静默忽略
                return;
            }
            catch (InvalidOperationException)
            {
                // 控件操作无效，静默忽略
                return;
            }
            catch (Exception ex)
            {
                // 其他异常记录到系统日志
                System.Diagnostics.Debug.WriteLine($"AppendLog失败: {ex.Message}");
            }
        }

        private void UpdateLogFilterComboBox(string targetServerId)
        {
            try
            {
                if (IsDisposed) return;

                var logFilterComboBox = Controls.Find("logFilterComboBox", true).FirstOrDefault() as ComboBox;
                if (logFilterComboBox != null && !logFilterComboBox.IsDisposed)
                {
                    var displayText = GetLogDisplayText(targetServerId);
                    if (!logFilterComboBox.Items.Cast<object>().Any(item => item.ToString() == displayText))
                    {
                        logFilterComboBox.Items.Add(displayText);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // 控件已释放，忽略
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新日志过滤下拉框失败: {ex.Message}");
            }
        }

        private string GetLogKey(string serverId)
        {
            if (serverId == "系统") return "系统";

            var config = _configManager.GetConfig(serverId);
            if (config != null)
            {
                return $"{config.IpAddress}:{config.Port}";
            }
            return serverId;
        }

        private string GetLogDisplayText(string serverId)
        {
            if (serverId == "系统") return "系统";

            var config = _configManager.GetConfig(serverId);
            if (config != null)
            {
                return $"{config.Name} ({config.IpAddress}:{config.Port})";
            }
            return serverId;
        }

        private string GetLogKeyFromDisplayText(string displayText)
        {
            if (displayText == "全部" || displayText == "系统") return displayText;

            // 从显示文本中提取IP:Port
            var match = System.Text.RegularExpressions.Regex.Match(displayText, @"\(([^)]+)\)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return displayText;
        }

        private void RefreshLogFilterComboBox()
        {
            try
            {
                if (IsDisposed) return;

                var logFilterComboBox = Controls.Find("logFilterComboBox", true).FirstOrDefault() as ComboBox;
                if (logFilterComboBox == null || logFilterComboBox.IsDisposed) return;

                // 保存当前选中项
                var currentSelection = logFilterComboBox.SelectedItem?.ToString();

                // 清空并重新添加项目
                logFilterComboBox.Items.Clear();

                // 添加所有服务器的日志选项
                var configs = _configManager.GetAllConfigs();
                foreach (var config in configs)
                {
                    var displayText = GetLogDisplayText(config.Id);
                    if (!logFilterComboBox.Items.Cast<object>().Any(item => item.ToString() == displayText))
                    {
                        logFilterComboBox.Items.Add(displayText);
                    }
                }

                // 恢复选中项
                if (!string.IsNullOrEmpty(currentSelection))
                {
                    for (int i = 0; i < logFilterComboBox.Items.Count; i++)
                    {
                        if (logFilterComboBox.Items[i].ToString() == currentSelection)
                        {
                            logFilterComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新日志过滤下拉框失败: {ex.Message}");
            }
        }

        private void OnServerStatusChanged(string serverId, bool isRunning)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string, bool>(OnServerStatusChanged), serverId, isRunning);
                    return;
                }

                LoadServerList();
                var config = _configManager.GetConfig(serverId);
                var status = isRunning ? "已启动" : "已停止";
                AppendLog($"服务器状态变化: {config?.Name} - {status}", config?.Name ?? serverId);
            }
            catch (Exception ex)
            {
                AppendLog($"处理服务器状态变化失败: {ex.Message}");
            }
        }

        private void OnClientConnectionChanged(string serverId, Models.ClientConnectionInfo connectionInfo, bool connected)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string, Models.ClientConnectionInfo, bool>(OnClientConnectionChanged),
                        serverId, connectionInfo, connected);
                    return;
                }

                LoadClientList();
                var action = connected ? "连接" : "断开";
                var config = _configManager.GetConfig(serverId);
                AppendLog($"客户端{action}: {connectionInfo.ClientIp}:{connectionInfo.ClientPort} (ID: {connectionInfo.ConnectionId})", config?.Name ?? serverId);
            }
            catch (Exception ex)
            {
                AppendLog($"处理客户端连接变化失败: {ex.Message}");
            }
        }

        private void SimpleTcpPanel_HandleDestroyed(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("TCP服务器管理面板Handle被销毁事件触发");
                // HandleDestroyed事件通常在Dispose之后触发，此时资源应该已经被清理
                // 这里只做日志记录，实际清理工作在Dispose方法中完成
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

                    // 先停止定时器
                    _refreshTimer?.Stop();
                    _refreshTimer?.Dispose();

                    // 直接调用TCP服务器停止逻辑（不依赖事件）
                    StopAllTcpServersSync();

                    // 通知插件类清理静态引用
                    TcpServerPlugin.OnPanelDisposed(this);

                    // 取消事件注册
                    this.HandleDestroyed -= SimpleTcpPanel_HandleDestroyed;

                    System.Diagnostics.Debug.WriteLine("TCP服务器管理面板Dispose完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Dispose处理失败: {ex.Message}");
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 同步停止所有TCP服务器（用于Dispose时调用）
        /// </summary>
        private void StopAllTcpServersSync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始同步停止所有TCP服务器...");

                // 使用同步方式停止所有服务器，但设置较短的超时时间
                var stopTask = _serverManager.StopAllServersAsync();
                if (stopTask.Wait(TimeSpan.FromSeconds(3)))
                {
                    System.Diagnostics.Debug.WriteLine("所有TCP服务器已成功停止");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("停止TCP服务器超时，但继续释放资源");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"同步停止TCP服务器失败: {ex.Message}");
            }
        }
    }
}
