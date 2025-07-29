using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace XPlugin.Theme
{
    /// <summary>
    /// 主题选择对话框 - 简化版本
    /// </summary>
    public partial class ThemeSelectionDialog : Form
    {
        private ComboBox _themeComboBox = null!;
        private Button _okButton = null!;
        private Button _cancelButton = null!;

        public ThemeConfig SelectedTheme { get; private set; } = new ThemeConfig();

        public ThemeSelectionDialog()
        {
            InitializeComponent();
            LoadThemes();
        }

        private void InitializeComponent()
        {
            Text = "主题设置";
            Size = new Size(400, 200);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            // 主题选择区域
            var themeLabel = new Label
            {
                Text = "选择主题:",
                Location = new Point(20, 30),
                Size = new Size(80, 20),
                Font = new Font("Microsoft YaHei", 10F)
            };

            _themeComboBox = new ComboBox
            {
                Location = new Point(110, 28),
                Size = new Size(250, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei", 10F)
            };
            _themeComboBox.SelectedIndexChanged += ThemeComboBox_SelectedIndexChanged;

            // 按钮区域
            _okButton = new Button
            {
                Text = "确定",
                Location = new Point(200, 80),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK,
                Font = new Font("Microsoft YaHei", 10F),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false
            };

            _cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(285, 80),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Microsoft YaHei", 10F),
                BackColor = Color.LightCoral,
                UseVisualStyleBackColor = false
            };

            // 添加控件到窗体
            Controls.AddRange(new Control[] 
            { 
                themeLabel, _themeComboBox, _okButton, _cancelButton 
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
                
                // 简单的预览效果
                BackColor = SelectedTheme.BackgroundColor;
                ForeColor = SelectedTheme.ForegroundColor;
            }
        }
    }
}
