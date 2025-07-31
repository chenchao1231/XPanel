using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XPlugin.Theme;
using XPluginSample.Models;
using XPluginSample.Services;
using XPlugin.logs;

namespace XPluginSample.UI
{
    /// <summary>
    /// 示例插件主面板
    /// </summary>
    public class SampleMainPanel : UserControl
    {
        private readonly SampleDataService _dataService;
        private ListView _dataListView = null!;
        private TextBox _logTextBox = null!;
        private Label _statisticsLabel = null!;
        private Timer _refreshTimer = null!;

        public SampleMainPanel()
        {
            _dataService = new SampleDataService();
            InitializeComponent();
            LoadData();

            // 注册事件
            _dataService.DataChanged += OnDataChanged;

            // 注册日志输出
            Log.RegisterOutput(new UILogOutput(message => AppendLog(message)));
            AppendLog("示例插件面板已启动");

            // 应用当前主题
            ThemeManager.ApplyTheme(this);

            // 监听主题变化
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void InitializeComponent()
        {
            Size = new Size(1200, 800);
            BackColor = Color.White;
            Dock = DockStyle.Fill;

            // 标题
            var titleLabel = new Label
            {
                Text = "示例插件 - 数据管理演示",
                Font = new Font("Microsoft YaHei", 16F, FontStyle.Bold),
                Location = new Point(10, 10),
                Size = new Size(400, 30),
                ForeColor = Color.DarkBlue,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // 左侧面板 - 数据管理
            var leftPanel = new Panel
            {
                Location = new Point(10, 50),
                Size = new Size(580, 740),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };

            // 数据管理组
            var dataGroup = new GroupBox
            {
                Text = "数据管理",
                Location = new Point(5, 5),
                Size = new Size(565, 500),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // 数据列表
            _dataListView = new ListView
            {
                Location = new Point(10, 25),
                Size = new Size(545, 420),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _dataListView.Columns.Add("名称", 120);
            _dataListView.Columns.Add("值", 150);
            _dataListView.Columns.Add("状态", 60);
            _dataListView.Columns.Add("创建时间", 120);
            _dataListView.Columns.Add("描述", 150);

            // 数据操作按钮
            var dataButtonPanel = new Panel
            {
                Location = new Point(10, 455),
                Size = new Size(545, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var addButton = new Button
            {
                Text = "添加数据",
                Location = new Point(0, 5),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };
            addButton.Click += AddButton_Click;

            var editButton = new Button
            {
                Text = "编辑",
                Location = new Point(90, 5),
                Size = new Size(60, 25),
                UseVisualStyleBackColor = true
            };
            editButton.Click += EditButton_Click;

            var deleteButton = new Button
            {
                Text = "删除",
                Location = new Point(160, 5),
                Size = new Size(60, 25),
                UseVisualStyleBackColor = true
            };
            deleteButton.Click += DeleteButton_Click;

            var refreshButton = new Button
            {
                Text = "刷新",
                Location = new Point(230, 5),
                Size = new Size(60, 25),
                UseVisualStyleBackColor = true
            };
            refreshButton.Click += RefreshButton_Click;

            var clearButton = new Button
            {
                Text = "清空",
                Location = new Point(300, 5),
                Size = new Size(60, 25),
                UseVisualStyleBackColor = true
            };
            clearButton.Click += ClearButton_Click;

            dataButtonPanel.Controls.AddRange(new Control[] { addButton, editButton, deleteButton, refreshButton, clearButton });
            dataGroup.Controls.AddRange(new Control[] { _dataListView, dataButtonPanel });

            // 统计信息组
            var statsGroup = new GroupBox
            {
                Text = "统计信息",
                Location = new Point(5, 515),
                Size = new Size(565, 80),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _statisticsLabel = new Label
            {
                Location = new Point(10, 25),
                Size = new Size(545, 45),
                Font = new Font("Microsoft YaHei", 9F),
                ForeColor = Color.DarkGreen,
                Text = "统计信息加载中..."
            };

            statsGroup.Controls.Add(_statisticsLabel);

            // 功能演示组
            var demoGroup = new GroupBox
            {
                Text = "功能演示",
                Location = new Point(5, 605),
                Size = new Size(565, 125),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var demoLabel = new Label
            {
                Text = "这个示例插件演示了以下功能：\n" +
                       "• 标准的插件目录结构（Models、Services、UI、Utils、插件文档）\n" +
                       "• 数据模型和业务服务的分离\n" +
                       "• 主题系统的集成和响应\n" +
                       "• 日志系统的使用\n" +
                       "• 配置文件的管理和持久化",
                Location = new Point(10, 20),
                Size = new Size(545, 95),
                Font = new Font("Microsoft YaHei", 9F),
                ForeColor = Color.DarkSlateGray
            };

            demoGroup.Controls.Add(demoLabel);

            leftPanel.Controls.AddRange(new Control[] { dataGroup, statsGroup, demoGroup });

            // 右侧面板 - 日志和搜索
            var rightPanel = new Panel
            {
                Location = new Point(600, 50),
                Size = new Size(590, 740),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // 搜索组
            var searchGroup = new GroupBox
            {
                Text = "数据搜索",
                Location = new Point(5, 5),
                Size = new Size(575, 80),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var searchTextBox = new TextBox
            {
                Location = new Point(10, 25),
                Size = new Size(400, 25),
                Font = new Font("Microsoft YaHei", 9F),
                PlaceholderText = "输入关键词搜索数据..."
            };

            var searchButton = new Button
            {
                Text = "搜索",
                Location = new Point(420, 25),
                Size = new Size(60, 25),
                UseVisualStyleBackColor = true
            };
            searchButton.Click += (s, e) => SearchData(searchTextBox.Text);

            var resetSearchButton = new Button
            {
                Text = "重置",
                Location = new Point(490, 25),
                Size = new Size(60, 25),
                UseVisualStyleBackColor = true
            };
            resetSearchButton.Click += (s, e) => { searchTextBox.Clear(); LoadData(); };

            searchGroup.Controls.AddRange(new Control[] { searchTextBox, searchButton, resetSearchButton });

            // 日志组
            var logGroup = new GroupBox
            {
                Text = "操作日志",
                Location = new Point(5, 95),
                Size = new Size(575, 635),
                Font = new Font("Microsoft YaHei", 10F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var logButtonPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(555, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var clearLogButton = new Button
            {
                Text = "清空日志",
                Location = new Point(0, 5),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };
            clearLogButton.Click += ClearLogButton_Click;

            logButtonPanel.Controls.Add(clearLogButton);

            _logTextBox = new TextBox
            {
                Location = new Point(10, 70),
                Size = new Size(555, 555),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LimeGreen,
                Font = new Font("Consolas", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            logGroup.Controls.AddRange(new Control[] { logButtonPanel, _logTextBox });
            rightPanel.Controls.AddRange(new Control[] { searchGroup, logGroup });

            // 添加所有控件到主面板
            Controls.AddRange(new Control[] { titleLabel, leftPanel, rightPanel });

            // 启动定时器
            _refreshTimer = new Timer
            {
                Interval = 5000 // 5秒刷新一次统计信息
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

        private void LoadData()
        {
            try
            {
                _dataListView.Items.Clear();
                var dataList = _dataService.GetAllData();

                foreach (var data in dataList)
                {
                    var item = new ListViewItem(data.Name);
                    item.SubItems.Add(data.Value);
                    item.SubItems.Add(data.IsEnabled ? "启用" : "禁用");
                    item.SubItems.Add(data.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                    item.SubItems.Add(data.Description);
                    item.Tag = data.Id;

                    if (!data.IsEnabled)
                    {
                        item.ForeColor = Color.Gray;
                    }

                    _dataListView.Items.Add(item);
                }

                UpdateStatistics();
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 加载数据失败: {ex.Message}");
            }
        }

        private void SearchData(string keyword)
        {
            try
            {
                _dataListView.Items.Clear();
                var dataList = _dataService.SearchData(keyword);

                foreach (var data in dataList)
                {
                    var item = new ListViewItem(data.Name);
                    item.SubItems.Add(data.Value);
                    item.SubItems.Add(data.IsEnabled ? "启用" : "禁用");
                    item.SubItems.Add(data.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                    item.SubItems.Add(data.Description);
                    item.Tag = data.Id;

                    if (!data.IsEnabled)
                    {
                        item.ForeColor = Color.Gray;
                    }

                    _dataListView.Items.Add(item);
                }

                AppendLog($"INFO: 搜索完成，找到 {dataList.Count} 条匹配数据");
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 搜索数据失败: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                var stats = _dataService.GetStatistics();
                _statisticsLabel.Text = $"总数据: {stats.TotalCount} 条  |  " +
                                       $"启用: {stats.EnabledCount} 条  |  " +
                                       $"禁用: {stats.DisabledCount} 条  |  " +
                                       $"最新数据: {stats.NewestData?.Name ?? "无"}";
            }
            catch (Exception ex)
            {
                _statisticsLabel.Text = $"统计信息获取失败: {ex.Message}";
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

                    // 限制日志长度
                    if (_logTextBox.Lines.Length > 500)
                    {
                        var lines = _logTextBox.Lines.Skip(100).ToArray();
                        _logTextBox.Lines = lines;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"日志输出失败: {ex.Message}");
            }
        }

        private void OnDataChanged(object? sender, DataChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object?, DataChangedEventArgs>(OnDataChanged), sender, e);
                return;
            }

            LoadData();
            AppendLog($"INFO: 数据变更 - {e.ChangeType}: {e.Data?.Name ?? "批量操作"}");
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dialog = new SampleDataDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var data = dialog.GetSampleData();
                    if (_dataService.AddData(data))
                    {
                        AppendLog($"INFO: 添加数据成功: {data.Name}");
                    }
                    else
                    {
                        MessageBox.Show("添加数据失败，请检查数据是否有效或名称是否重复", "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 添加数据异常: {ex.Message}");
                MessageBox.Show($"添加数据失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditButton_Click(object? sender, EventArgs e)
        {
            if (_dataListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要编辑的数据", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var selectedItem = _dataListView.SelectedItems[0];
                var dataId = selectedItem.Tag?.ToString();
                if (string.IsNullOrEmpty(dataId)) return;

                var data = _dataService.GetDataById(dataId);
                if (data == null)
                {
                    MessageBox.Show("数据不存在", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using var dialog = new SampleDataDialog(data);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var updatedData = dialog.GetSampleData();
                    updatedData.Id = dataId;
                    
                    if (_dataService.UpdateData(updatedData))
                    {
                        AppendLog($"INFO: 更新数据成功: {updatedData.Name}");
                    }
                    else
                    {
                        MessageBox.Show("更新数据失败，请检查数据是否有效或名称是否重复", "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 编辑数据异常: {ex.Message}");
                MessageBox.Show($"编辑数据失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (_dataListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要删除的数据", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("确定要删除选中的数据吗？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    var selectedItem = _dataListView.SelectedItems[0];
                    var dataId = selectedItem.Tag?.ToString();
                    if (!string.IsNullOrEmpty(dataId))
                    {
                        if (_dataService.DeleteData(dataId))
                        {
                            AppendLog($"INFO: 删除数据成功: {dataId}");
                        }
                        else
                        {
                            MessageBox.Show("删除数据失败", "错误",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"ERROR: 删除数据异常: {ex.Message}");
                    MessageBox.Show($"删除数据失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            LoadData();
            AppendLog("INFO: 手动刷新数据完成");
        }

        private void ClearButton_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("确定要清空所有数据吗？此操作不可恢复！", "确认清空",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    _dataService.ClearAllData();
                    AppendLog("INFO: 清空所有数据完成");
                }
                catch (Exception ex)
                {
                    AppendLog($"ERROR: 清空数据异常: {ex.Message}");
                    MessageBox.Show($"清空数据失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ClearLogButton_Click(object? sender, EventArgs e)
        {
            _logTextBox.Clear();
            AppendLog("INFO: 日志已清空");
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: 刷新统计信息失败: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("示例插件面板Dispose被调用");

                    // 停止定时器
                    _refreshTimer?.Stop();
                    _refreshTimer?.Dispose();

                    // 取消事件注册
                    if (_dataService != null)
                    {
                        _dataService.DataChanged -= OnDataChanged;
                        _dataService.Dispose();
                    }

                    ThemeManager.ThemeChanged -= OnThemeChanged;

                    System.Diagnostics.Debug.WriteLine("示例插件面板Dispose完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"示例插件面板Dispose处理失败: {ex.Message}");
                }
            }
            base.Dispose(disposing);
        }
    }


}
