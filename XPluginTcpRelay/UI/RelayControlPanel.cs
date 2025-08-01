using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using XPlugin.Auditing;
using XPlugin.Configuration;
using XPlugin.Network;
using XPlugin.Services;
using XPlugin.logs;
using XPluginTcpRelay.Models;
using XPluginTcpRelay.Services;

namespace XPluginTcpRelay.UI
{
    /// <summary>
    /// TCP中继控制面板 - 重构后的UI层，与业务层解耦
    /// </summary>
    public partial class RelayControlPanel : UserControl
    {
        // 业务服务层
        private readonly IDataRelayService _relayService;
        private readonly IConfigurationService<RelayConfig> _configService;
        private readonly IAuditService? _auditService;
        private readonly IServiceFactory _serviceFactory;

        // UI状态管理
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

        /// <summary>
        /// 构造函数 - 使用依赖注入
        /// </summary>
        /// <param name="serviceFactory">服务工厂</param>
        public RelayControlPanel(IServiceFactory? serviceFactory = null)
        {
            // 初始化服务工厂
            _serviceFactory = serviceFactory ?? CreateDefaultServiceFactory();

            // 获取业务服务
            _configService = _serviceFactory.GetService<IConfigurationService<RelayConfig>>();
            _relayService = _serviceFactory.GetService<IDataRelayService>();
            _serviceFactory.TryGetService<IAuditService>(out _auditService);

            InitializeComponent();

            // 初始化定时器
            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 5000; // 5秒刷新一次，减少刷新频率
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();

            // 初始化右键菜单
            InitializeContextMenu();

            SetupEventHandlers();

            // 异步加载配置和规则
            _ = InitializeAsync();
        }

        /// <summary>
        /// 创建默认服务工厂
        /// </summary>
        private IServiceFactory CreateDefaultServiceFactory()
        {
            var factory = new DefaultServiceFactory();

            // 创建并注册配置服务实例
            var configService = new RelayConfigService("relay_config.json", true);
            factory.RegisterInstance<IConfigurationService<RelayConfig>>(configService);

            // 创建并注册审计服务实例
            var auditOptions = new AuditServiceOptions
            {
                Enabled = true,
                Level = AuditLevel.Info,
                LogFilePath = "tcp_relay_audit.log",
                BufferSize = 1000,
                LogDataContent = true,
                MaxDataLength = 1024
            };
            var auditService = factory.CreateAuditService(auditOptions);
            factory.RegisterInstance<IAuditService>(auditService);

            // 创建并注册中继服务实例
            var relayService = new TcpRelayService(auditService);
            factory.RegisterInstance<IDataRelayService>(relayService);

            return factory;
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                // 加载配置
                await _configService.LoadAsync();

                // 加载路由规则到UI
                LoadRouteRules();

                AppendLog("INFO: 系统初始化完成");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 系统初始化失败: {ex.Message}");
                Log.Error($"RelayControlPanel初始化失败: {ex}");
            }
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
                Size = new Size(130, 30),
                BackColor = Color.LightGreen
            };

            _stopButton = new Button
            {
                Text = "一键停止所有规则",
                Location = new Point(160, 80),
                Size = new Size(130, 30),
                BackColor = Color.LightCoral,
                Enabled = false
            };

            // 路由规则管理按钮
            _addRuleButton = new Button
            {
                Text = "添加规则",
                Location = new Point(300, 80),
                Size = new Size(80, 30)
            };

            _editRuleButton = new Button
            {
                Text = "编辑规则",
                Location = new Point(390, 80),
                Size = new Size(80, 30),
                Enabled = false
            };

