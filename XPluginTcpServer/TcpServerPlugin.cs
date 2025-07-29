using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XPlugin;
using XPluginTcpServer.Services;
using XPlugin.logs;

namespace XPluginTcpServer
{
    /// <summary>
    /// TCP服务器插件 - 简化实用版本
    /// </summary>
    public class TcpServerPlugin : IXPanelInterface
    {
        public string Name => "TCP服务器管理";

        public UserControl CreatePanel()
        {
            try
            {
                return new SimpleTcpPanel();
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
            Log.RegisterOutput(new UILogOutput(AppendLog));
            AppendLog("TCP服务器管理面板已启动");

            // 启动自动启动的服务器
            _ = Task.Run(async () => await _serverManager.StartAutoStartServersAsync());
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
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _serverListView.Columns.Add("名称", 120);
            _serverListView.Columns.Add("地址", 100);
            _serverListView.Columns.Add("端口", 60);
            _serverListView.Columns.Add("状态", 80);
            _serverListView.Columns.Add("连接数", 80);

            // 按钮面板
            var buttonPanel = new Panel
            {
                Location = new Point(10, 230),
                Size = new Size(445, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var addButton = new Button
            {
                Text = "添加",
                Location = new Point(0, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };
            addButton.Click += AddButton_Click;

            var editButton = new Button
            {
                Text = "编辑",
                Location = new Point(80, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            editButton.Click += EditButton_Click;

            var deleteButton = new Button
            {
                Text = "删除",
                Location = new Point(160, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };
            deleteButton.Click += DeleteButton_Click;

            var startButton = new Button
            {
                Text = "启动",
                Location = new Point(240, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };
            startButton.Click += StartButton_Click;

            var stopButton = new Button
            {
                Text = "停止",
                Location = new Point(320, 5),
                Size = new Size(70, 30),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };
            stopButton.Click += StopButton_Click;

            buttonPanel.Controls.AddRange(new Control[] { addButton, editButton, deleteButton, startButton, stopButton });
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

            var kickButton = new Button
            {
                Text = "踢出客户端",
                Location = new Point(0, 5),
                Size = new Size(100, 30),
                BackColor = Color.Orange,
                UseVisualStyleBackColor = false
            };
            kickButton.Click += KickButton_Click;

            var refreshButton = new Button
            {
                Text = "刷新连接",
                Location = new Point(110, 5),
                Size = new Size(100, 30),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            refreshButton.Click += RefreshButton_Click;

            clientButtonPanel.Controls.AddRange(new Control[] { kickButton, refreshButton });
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

            _logTextBox = new TextBox
            {
                Location = new Point(10, 25),
                Size = new Size(655, 690),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            logGroup.Controls.Add(_logTextBox);
            rightPanel.Controls.Add(logGroup);

            // 添加到主面板
            Controls.AddRange(new Control[] { titleLabel, leftPanel, rightPanel });

            // 定时器
            _refreshTimer = new Timer { Interval = 2000, Enabled = true };
            _refreshTimer.Tick += RefreshTimer_Tick;
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
                    item.SubItems.Add(config.CurrentConnections.ToString());
                    item.Tag = config.Id;

                    if (config.IsRunning)
                    {
                        item.BackColor = Color.LightGreen;
                        item.ForeColor = Color.DarkGreen;
                    }
                    else
                    {
                        item.BackColor = Color.LightGray;
                        item.ForeColor = Color.Black;
                    }

                    _serverListView.Items.Add(item);
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

                clientListView.Items.Clear();

                if (_serverListView.SelectedItems.Count > 0)
                {
                    var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                    if (!string.IsNullOrEmpty(serverId))
                    {
                        var connections = GetServerConnections(serverId);
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
                                item.BackColor = Color.LightGray;
                                item.ForeColor = Color.Black;
                            }

                            clientListView.Items.Add(item);
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

                var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                if (string.IsNullOrEmpty(serverId)) return;

                var config = _configManager.GetConfig(serverId);
                if (config == null) return;

                // 创建编辑对话框（复用添加对话框的逻辑）
                using var form = CreateServerConfigDialog(config);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _configManager.UpdateConfig(config);
                    LoadServerList();
                    AppendLog($"更新服务器配置: {config.Name}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"编辑服务器失败: {ex.Message}");
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_serverListView.SelectedItems.Count == 0)
                {
                    MessageBox.Show("请选择要删除的服务器", "提示");
                    return;
                }

                var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                if (string.IsNullOrEmpty(serverId)) return;

                var config = _configManager.GetConfig(serverId);
                if (config == null) return;

                if (config.IsRunning)
                {
                    MessageBox.Show("请先停止服务器再删除", "提示");
                    return;
                }

                var result = MessageBox.Show($"确定要删除服务器 '{config.Name}' 吗？", "确认删除",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    _configManager.DeleteConfig(serverId);
                    LoadServerList();
                    AppendLog($"删除服务器: {config.Name}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"删除服务器失败: {ex.Message}");
            }
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

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_serverListView.SelectedItems.Count == 0)
                {
                    MessageBox.Show("请选择要启动的服务器", "提示");
                    return;
                }

                var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                if (string.IsNullOrEmpty(serverId)) return;

                var config = _configManager.GetConfig(serverId);
                if (config == null) return;

                AppendLog($"正在启动TCP服务器: {config.Name} ({config.IpAddress}:{config.Port})");

                var success = await _serverManager.StartServerAsync(serverId);
                if (success)
                {
                    LoadServerList();
                    AppendLog($"TCP服务器启动成功: {config.Name}");
                }
                else
                {
                    AppendLog($"TCP服务器启动失败: {config.Name}");
                    MessageBox.Show("服务器启动失败，请查看日志", "错误");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"启动服务器失败: {ex.Message}");
            }
        }

        private async void StopButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_serverListView.SelectedItems.Count == 0)
                {
                    MessageBox.Show("请选择要停止的服务器", "提示");
                    return;
                }

                var serverId = _serverListView.SelectedItems[0].Tag?.ToString();
                if (string.IsNullOrEmpty(serverId)) return;

                var config = _configManager.GetConfig(serverId);
                if (config == null) return;

                AppendLog($"正在停止TCP服务器: {config.Name}");

                var success = await _serverManager.StopServerAsync(serverId);
                if (success)
                {
                    LoadServerList();
                    LoadClientList();
                    AppendLog($"TCP服务器停止成功: {config.Name}");
                }
                else
                {
                    AppendLog($"TCP服务器停止失败: {config.Name}");
                    MessageBox.Show("服务器停止失败，请查看日志", "错误");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"停止服务器失败: {ex.Message}");
            }
        }

        private void AppendLog(string message)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string>(AppendLog), message);
                    return;
                }

                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                _logTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
                _logTextBox.SelectionStart = _logTextBox.Text.Length;
                _logTextBox.ScrollToCaret();
            }
            catch
            {
                // 忽略日志错误
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
                AppendLog($"服务器状态变化: {config?.Name} - {status}");
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
                AppendLog($"客户端{action}: {connectionInfo.ClientIp}:{connectionInfo.ClientPort} (ID: {connectionInfo.ConnectionId})");
            }
            catch (Exception ex)
            {
                AppendLog($"处理客户端连接变化失败: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _refreshTimer?.Dispose();

                    // 停止所有服务器
                    AppendLog("正在停止所有TCP服务器...");
                    _ = Task.Run(async () => await _serverManager.StopAllServersAsync());

                    AppendLog("TCP服务器管理面板已关闭");
                }
                catch (Exception ex)
                {
                    AppendLog($"关闭面板时发生错误: {ex.Message}");
                }
            }
            base.Dispose(disposing);
        }
    }
}
