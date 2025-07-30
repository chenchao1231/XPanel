using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XPluginTcpRelay.Models;
using XPluginTcpRelay.Services;

namespace XPluginTcpRelay.UI
{
    /// <summary>
    /// TCP中继控制面板
    /// </summary>
    public partial class RelayControlPanel : UserControl
    {
        private readonly ConfigManager _configManager;
        private readonly TcpRelayManager _relayManager;
        private readonly DataAuditor _dataAuditor;
        private Timer? _refreshTimer;

        // UI控件
        private Button _startButton;
        private Button _stopButton;
        private Button _addRuleButton;
        private Button _editRuleButton;
        private Button _deleteRuleButton;
        private ListView _routeRulesListView;
        private ListView _connectionsListView;
        private TextBox _logTextBox;
        private Label _statusLabel;
        private Label _statisticsLabel;

        public RelayControlPanel()
        {
            _configManager = new ConfigManager();
            _relayManager = new TcpRelayManager(_configManager);
            _dataAuditor = new DataAuditor();

            InitializeComponent();
            SetupEventHandlers();
            LoadRouteRules();
            StartRefreshTimer();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // 设置面板属性
            Size = new Size(1000, 700);
            BackColor = Color.White;

            // 创建标题
            var titleLabel = new Label
            {
                Text = "TCP数据转发系统中继平台（B方）",
                Font = new Font("Microsoft YaHei", 16F, FontStyle.Bold),
                Location = new Point(20, 10),
                Size = new Size(400, 30),
                ForeColor = Color.DarkBlue
            };

            // 状态标签
            _statusLabel = new Label
            {
                Text = "状态: 已停止",
                Location = new Point(20, 50),
                Size = new Size(200, 20),
                Font = new Font("Microsoft YaHei", 10F),
                ForeColor = Color.Red
            };

            // 统计标签
            _statisticsLabel = new Label
            {
                Text = "连接数: 0 | 转发量: 0 字节",
                Location = new Point(250, 50),
                Size = new Size(300, 20),
                Font = new Font("Microsoft YaHei", 10F)
            };

            // 控制按钮
            _startButton = new Button
            {
                Text = "启动服务",
                Location = new Point(20, 80),
                Size = new Size(80, 30),
                BackColor = Color.LightGreen
            };

            _stopButton = new Button
            {
                Text = "停止服务",
                Location = new Point(110, 80),
                Size = new Size(80, 30),
                BackColor = Color.LightCoral,
                Enabled = false
            };

            // 路由规则管理按钮
            _addRuleButton = new Button
            {
                Text = "添加规则",
                Location = new Point(220, 80),
                Size = new Size(80, 30)
            };

            _editRuleButton = new Button
            {
                Text = "编辑规则",
                Location = new Point(310, 80),
                Size = new Size(80, 30),
                Enabled = false
            };

            _deleteRuleButton = new Button
            {
                Text = "删除规则",
                Location = new Point(400, 80),
                Size = new Size(80, 30),
                Enabled = false
            };

            // 路由规则列表
            var routeRulesLabel = new Label
            {
                Text = "路由规则配置:",
                Location = new Point(20, 120),
                Size = new Size(100, 20),
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold)
            };

            _routeRulesListView = new ListView
            {
                Location = new Point(20, 145),
                Size = new Size(960, 150),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            _routeRulesListView.Columns.Add("规则名称", 120);
            _routeRulesListView.Columns.Add("A方端点", 120);
            _routeRulesListView.Columns.Add("C方端点", 120);
            _routeRulesListView.Columns.Add("状态", 60);
            _routeRulesListView.Columns.Add("转发包数", 80);
            _routeRulesListView.Columns.Add("转发字节", 100);
            _routeRulesListView.Columns.Add("描述", 200);
            _routeRulesListView.Columns.Add("创建时间", 140);

            // 连接状态列表
            var connectionsLabel = new Label
            {
                Text = "活动连接:",
                Location = new Point(20, 305),
                Size = new Size(100, 20),
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold)
            };

            _connectionsListView = new ListView
            {
                Location = new Point(20, 330),
                Size = new Size(960, 120),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            _connectionsListView.Columns.Add("连接ID", 100);
            _connectionsListView.Columns.Add("类型", 60);
            _connectionsListView.Columns.Add("远程端点", 120);
            _connectionsListView.Columns.Add("状态", 60);
            _connectionsListView.Columns.Add("连接时间", 80);
            _connectionsListView.Columns.Add("接收字节", 80);
            _connectionsListView.Columns.Add("发送字节", 80);
            _connectionsListView.Columns.Add("最后活动", 120);

            // 实时日志
            var logLabel = new Label
            {
                Text = "实时日志:",
                Location = new Point(20, 460),
                Size = new Size(100, 20),
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold)
            };