            _deleteRuleButton = new Button
            {
                Text = "删除规则",
                Location = new Point(480, 80),
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
            _routeRulesListView.Columns.Add("数据源连接", 90);
            _routeRulesListView.Columns.Add("转发包数", 80);
            _routeRulesListView.Columns.Add("转发字节", 100);
            _routeRulesListView.Columns.Add("创建时间", 140);
            _routeRulesListView.Columns.Add("描述", 150);

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
            _connectionsListView.Columns.Add("从数据源接收", 90);  // 修正：从数据源接收的字节数
            _connectionsListView.Columns.Add("发送到数据源", 90);  // 修正：发送到数据源的字节数
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
            _routeRulesListView.MouseDown += RouteRulesListView_MouseDown;

            // 订阅中继服务事件
            _relayService.ConnectionChanged += OnConnectionChanged;
            _relayService.DataTransferred += OnDataTransferred;
            _relayService.LogMessage += OnLogMessage;

            // 订阅配置服务事件
            _configService.ConfigurationChanged += OnConfigurationChanged;
        }

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // 检查服务是否已经运行
                if (!_relayService.IsRunning)
                {
                    AppendLog("正在启动TCP中继服务...");

                    // 启动中继服务
                    var success = await _relayService.StartAsync();
                    if (success)
                    {
                        AppendLog("TCP中继服务启动成功");
                    }
                    else
                    {
                        AppendLog("TCP中继服务启动失败");
                        UpdateServiceStatus();
                        return;
                    }
                }
                else
                {
                    AppendLog("TCP中继服务已在运行中");
                }

                    // 启动所有启用的规则（跳过已运行的规则）
                    var config = _configService.Configuration;
                    var enabledRules = config.RouteRules.Where(r => r.IsEnabled).ToList();
                    var rulesToStart = enabledRules.Where(r => !_relayService.IsRuleActive(r.Id)).ToList();

                    if (rulesToStart.Count == 0)
                    {
                        AppendLog("所有启用的规则都已在运行中");
                    }
                    else
                    {
                        AppendLog($"开始启动 {rulesToStart.Count} 个未运行的规则...");

                        int successCount = 0;
                        foreach (var rule in rulesToStart)
                        {
                            try
                            {
                                var ruleSuccess = await _relayService.StartRelayRuleAsync(rule);
                                if (ruleSuccess)
                                {
                                    successCount++;
                                    AppendLog($"规则 '{rule.Name}' 启动成功，监听端口: {rule.LocalServerPort}");
                                }
                                else
                                {
                                    AppendLog($"规则 '{rule.Name}' 启动失败");
                                }
                            }
                            catch (Exception ex)
                            {
                                AppendLog($"规则 '{rule.Name}' 启动异常: {ex.Message}");
                            }
                        }

                        AppendLog($"TCP中继服务启动完成，成功启动 {successCount}/{rulesToStart.Count} 个规则");
                    }

                // 更新UI状态
                UpdateServiceStatus();
                LoadRouteRules(); // 刷新规则列表以显示运行状态
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
            var isRunning = _relayService.IsRunning;
            var totalEnabledRules = _configService.Configuration.RouteRules.Count(r => r.IsEnabled);

