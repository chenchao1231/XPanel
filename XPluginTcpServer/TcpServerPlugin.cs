using System;
using System.Drawing;
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
        private ListView _serverListView = null!;
        private TextBox _logTextBox = null!;
        private Timer _refreshTimer = null!;

        public SimpleTcpPanel()
        {
            _configManager = new ConfigManager();
            InitializeComponent();
            LoadServerList();

            // 注册日志输出
            Log.RegisterOutput(new UILogOutput(AppendLog));
            AppendLog("TCP服务器管理面板已启动");
        }

        private void InitializeComponent()
        {
            Size = new Size(1000, 600);
            BackColor = Color.White;

            // 标题
            var titleLabel = new Label
            {
                Text = "TCP服务器管理面板",
                Font = new Font("Microsoft YaHei", 16F, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(300, 30),
                ForeColor = Color.DarkBlue
            };

            // 服务器列表
            var serverGroup = new GroupBox
            {
                Text = "服务器配置",
                Location = new Point(20, 60),
                Size = new Size(960, 200),
                Font = new Font("Microsoft YaHei", 10F)
            };

            _serverListView = new ListView
            {
                Location = new Point(10, 25),
                Size = new Size(940, 130),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            _serverListView.Columns.Add("名称", 150);
            _serverListView.Columns.Add("地址", 120);
            _serverListView.Columns.Add("端口", 80);
            _serverListView.Columns.Add("状态", 100);
            _serverListView.Columns.Add("自动启动", 100);
            _serverListView.Columns.Add("描述", 300);

            // 按钮面板
            var buttonPanel = new Panel
            {
                Location = new Point(10, 160),
                Size = new Size(940, 30)
            };

            var addButton = new Button
            {
                Text = "添加服务器",
                Location = new Point(0, 0),
                Size = new Size(100, 25),
                BackColor = Color.LightGreen
            };
            addButton.Click += AddButton_Click;

            var startButton = new Button
            {
                Text = "启动服务器",
                Location = new Point(110, 0),
                Size = new Size(100, 25),
                BackColor = Color.LightBlue
            };
            startButton.Click += StartButton_Click;

            var stopButton = new Button
            {
                Text = "停止服务器",
                Location = new Point(220, 0),
                Size = new Size(100, 25),
                BackColor = Color.LightCoral
            };
            stopButton.Click += StopButton_Click;

            buttonPanel.Controls.AddRange(new Control[] { addButton, startButton, stopButton });
            serverGroup.Controls.AddRange(new Control[] { _serverListView, buttonPanel });

            // 日志区域
            var logGroup = new GroupBox
            {
                Text = "操作日志",
                Location = new Point(20, 280),
                Size = new Size(960, 300),
                Font = new Font("Microsoft YaHei", 10F)
            };

            _logTextBox = new TextBox
            {
                Location = new Point(10, 25),
                Size = new Size(940, 265),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9F)
            };

            logGroup.Controls.Add(_logTextBox);

            // 添加到面板
            Controls.AddRange(new Control[] { titleLabel, serverGroup, logGroup });

            // 定时器
            _refreshTimer = new Timer { Interval = 3000, Enabled = true };
            _refreshTimer.Tick += (s, e) => LoadServerList();
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

                    if (config.IsRunning)
                    {
                        item.BackColor = Color.LightGreen;
                    }

                    _serverListView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"加载服务器列表失败: {ex.Message}");
            }
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

        private void StartButton_Click(object? sender, EventArgs e)
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

                // 模拟启动服务器
                _configManager.UpdateServerStatus(serverId, true, 0);
                LoadServerList();
                AppendLog($"启动TCP服务器: {config.Name} ({config.IpAddress}:{config.Port})");
                AppendLog("注意: 这是演示版本，实际TCP服务功能需要完整实现");
            }
            catch (Exception ex)
            {
                AppendLog($"启动服务器失败: {ex.Message}");
            }
        }

        private void StopButton_Click(object? sender, EventArgs e)
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

                // 模拟停止服务器
                _configManager.UpdateServerStatus(serverId, false, 0);
                LoadServerList();
                AppendLog($"停止TCP服务器: {config.Name}");
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Dispose();
                AppendLog("TCP服务器管理面板已关闭");
            }
            base.Dispose(disposing);
        }
    }
}