            _logTextBox = new TextBox
            {
                Location = new Point(20, 485),
                Size = new Size(960, 200),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LimeGreen,
                Font = new Font("Consolas", 9F)
            };

            // 添加控件到面板
            Controls.AddRange(new Control[]
            {
                titleLabel, _statusLabel, _statisticsLabel,
                _startButton, _stopButton,
                _addRuleButton, _editRuleButton, _deleteRuleButton,
                routeRulesLabel, _routeRulesListView,
                connectionsLabel, _connectionsListView,
                logLabel, _logTextBox
            });

            ResumeLayout(false);
        }

        private void SetupEventHandlers()
        {
            _startButton.Click += StartButton_Click;
            _stopButton.Click += StopButton_Click;
            _addRuleButton.Click += AddRuleButton_Click;
            _editRuleButton.Click += EditRuleButton_Click;
            _deleteRuleButton.Click += DeleteRuleButton_Click;
            _routeRulesListView.SelectedIndexChanged += RouteRulesListView_SelectedIndexChanged;

            // 订阅中继管理器事件
            _relayManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            _relayManager.DataForwarded += OnDataForwarded;
            _relayManager.LogMessage += OnLogMessage;

            // 订阅数据审计事件
            _dataAuditor.NewAuditLog += OnNewAuditLog;
        }

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            try
            {
                _startButton.Enabled = false;
                var success = await _relayManager.StartAsync();
                
                if (success)
                {
                    _statusLabel.Text = "状态: 运行中";
                    _statusLabel.ForeColor = Color.Green;
                    _stopButton.Enabled = true;
                    AppendLog("TCP中继服务已启动");
                }
                else
                {
                    _startButton.Enabled = true;
                    AppendLog("启动TCP中继服务失败");
                }
            }
            catch (Exception ex)
            {
                _startButton.Enabled = true;
                AppendLog($"启动服务时发生错误: {ex.Message}");
            }
        }

        private async void StopButton_Click(object? sender, EventArgs e)
        {
            try
            {
                _stopButton.Enabled = false;
                await _relayManager.StopAsync();
                
                _statusLabel.Text = "状态: 已停止";
                _statusLabel.ForeColor = Color.Red;
                _startButton.Enabled = true;
                
                // 清空连接列表
                _connectionsListView.Items.Clear();
                AppendLog("TCP中继服务已停止");
            }
            catch (Exception ex)
            {
                _stopButton.Enabled = true;
                AppendLog($"停止服务时发生错误: {ex.Message}");
            }
        }

        private void AddRuleButton_Click(object? sender, EventArgs e)
        {
            var dialog = new RouteRuleDialog();
            if (dialog.ShowDialog() == DialogResult.OK && dialog.RouteRule != null)
            {
                if (_configManager.AddRouteRule(dialog.RouteRule))
                {
                    LoadRouteRules();
                    AppendLog($"已添加路由规则: {dialog.RouteRule.Name}");
                }
                else
                {
                    MessageBox.Show("添加路由规则失败，可能已存在相同的A方端点", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EditRuleButton_Click(object? sender, EventArgs e)
        {
            if (_routeRulesListView.SelectedItems.Count == 0) return;

            var selectedItem = _routeRulesListView.SelectedItems[0];
            var ruleId = selectedItem.Tag?.ToString();
            if (string.IsNullOrEmpty(ruleId)) return;

            var rule = _configManager.GetRouteRule(ruleId);
            if (rule == null) return;

            var dialog = new RouteRuleDialog(rule);
            if (dialog.ShowDialog() == DialogResult.OK && dialog.RouteRule != null)
            {
                if (_configManager.UpdateRouteRule(dialog.RouteRule))
                {
                    LoadRouteRules();
                    AppendLog($"已更新路由规则: {dialog.RouteRule.Name}");
                }
                else
                {
                    MessageBox.Show("更新路由规则失败", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteRuleButton_Click(object? sender, EventArgs e)
        {
            if (_routeRulesListView.SelectedItems.Count == 0) return;

            var selectedItem = _routeRulesListView.SelectedItems[0];
            var ruleId = selectedItem.Tag?.ToString();
            var ruleName = selectedItem.Text;

            if (string.IsNullOrEmpty(ruleId)) return;

            var result = MessageBox.Show($"确定要删除路由规则 '{ruleName}' 吗？", "确认删除", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (_configManager.RemoveRouteRule(ruleId))
                {
                    LoadRouteRules();
                    AppendLog($"已删除路由规则: {ruleName}");
                }
                else
                {
                    MessageBox.Show("删除路由规则失败", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RouteRulesListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var hasSelection = _routeRulesListView.SelectedItems.Count > 0;
            _editRuleButton.Enabled = hasSelection;
            _deleteRuleButton.Enabled = hasSelection;
        }

        private void LoadRouteRules()
        {
            _routeRulesListView.Items.Clear();

            foreach (var rule in _configManager.Config.RouteRules)
            {
                var item = new ListViewItem(rule.Name);
                item.SubItems.Add(rule.AEndpoint);
                item.SubItems.Add(rule.CEndpoint);
                item.SubItems.Add(rule.IsEnabled ? "启用" : "禁用");
                item.SubItems.Add(rule.ForwardedPackets.ToString());
                item.SubItems.Add(rule.ForwardedBytes.ToString());
                item.SubItems.Add(rule.Description);
                item.SubItems.Add(rule.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                item.Tag = rule.Id;
                item.BackColor = rule.IsEnabled ? Color.White : Color.LightGray;

                _routeRulesListView.Items.Add(item);
            }
        }

        private void OnConnectionStatusChanged(ConnectionInfo connection)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ConnectionInfo>(OnConnectionStatusChanged), connection);
                return;
            }

            UpdateConnectionsList();
        }

        private void OnDataForwarded(string direction, byte[] data, string connectionId)
        {
            _dataAuditor.LogDataForward(direction, data, connectionId);
        }

        private void OnLogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(OnLogMessage), message);
                return;
            }

            AppendLog(message);
        }

        private void OnNewAuditLog(AuditLogEntry logEntry)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<AuditLogEntry>(OnNewAuditLog), logEntry);
                return;
            }

            AppendLog(logEntry.GetDisplayText());
        }

        private void UpdateConnectionsList()
        {
            _connectionsListView.Items.Clear();

            foreach (var connection in _relayManager.GetAllConnections())
            {
                var item = new ListViewItem(connection.Id[..8] + "...");
                item.SubItems.Add(connection.Type.ToString());
                item.SubItems.Add(connection.RemoteEndPoint?.ToString() ?? "");
                item.SubItems.Add(connection.Status.ToString());
                item.SubItems.Add(connection.Duration.ToString(@"hh\:mm\:ss"));
                item.SubItems.Add(connection.ReceivedBytes.ToString());
                item.SubItems.Add(connection.SentBytes.ToString());
                item.SubItems.Add(connection.LastActivityTime.ToString("HH:mm:ss"));

                // 根据连接状态设置颜色
                switch (connection.Status)
                {
                    case ConnectionStatus.Connected:
                        item.BackColor = Color.LightGreen;
                        break;
                    case ConnectionStatus.Disconnected:
                        item.BackColor = Color.LightGray;
                        break;
                    case ConnectionStatus.Error:
                        item.BackColor = Color.LightPink;
                        break;
                }

                _connectionsListView.Items.Add(item);
            }

            // 更新统计信息
            var connections = _relayManager.GetAllConnections().ToList();
            var totalBytes = connections.Sum(c => c.ReceivedBytes + c.SentBytes);
            _statisticsLabel.Text = $"连接数: {connections.Count} | 转发量: {totalBytes:N0} 字节";
        }

        private void AppendLog(string message)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new Action<string>(AppendLog), message);
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logLine = $"[{timestamp}] {message}";
            
            _logTextBox.AppendText(logLine + Environment.NewLine);
            _logTextBox.SelectionStart = _logTextBox.Text.Length;
            _logTextBox.ScrollToCaret();

            // 限制日志长度，避免内存占用过多
            if (_logTextBox.Lines.Length > 1000)
            {
                var lines = _logTextBox.Lines.Skip(500).ToArray();
                _logTextBox.Lines = lines;
            }
        }

        private void StartRefreshTimer()
        {
            _refreshTimer = new Timer();
            _refreshTimer.Interval = _configManager.Config.StatisticsRefreshInterval * 1000;
            _refreshTimer.Tick += (s, e) => UpdateConnectionsList();
            _refreshTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                _relayManager?.Dispose();
                _dataAuditor?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