            if (isRunning)
            {
                _statusLabel.Text = $"状态: 运行中 ({totalEnabledRules} 个规则)";
                _statusLabel.ForeColor = Color.Green;
                _stopButton.Enabled = true;
                _startButton.Enabled = true; // 允许添加新规则后重新启动
            }
            else
            {
                _statusLabel.Text = "状态: 已停止";
                _statusLabel.ForeColor = Color.Red;
                _startButton.Enabled = true; // 始终允许启动
                _stopButton.Enabled = false;
            }
        }

        private async void StopButton_Click(object? sender, EventArgs e)
        {
            try
            {
                _stopButton.Enabled = false;
                AppendLog("正在停止TCP中继服务...");

                await _relayService.StopAsync();

                // 立即更新UI状态
                UpdateServiceStatus();
                LoadRouteRules(); // 刷新规则列表以显示运行状态

                // 清空连接列表
                _connectionsListView.Items.Clear();
                _selectedRuleId = null; // 清除选中状态

                // 更新统计信息
                _statisticsLabel.Text = "连接数: 0 | 转发量: 0 字节";

                AppendLog("TCP中继服务已停止，所有连接已关闭");
            }
            catch (Exception ex)
            {
                UpdateServiceStatus();
                AppendLog($"停止服务时发生错误: {ex.Message}");
            }
            finally
            {
                _stopButton.Enabled = false; // 停止后禁用停止按钮
            }
        }

        private void AddRuleButton_Click(object? sender, EventArgs e)
        {
            var dialog = new RouteRuleDialog();
            if (dialog.ShowDialog() == DialogResult.OK && dialog.RouteRule != null)
            {
                var config = _configService.Configuration;
                if (config.AddRule(dialog.RouteRule))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _configService.SaveAsync(config);
                            Invoke(new Action(() =>
                            {
                                LoadRouteRules();
                                AppendLog($"添加规则成功: {dialog.RouteRule.Name}");
                            }));
                        }
                        catch (Exception ex)
                        {
                            Invoke(new Action(() => AppendLog($"保存配置失败: {ex.Message}")));
                        }
                    });
                }
                else
                {
                    AppendLog($"添加规则失败: 规则验证不通过或名称重复");
                }
            }
        }

        private void EditRuleButton_Click(object? sender, EventArgs e)
        {
            if (_routeRulesListView.SelectedItems.Count == 0) return;

            var selectedItem = _routeRulesListView.SelectedItems[0];
            var ruleId = selectedItem.Tag?.ToString();
            if (string.IsNullOrEmpty(ruleId)) return;

            var rule = _configService.Configuration.FindRuleById(ruleId);
            if (rule == null) return;

            var dialog = new RouteRuleDialog(rule);
            if (dialog.ShowDialog() == DialogResult.OK && dialog.RouteRule != null)
            {
                var config = _configService.Configuration;
                if (config.UpdateRule(dialog.RouteRule))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _configService.SaveAsync(config);
                            Invoke(new Action(() =>
                            {
                                LoadRouteRules();
                                AppendLog($"更新规则成功: {dialog.RouteRule.Name}");
                            }));
                        }
                        catch (Exception ex)
                        {
                            Invoke(new Action(() => AppendLog($"保存配置失败: {ex.Message}")));
                        }
                    });
                }
                else
                {
                    AppendLog($"更新规则失败: 规则验证不通过");
                }
            }
        }

        private async void DeleteRuleButton_Click(object? sender, EventArgs e)
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
                try
                {
                    AppendLog($"正在删除规则: {ruleName}");

                    // 1. 从中继服务中删除规则（会先停止运行再删除）
                    var relayService = _relayService as TcpRelayService;
                    if (relayService != null)
                    {
                        await relayService.DeleteRelayRuleAsync(ruleId);
                    }

                    // 2. 从配置中删除规则
                    var config = _configService.Configuration;
                    if (config.RemoveRule(ruleId))
                    {
                        await _configService.SaveAsync(config);
                        LoadRouteRules();

                        // 如果删除的是当前选中的规则，清空连接列表
                        if (_selectedRuleId == ruleId)
                        {
                            _connectionsListView.Items.Clear();
                            _statisticsLabel.Text = "连接数: 0 | 转发量: 0 字节";
                            _selectedRuleId = null;
                        }

                        AppendLog($"删除规则成功: {ruleName}");
                    }
                    else
                    {
                        AppendLog($"删除规则失败: 规则不存在");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"删除规则异常: {ruleName} - {ex.Message}");
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

        /// <summary>
        /// 处理ListView鼠标按下事件，用于右键菜单定位
        /// </summary>
        private void RouteRulesListView_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTest = _routeRulesListView.HitTest(e.Location);
                if (hitTest.Item != null)
                {
                    // 选中右键点击的项
                    _routeRulesListView.SelectedItems.Clear();
                    hitTest.Item.Selected = true;
                    _selectedRuleId = hitTest.Item.Tag?.ToString();
                }
            }
        }

        private void LoadRouteRules()
        {
            // 保存当前选中的规则ID
            var selectedRuleId = _selectedRuleId;

            _routeRulesListView.Items.Clear();

            foreach (var rule in _configService.Configuration.RouteRules)
            {
                var item = new ListViewItem(rule.Name);
                item.SubItems.Add(rule.DataSourceEndpoint);  // 数据源
                item.SubItems.Add($"0.0.0.0:{rule.LocalServerPort}");  // 本地监听
                item.SubItems.Add(rule.IsEnabled ? "启用" : "禁用");  // 规则状态

                // 运行状态 - 检查是否正在运行
                var isRunning = _relayService.IsRunning && rule.IsEnabled && _relayService.IsRuleActive(rule.Id);
                item.SubItems.Add(isRunning ? "运行中" : "已停止");  // 运行状态

                // 数据源连接状态
                var dataSourceStatus = "未知";
                var dataSourceColor = Color.Gray;
                try
                {
                    if (_relayService is TcpRelayService tcpService)
                    {
                        var status = tcpService.GetDataSourceConnectionStatus(rule.DataSourceIp, rule.DataSourcePort);
                        switch (status)
                        {
                            case DataSourceConnectionStatus.Connected:
                                dataSourceStatus = "已连接";
                                dataSourceColor = Color.Green;
                                break;
                            case DataSourceConnectionStatus.Connecting:
                                dataSourceStatus = "连接中";
                                dataSourceColor = Color.Orange;
                                break;
                            case DataSourceConnectionStatus.Disconnected:
                                dataSourceStatus = "未连接";
                                dataSourceColor = Color.Red;
                                break;
                            case DataSourceConnectionStatus.Error:
                                dataSourceStatus = "错误";
                                dataSourceColor = Color.Red;
                                break;
                        }
                    }
                }
                catch
                {
                    dataSourceStatus = "未知";
                    dataSourceColor = Color.Gray;
                }

                var dataSourceSubItem = item.SubItems.Add(dataSourceStatus);  // 数据源连接状态
                dataSourceSubItem.ForeColor = dataSourceColor;

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

            // 恢复之前选中的规则
            if (!string.IsNullOrEmpty(selectedRuleId))
            {
                foreach (ListViewItem item in _routeRulesListView.Items)
                {
                    if (item.Tag?.ToString() == selectedRuleId)
                    {
                        item.Selected = true;
                        _selectedRuleId = selectedRuleId;
                        break;
                    }
                }
            }
        }

        private void OnConnectionChanged(object? sender, XPlugin.Network.ConnectionEventArgs e)
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

        private void OnDataTransferred(object? sender, XPlugin.Network.DataTransferEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnDataTransferred(sender, e)));
                return;
            }

            // 记录数据传输日志，包含报文内容
            var direction = e.Direction == XPlugin.Network.DataDirection.Received ? "C→A" : "A→C";
            var hexData = e.Data != null ? BitConverter.ToString(e.Data).Replace("-", " ") : "";
            var asciiData = e.Data != null ? System.Text.Encoding.UTF8.GetString(e.Data).Replace('\0', '.') : "";

            // 限制显示长度
            if (hexData.Length > 100)
            {
                hexData = hexData.Substring(0, 100) + "...";
            }
            if (asciiData.Length > 50)
            {
                asciiData = asciiData.Substring(0, 50) + "...";
            }

            AppendLog($"数据传输: {direction} - {e.DataLength} 字节 | HEX: {hexData} | ASCII: {asciiData}");
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

        private void OnConfigurationChanged(object? sender, XPlugin.Configuration.ConfigurationChangedEventArgs<RelayConfig> e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnConfigurationChanged(sender, e)));
                return;
            }

            AppendLog($"配置已更新: {e.ChangeType}");
            LoadRouteRules();
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

            try
            {
                // 获取所有活跃连接
                var connections = (_relayService as TcpRelayService)?.GetActiveConnections() ?? Enumerable.Empty<Models.ConnectionInfo>();

                foreach (var connection in connections)
                {
                    var item = new ListViewItem(connection.Id.Length > 8 ? connection.Id[..8] + "..." : connection.Id);
                    item.SubItems.Add(connection.Type.ToString());
                    item.SubItems.Add(connection.RemoteEndPoint?.ToString() ?? "");
                    item.SubItems.Add(connection.Status.ToString());

                    // 计算连接持续时间
                    var duration = DateTime.Now - connection.ConnectedTime;
                    item.SubItems.Add(duration.ToString(@"hh\:mm\:ss"));

                    item.SubItems.Add(connection.ReceivedBytes.ToString("N0"));
                    item.SubItems.Add(connection.SentBytes.ToString("N0"));
                    item.SubItems.Add(connection.LastActivityTime.ToString("HH:mm:ss"));

                    // 根据连接状态设置颜色
                    switch (connection.Status)
                    {
                        case Models.ConnectionStatus.Connected:
                            item.BackColor = Color.LightGreen;
                            break;
                        case Models.ConnectionStatus.Disconnected:
                            item.BackColor = Color.LightGray;
                            break;
                        case Models.ConnectionStatus.Error:
                            item.BackColor = Color.LightPink;
                            break;
                    }

                    _connectionsListView.Items.Add(item);
                }

                // 更新统计信息
                var totalBytes = connections.Sum(c => c.ReceivedBytes + c.SentBytes);
                _statisticsLabel.Text = $"连接数: {connections.Count()} | 转发量: {totalBytes:N0} 字节";
            }
            catch (Exception ex)
            {
                // 静默处理异常，避免影响UI
                System.Diagnostics.Debug.WriteLine($"更新连接列表异常: {ex.Message}");
                _statisticsLabel.Text = "连接数: 0 | 转发量: 0 字节";
            }
        }

        /// <summary>
        /// 更新指定规则的连接列表（只显示消费端连接）
        /// </summary>
        private void UpdateConnectionsListForRule(string ruleId)
        {
            _connectionsListView.Items.Clear();

            try
            {
                // 获取指定规则的连接
                var connections = (_relayService as TcpRelayService)?.GetConnectionsByRule(ruleId) ?? Enumerable.Empty<Models.ConnectionInfo>();

                foreach (var connection in connections)
                {
                    var item = new ListViewItem(connection.Id.Length > 8 ? connection.Id[..8] + "..." : connection.Id);
                    item.SubItems.Add("消费端");
                    item.SubItems.Add(connection.RemoteEndPoint?.ToString() ?? "");
                    item.SubItems.Add(connection.Status.ToString());

                    // 计算连接持续时间
                    var duration = DateTime.Now - connection.ConnectedTime;
                    item.SubItems.Add(duration.ToString(@"hh\:mm\:ss"));

                    item.SubItems.Add(connection.ReceivedBytes.ToString("N0"));
                    item.SubItems.Add(connection.SentBytes.ToString("N0"));
                    item.SubItems.Add(connection.LastActivityTime.ToString("HH:mm:ss"));

                    // 根据连接状态设置颜色
                    switch (connection.Status)
                    {
                        case Models.ConnectionStatus.Connected:
                            item.BackColor = Color.LightGreen;
                            break;
                        case Models.ConnectionStatus.Disconnected:
                            item.BackColor = Color.LightGray;
                            break;
                        case Models.ConnectionStatus.Error:
                            item.BackColor = Color.LightPink;
                            break;
                    }

                    _connectionsListView.Items.Add(item);
                }

                // 更新统计信息（仅针对选中规则的消费端连接）
                var totalBytes = connections.Sum(c => c.ReceivedBytes + c.SentBytes);
                _statisticsLabel.Text = $"消费端连接数: {connections.Count()} | 转发量: {totalBytes:N0} 字节";
            }
            catch (Exception ex)
            {
                // 静默处理异常，避免影响UI
                System.Diagnostics.Debug.WriteLine($"更新规则连接列表异常: {ex.Message}");
                _statisticsLabel.Text = "消费端连接数: 0 | 转发量: 0 字节";
            }
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
            var rule = _configService.Configuration.RouteRules.FirstOrDefault(r => r.Id == ruleId);

            if (rule == null)
            {
                e.Cancel = true;
                return;
            }

            // 检查规则是否正在运行（修复版本：即使服务未启动也能正确判断）
            var isRuleRunning = false;
            try
            {
                isRuleRunning = _relayService.IsRunning && rule.IsEnabled && _relayService.IsRuleActive(rule.Id);
            }
            catch
            {
                // 如果检查失败，默认为未运行
                isRuleRunning = false;
            }

            // 根据规则状态和运行状态设置菜单项可用性
            _routeRulesContextMenu.Items[0].Enabled = rule.IsEnabled && !isRuleRunning; // 启动服务
            _routeRulesContextMenu.Items[1].Enabled = isRuleRunning; // 停止服务
            _routeRulesContextMenu.Items[3].Enabled = true; // 编辑规则
            _routeRulesContextMenu.Items[4].Enabled = !isRuleRunning; // 删除规则
        }

        /// <summary>
        /// 启动选中的规则
        /// </summary>
        private async void StartSelectedRule()
        {
            if (_routeRulesListView.SelectedItems.Count == 0) return;

            var selectedItem = _routeRulesListView.SelectedItems[0];
            var ruleId = selectedItem.Tag?.ToString();
            var rule = _configService.Configuration.RouteRules.FirstOrDefault(r => r.Id == ruleId);

            if (rule != null && rule.IsEnabled)
            {
                try
                {
                    // 确保服务已启动
                    if (!_relayService.IsRunning)
                    {
                        var serviceStarted = await _relayService.StartAsync();
                        if (!serviceStarted)
                        {
                            AppendLog("启动中继服务失败，无法启动规则");
                            return;
                        }
                    }

                    var success = await _relayService.StartRelayRuleAsync(rule);
                    if (success)
                    {
                        AppendLog($"已启动规则: {rule.Name}");
                    }
                    else
                    {
                        AppendLog($"启动规则失败: {rule.Name}");
                    }
                    UpdateServiceStatus();
                    LoadRouteRules(); // 刷新规则列表以显示状态变化
                }
                catch (Exception ex)
                {
                    AppendLog($"启动规则异常: {rule.Name} - {ex.Message}");
                }
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
            var rule = _configService.Configuration.RouteRules.FirstOrDefault(r => r.Id == ruleId);

            if (rule != null && !string.IsNullOrEmpty(ruleId))
            {
                try
                {
                    AppendLog($"正在停止规则: {rule.Name}...");

                    var success = await _relayService.RemoveRelayRuleAsync(ruleId);
                    if (success)
                    {
                        AppendLog($"已停止规则: {rule.Name}，相关连接已关闭");

                        // 如果停止的是当前选中的规则，清空连接列表
                        if (_selectedRuleId == ruleId)
                        {
                            _connectionsListView.Items.Clear();
                            _statisticsLabel.Text = "连接数: 0 | 转发量: 0 字节";
                        }
                    }
                    else
                    {
                        AppendLog($"停止规则失败: {rule.Name}");
                    }

                    // 立即刷新状态
                    UpdateServiceStatus();
                    LoadRouteRules(); // 刷新规则列表以显示状态变化

                    // 如果有选中规则，更新其连接信息
                    if (!string.IsNullOrEmpty(_selectedRuleId))
                    {
                        UpdateConnectionsListForRule(_selectedRuleId);
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"停止规则异常: {rule.Name} - {ex.Message}");
                }
            }
        }

        private void AppendLog(string message)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke((Action<string>)AppendLog, message);
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
                _relayService?.Dispose();
                _serviceFactory?.DisposeAll();
            }
            base.Dispose(disposing);
        }
    }
}
