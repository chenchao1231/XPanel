using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace XPlugin.Theme
{
    /// <summary>
    /// 主题选择对话框
    /// </summary>
    public partial class ThemeSelectionDialog : Form
    {
        private ComboBox _themeComboBox = null!;
        private Panel _previewPanel = null!;
        private Label _previewLabel = null!;
        private ListView _previewListView = null!;
        private TextBox _previewTextBox = null!;
        private Button _okButton = null!;
        private Button _cancelButton = null!;
        private Button _customButton = null!;

        public ThemeConfig SelectedTheme { get; private set; } = new ThemeConfig();

        public ThemeSelectionDialog()
        {
            InitializeComponent();
            LoadThemes();
        }

        private void InitializeComponent()
        {
            Text = "主题设置";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            // 主题选择区域
            var themeLabel = new Label
            {
                Text = "选择主题:",
                Location = new Point(20, 20),
                Size = new Size(80, 20),
                Font = new Font("Microsoft YaHei", 10F)
            };

            _themeComboBox = new ComboBox
            {
                Location = new Point(110, 18),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei", 10F)
            };
            _themeComboBox.SelectedIndexChanged += ThemeComboBox_SelectedIndexChanged;

            _customButton = new Button
            {
                Text = "自定义主题",
                Location = new Point(320, 17),
                Size = new Size(100, 27),
                Font = new Font("Microsoft YaHei", 9F),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            _customButton.Click += CustomButton_Click;

            // 预览区域
            var previewGroupBox = new GroupBox
            {
                Text = "主题预览",
                Location = new Point(20, 60),
                Size = new Size(540, 350),
                Font = new Font("Microsoft YaHei", 10F)
            };

            _previewPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(520, 315),
                BorderStyle = BorderStyle.FixedSingle
            };

            _previewLabel = new Label
            {
                Text = "这是预览标签文本",
                Location = new Point(10, 10),
                Size = new Size(200, 20),
                Font = new Font("Microsoft YaHei", 10F)
            };

            _previewListView = new ListView
            {
                Location = new Point(10, 40),
                Size = new Size(240, 120),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _previewListView.Columns.Add("列1", 80);
            _previewListView.Columns.Add("列2", 80);
            _previewListView.Columns.Add("列3", 80);

            // 添加预览数据
            var item1 = new ListViewItem("数据1");
            item1.SubItems.Add("值1");
            item1.SubItems.Add("状态1");
            _previewListView.Items.Add(item1);

            var item2 = new ListViewItem("数据2");
            item2.SubItems.Add("值2");
            item2.SubItems.Add("状态2");
            _previewListView.Items.Add(item2);

            _previewTextBox = new TextBox
            {
                Location = new Point(260, 40),
                Size = new Size(250, 120),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Text = "[10:30:15] 这是日志预览文本\r\n[10:30:16] 系统启动成功\r\n[10:30:17] 连接建立\r\n[10:30:18] 数据传输中...",
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9F)
            };

            var previewButton = new Button
            {
                Text = "预览按钮",
                Location = new Point(10, 170),
                Size = new Size(100, 30),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };

            var previewTextInput = new TextBox
            {
                Location = new Point(120, 175),
                Size = new Size(130, 20),
                Text = "输入框预览"
            };

            _previewPanel.Controls.AddRange(new Control[] 
            { 
                _previewLabel, _previewListView, _previewTextBox, previewButton, previewTextInput 
            });

            previewGroupBox.Controls.Add(_previewPanel);

            // 按钮区域
            _okButton = new Button
            {
                Text = "确定",
                Location = new Point(400, 430),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK,
                Font = new Font("Microsoft YaHei", 10F),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };

            _cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(485, 430),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Microsoft YaHei", 10F),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };

            // 添加控件到窗体
            Controls.AddRange(new Control[] 
            { 
                themeLabel, _themeComboBox, _customButton, previewGroupBox, _okButton, _cancelButton 
            });

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private void LoadThemes()
        {
            var themes = ThemeManager.GetPredefinedThemes();
            _themeComboBox.Items.AddRange(themes);
            
            // 选择当前主题
            var currentTheme = ThemeManager.CurrentTheme;
            var matchingTheme = themes.FirstOrDefault(t => t.Equals(currentTheme));
            if (matchingTheme != null)
            {
                _themeComboBox.SelectedItem = matchingTheme;
            }
            else
            {
                _themeComboBox.SelectedIndex = 0;
            }
        }

        private void ThemeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_themeComboBox.SelectedItem is ThemeConfig selectedTheme)
            {
                SelectedTheme = selectedTheme.Clone();
                ApplyPreviewTheme(SelectedTheme);
            }
        }

        private void ApplyPreviewTheme(ThemeConfig theme)
        {
            try
            {
                // 应用到预览面板
                _previewPanel.BackColor = theme.BackgroundColor;
                _previewLabel.ForeColor = theme.ForegroundColor;
                _previewListView.BackColor = theme.ListBackgroundColor;
                _previewListView.ForeColor = theme.ForegroundColor;
                _previewTextBox.BackColor = theme.LogBackgroundColor;
                _previewTextBox.ForeColor = theme.LogForegroundColor;

                // 更新预览面板中的其他控件
                foreach (Control control in _previewPanel.Controls)
                {
                    if (control is TextBox textBox && !textBox.ReadOnly)
                    {
                        textBox.BackColor = theme.InputBackgroundColor;
                        textBox.ForeColor = theme.ForegroundColor;
                    }
                    else if (control is Button button)
                    {
                        button.ForeColor = theme.ForegroundColor;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用预览主题失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CustomButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var customDialog = new CustomThemeDialog(SelectedTheme);
                if (customDialog.ShowDialog() == DialogResult.OK)
                {
                    SelectedTheme = customDialog.CustomTheme;
                    ApplyPreviewTheme(SelectedTheme);
                    
                    // 添加到下拉列表
                    _themeComboBox.Items.Add(SelectedTheme);
                    _themeComboBox.SelectedItem = SelectedTheme;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开自定义主题对话框失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// 自定义主题对话框
    /// </summary>
    public partial class CustomThemeDialog : Form
    {
        public ThemeConfig CustomTheme { get; private set; }

        private TextBox _nameTextBox = null!;
        private Button _backgroundColorButton = null!;
        private Button _foregroundColorButton = null!;
        private Button _listBackgroundColorButton = null!;
        private Button _inputBackgroundColorButton = null!;
        private Button _logBackgroundColorButton = null!;
        private Button _logForegroundColorButton = null!;

        public CustomThemeDialog(ThemeConfig baseTheme)
        {
            CustomTheme = baseTheme.Clone();
            CustomTheme.Name = "自定义主题";
            InitializeComponent();
            LoadThemeValues();
        }

        private void InitializeComponent()
        {
            Text = "自定义主题";
            Size = new Size(400, 350);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var y = 20;
            var spacing = 35;

            // 主题名称
            var nameLabel = new Label { Text = "主题名称:", Location = new Point(20, y), Size = new Size(80, 20) };
            _nameTextBox = new TextBox { Location = new Point(110, y - 2), Size = new Size(200, 20) };
            y += spacing;

            // 背景颜色
            var bgLabel = new Label { Text = "背景颜色:", Location = new Point(20, y), Size = new Size(80, 20) };
            _backgroundColorButton = new Button { Location = new Point(110, y - 2), Size = new Size(100, 25), Text = "选择颜色" };
            _backgroundColorButton.Click += (s, e) => SelectColor(ref CustomTheme.BackgroundColor, _backgroundColorButton);
            y += spacing;

            // 前景颜色
            var fgLabel = new Label { Text = "文字颜色:", Location = new Point(20, y), Size = new Size(80, 20) };
            _foregroundColorButton = new Button { Location = new Point(110, y - 2), Size = new Size(100, 25), Text = "选择颜色" };
            _foregroundColorButton.Click += (s, e) => SelectColor(ref CustomTheme.ForegroundColor, _foregroundColorButton);
            y += spacing;

            // 列表背景颜色
            var listBgLabel = new Label { Text = "列表背景:", Location = new Point(20, y), Size = new Size(80, 20) };
            _listBackgroundColorButton = new Button { Location = new Point(110, y - 2), Size = new Size(100, 25), Text = "选择颜色" };
            _listBackgroundColorButton.Click += (s, e) => SelectColor(ref CustomTheme.ListBackgroundColor, _listBackgroundColorButton);
            y += spacing;

            // 输入框背景颜色
            var inputBgLabel = new Label { Text = "输入框背景:", Location = new Point(20, y), Size = new Size(80, 20) };
            _inputBackgroundColorButton = new Button { Location = new Point(110, y - 2), Size = new Size(100, 25), Text = "选择颜色" };
            _inputBackgroundColorButton.Click += (s, e) => SelectColor(ref CustomTheme.InputBackgroundColor, _inputBackgroundColorButton);
            y += spacing;

            // 日志背景颜色
            var logBgLabel = new Label { Text = "日志背景:", Location = new Point(20, y), Size = new Size(80, 20) };
            _logBackgroundColorButton = new Button { Location = new Point(110, y - 2), Size = new Size(100, 25), Text = "选择颜色" };
            _logBackgroundColorButton.Click += (s, e) => SelectColor(ref CustomTheme.LogBackgroundColor, _logBackgroundColorButton);
            y += spacing;

            // 日志文字颜色
            var logFgLabel = new Label { Text = "日志文字:", Location = new Point(20, y), Size = new Size(80, 20) };
            _logForegroundColorButton = new Button { Location = new Point(110, y - 2), Size = new Size(100, 25), Text = "选择颜色" };
            _logForegroundColorButton.Click += (s, e) => SelectColor(ref CustomTheme.LogForegroundColor, _logForegroundColorButton);
            y += 50;

            // 按钮
            var okButton = new Button { Text = "确定", Location = new Point(200, y), Size = new Size(75, 30), DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "取消", Location = new Point(285, y), Size = new Size(75, 30), DialogResult = DialogResult.Cancel };

            okButton.Click += (s, e) => CustomTheme.Name = _nameTextBox.Text;

            Controls.AddRange(new Control[] 
            { 
                nameLabel, _nameTextBox,
                bgLabel, _backgroundColorButton,
                fgLabel, _foregroundColorButton,
                listBgLabel, _listBackgroundColorButton,
                inputBgLabel, _inputBackgroundColorButton,
                logBgLabel, _logBackgroundColorButton,
                logFgLabel, _logForegroundColorButton,
                okButton, cancelButton
            });
        }

        private void LoadThemeValues()
        {
            _nameTextBox.Text = CustomTheme.Name;
            _backgroundColorButton.BackColor = CustomTheme.BackgroundColor;
            _foregroundColorButton.BackColor = CustomTheme.ForegroundColor;
            _listBackgroundColorButton.BackColor = CustomTheme.ListBackgroundColor;
            _inputBackgroundColorButton.BackColor = CustomTheme.InputBackgroundColor;
            _logBackgroundColorButton.BackColor = CustomTheme.LogBackgroundColor;
            _logForegroundColorButton.BackColor = CustomTheme.LogForegroundColor;
        }

        private void SelectColor(ref Color targetColor, Button button)
        {
            using var colorDialog = new ColorDialog { Color = targetColor };
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                targetColor = colorDialog.Color;
                button.BackColor = targetColor;
            }
        }
    }
}
