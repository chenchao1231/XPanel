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
        private readonly TcpDataRelayManager _relayManager;
        private readonly DataAuditor _dataAuditor;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private string? _selectedRuleId;
        private ContextMenuStrip _routeRulesContextMenu;
        private const int MaxLogSizeBytes = 10 * 1024 * 1024; // 10MB

        // UI控件
        private Button _startButton;
        private Button _stopButton;
        private Button _addRuleButton;
        private Button _editRuleButton;
        private Button _deleteRuleButton;
        private Button _clearLogButton;
        private ListView _routeRulesListView;
        private ListView _connectionsListView;
        private TextBox _logTextBox;
        private Label _statusLabel;
        private Label _statisticsLabel;

        public RelayControlPanel()
        {
            _configManager = new ConfigManager();
            _relayManager = new TcpDataRelayManager();
            _dataAuditor = new DataAuditor();

            InitializeComponent();

            // 初始化定时器
            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 2000; // 2秒刷新一次
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();

            // 初始化右键菜单
            InitializeContextMenu();

            SetupEventHandlers();
            LoadRouteRules();
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
                Text = "一键启动所有规则",
                Location = new Point(20, 80),
                Size = new Size(120, 30),
                BackColor = Color.LightGreen
            };

            _stopButton = new Button
            {
                Text = "一键停止所有规则",
                Location = new Point(150, 80),
                Size = new Size(120, 30),
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
            _routeRulesListView.Columns.Add("数据源", 120);
            _routeRulesListView.Columns.Add("本地监听", 120);
            _routeRulesListView.Columns.Add("规则状态", 80);
            _routeRulesListView.Columns.Add("运行状态", 80);
            _routeRulesListView.Columns.Add("转发包数", 80);
            _routeRulesListView.Columns.Add("转发字节", 100);
            _routeRulesListView.Columns.Add("创建时间", 140);
            _routeRulesListView.Columns.Add("描述", 180);

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

            _clearLogButton = new Button
            {
                Text = "清空日志",
                Location = new Point(900, 458),
                Size = new Size(80, 25),
                BackColor = Color.LightBlue
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
                logLabel, _clearLogButton, _logTextBox
            });

            ResumeLayout(false);
        }

        /// <summary>
        /// 初始化右键菜单
        /// </summary>
        private void InitializeContextMenu()
        {
            _routeRulesContextMenu = new ContextMenuStrip();

            var startMenuItem = new ToolStripMenuItem("启动服务");
            startMenuItem.Click += (s, e) => StartSelectedRule();

            var stopMenuItem = new ToolStripMenuItem("停止服务");
            stopMenuItem.Click += (s, e) => StopSelectedRule();

            var editMenuItem = new ToolStripMenuItem("编辑规则");
            editMenuItem.Click += (s, e) => EditRuleButton_Click(s, e);

            var deleteMenuItem = new ToolStripMenuItem("删除规则");
            deleteMenuItem.Click += (s, e) => DeleteRuleButton_Click(s, e);

            _routeRulesContextMenu.Items.AddRange(new ToolStripItem[]
            {
                startMenuItem,
                stopMenuItem,
                new ToolStripSeparator(),
                editMenuItem,
                deleteMenuItem
            });

            _routeRulesListView.ContextMenuStrip = _routeRulesContextMenu;
            _routeRulesContextMenu.Opening += RouteRulesContextMenu_Opening;
        }

        private void SetupEventHandlers()
        {
            _startButton.Click += StartButton_Click;
            _stopButton.Click += StopButton_Click;
            _addRuleButton.Click += AddRuleButton_Click;
            _editRuleButton.Click += EditRuleButton_Click;
            _deleteRuleButton.Click += DeleteRuleButton_Click;
            _clearLogButton.Click += ClearLogButton_Click;
            _routeRulesListView.SelectedIndexChanged += RouteRulesListView_SelectedIndexChanged;

            // 订阅中继管理器事件
            _relayManager.ConnectionChanged += OnConnectionChanged;
            _relayManager.DataTransferred += OnDataTransferred;
            _relayManager.LogMessage += OnLogMessage;

            // 订阅数据审计事件
            _dataAuditor.NewAuditLog += OnNewAuditLog;
        }

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            try
            {
                _startButton.Enabled = false;

                // 启动所有启用的路由规则
                var enabledRules = _configManager.Config.RouteRules.Where(r => r.IsEnabled).ToList();
                var successCount = 0;

                foreach (var rule in enabledRules)
                {
                    var success = await _relayManager.StartAsync(rule);
                    if (success)
                    {
                        successCount++;
                        AppendLog($"已启动路由规则: {rule.Name}");
                    }
                    else
                    {
                        AppendLog($"启动路由规则失败: {rule.Name}");
                    }
                }

                // 更新UI状态
                UpdateServiceStatus();
                LoadRouteRules(); // 刷新规则列表以显示运行状态

                if (successCount > 0)
                {
                    AppendLog($"TCP中继服务启动完成，成功启动 {successCount}/{enabledRules.Count} 个规则");
                }
                else
                {
                    AppendLog("启动TCP中继服务失败，没有成功启动任何规则");
                }
            }
            catch (Exception ex)
            {
                UpdateServiceStatus();
                AppendLog($"启动服务时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新服务状态显示
        /// </summary>
        private void UpdateServiceStatus()
        {
            var activeRulesCount = _relayManager.ActiveRulesCount;
            var totalEnabledRules = _configManager.Config.RouteRules.Count(r => r.IsEnabled);

            if (activeRulesCount > 0)
            {
                _statusLabel.Text = $"状态: 运行中 ({activeRulesCount}/{totalEnabledRules})";
                _statusLabel.ForeColor = Color.Green;
                _stopButton.Enabled = true;
                _startButton.Enabled = activeRulesCount < totalEnabledRules; // 如果还有未启动的规则，允许继续启动
            }
            else
            {
                _statusLabel.Text = "状态: 已停止";
                _statusLabel.ForeColor = Color.Red;
                _startButton.Enabled = totalEnabledRules > 0;
                _stopButton.Enabled = false;
            }
        }

        private async void StopButton_Click(object? sender, EventArgs e)
        {
            try
            {
                _stopButton.Enabled = false;
                await _relayManager.StopAllAsync();

                // 更新UI状态
                UpdateServiceStatus();
                LoadRouteRules(); // 刷新规则列表以显示运行状态

                // 清空连接列表
                _connectionsListView.Items.Clear();
                AppendLog("TCP中继服务已停止");
            }
            catch (Exception ex)
            {
                UpdateServiceStatus();
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

            // 更新选中的规则ID
            if (hasSelection)
            {
                var selectedItem = _routeRulesListView.SelectedItems[0];
                _selectedRuleId = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(_selectedRuleId))
                {
                    UpdateConnectionsListForRule(_selectedRuleId);
                }
            }
            else
            {
                _selectedRuleId = null;
                // 没有选中规则时显示所有连接
                UpdateConnectionsList();
            }
        }

        private void LoadRouteRules()
        {
            _routeRulesListView.Items.Clear();

            foreach (var rule in _configManager.Config.RouteRules)
            {
                var item = new ListViewItem(rule.Name);
                item.SubItems.Add(rule.DataSourceEndpoint);  // 数据源
                item.SubItems.Add($"0.0.0.0:{rule.LocalServerPort}");  // 本地监听
                item.SubItems.Add(rule.IsEnabled ? "启用" : "禁用");  // 规则状态

                // 运行状态 - 检查是否正在运行
                var isRunning = _relayManager.IsRuleRunning(rule.Id);
                item.SubItems.Add(isRunning ? "运行中" : "已停止");  // 运行状态

                item.SubItems.Add(rule.ForwardedPackets.ToString());  // 转发包数
                item.SubItems.Add(rule.ForwardedBytes.ToString());  // 转发字节
                item.SubItems.Add(rule.CreatedTime.ToString("yyyy-MM-dd HH:mm"));  // 创建时间
                item.SubItems.Add(rule.Description);  // 描述

                item.Tag = rule.Id;

                // 根据规则状态和运行状态设置颜色
                if (!rule.IsEnabled)
                    item.BackColor = Color.LightGray;
                else if (isRunning)
                    item.BackColor = Color.LightGreen;
                else
                    item.BackColor = Color.White;

                _routeRulesListView.Items.Add(item);
            }
        }

        private void OnConnectionChanged(object? sender, ConnectionEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnConnectionChanged(sender, e)));
                return;
            }

            UpdateConnectionsList();
            if (e.Connection != null)
            {
                AppendLog($"连接状态变化: {e.Connection} - {e.Message}");
            }
        }

        private void OnDataTransferred(object? sender, DataTransferEventArgs e)
        {
            if (e.ConnectionId != null)
            {
                _dataAuditor.LogDataForward(e.Direction ?? "Unknown", new byte[e.BytesTransferred], e.ConnectionId);
            }
        }

        private void OnLogMessage(object? sender, string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnLogMessage(sender, message)));
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

        /// <summary>
        /// 更新指定规则的连接列表（只显示消费端连接）
        /// </summary>
        private void UpdateConnectionsListForRule(string ruleId)
        {
            _connectionsListView.Items.Clear();

            // 只显示消费端连接
            foreach (var connection in _relayManager.GetConnectionsByRule(ruleId)
                .Where(c => c.Type == ConnectionType.ConsumerServer))
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

            // 更新统计信息（仅针对选中规则的消费端连接）
            var consumerConnections = _relayManager.GetConnectionsByRule(ruleId)
                .Where(c => c.Type == ConnectionType.ConsumerServer).ToList();
            var totalBytes = consumerConnections.Sum(c => c.ReceivedBytes + c.SentBytes);
            _statisticsLabel.Text = $"消费端连接数: {consumerConnections.Count} | 转发量: {totalBytes:N0} 字节";
        }

        /// <summary>
        /// 定时器刷新事件
        /// </summary>
        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // 刷新规则列表
                LoadRouteRules();

                // 如果有选中的规则，刷新其连接信息
                if (!string.IsNullOrEmpty(_selectedRuleId))
                {
                    UpdateConnectionsListForRule(_selectedRuleId);
                }
                else
                {
                    // 没有选中规则时显示所有连接
                    UpdateConnectionsList();
                }

                // 更新服务状态
                UpdateServiceStatus();
            }
            catch (Exception ex)
            {
                // 静默处理刷新异常，避免影响用户体验
                System.Diagnostics.Debug.WriteLine($"定时刷新异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 右键菜单打开事件
        /// </summary>
        private void RouteRulesContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var hasSelection = _routeRulesListView.SelectedItems.Count > 0;
            if (!hasSelection)
            {
                e.Cancel = true;
                return;
            }

            var selectedItem = _routeRulesListView.SelectedItems[0];
            var ruleId = selectedItem.Tag?.ToString();
            var rule = _configManager.Config.RouteRules.FirstOrDefault(r => r.Id == ruleId);

            if (rule == null)
            {
                e.Cancel = true;
                return;
            }

            var isRunning = _relayManager.IsRuleRunning(ruleId!);

            // 根据规则状态和运行状态设置菜单项可用性
            _routeRulesContextMenu.Items[0].Enabled = rule.IsEnabled && !isRunning; // 启动服务
            _routeRulesContextMenu.Items[1].Enabled = isRunning; // 停止服务
            _routeRulesContextMenu.Items[3].Enabled = true; // 编辑规则
            _routeRulesContextMenu.Items[4].Enabled = !isRunning; // 删除规则
        }

        /// <summary>
        /// 启动选中的规则
        /// </summary>
        private async void StartSelectedRule()
        {
            if (_routeRulesListView.SelectedItems.Count == 0) return;

            var selectedItem = _routeRulesListView.SelectedItems[0];
            var ruleId = selectedItem.Tag?.ToString();
            var rule = _configManager.Config.RouteRules.FirstOrDefault(r => r.Id == ruleId);

            if (rule != null && rule.IsEnabled)
            {
                var success = await _relayManager.StartAsync(rule);
                if (success)
                {
                    AppendLog($"已启动规则: {rule.Name}");
                }
                else
                {
                    AppendLog($"启动规则失败: {rule.Name}");
                }
                UpdateServiceStatus();
            }
        }

        /// <summary>
        /// 停止选中的规则
        /// </summary>
        private async void StopSelectedRule()
        {
            if (_routeRulesListView.SelectedItems.Count == 0) return;

            var selectedItem = _routeRulesListView.SelectedItems[0];
            var ruleId = selectedItem.Tag?.ToString();
            var rule = _configManager.Config.RouteRules.FirstOrDefault(r => r.Id == ruleId);

            if (rule != null && !string.IsNullOrEmpty(ruleId))
            {
                await _relayManager.StopAsync(ruleId);
                AppendLog($"已停止规则: {rule.Name}");
                UpdateServiceStatus();
            }
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

            // 检查日志大小，如果超过限制则清理
            CheckAndManageLogSize();
        }

        /// <summary>
        /// 检查并管理日志大小
        /// </summary>
        private void CheckAndManageLogSize()
        {
            try
            {
                var logSizeBytes = System.Text.Encoding.UTF8.GetByteCount(_logTextBox.Text);

                if (logSizeBytes > MaxLogSizeBytes)
                {
                    // 保留最后50%的日志内容
                    var lines = _logTextBox.Lines;
                    var keepLines = lines.Skip(lines.Length / 2).ToArray();

                    _logTextBox.Clear();
                    _logTextBox.Lines = keepLines;

                    // 添加日志管理提示
                    var managementMessage = $"[{DateTime.Now:HH:mm:ss.fff}] 日志大小超过{MaxLogSizeBytes / (1024 * 1024)}MB限制，已自动清理旧日志";
                    _logTextBox.AppendText(managementMessage + Environment.NewLine);

                    _logTextBox.SelectionStart = _logTextBox.Text.Length;
                    _logTextBox.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                // 日志管理失败时的静默处理
                System.Diagnostics.Debug.WriteLine($"日志大小管理异常: {ex.Message}");
            }
        }



        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void ClearLogButton_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("确定要清空所有日志吗？", "确认清空",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _logTextBox.Clear();
                AppendLog("日志已清空");
            }
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
