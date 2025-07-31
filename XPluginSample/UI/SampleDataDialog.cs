using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XPluginSample.Models;
using XPlugin.Theme;

namespace XPluginSample.UI
{
    /// <summary>
    /// 示例数据编辑对话框
    /// </summary>
    public partial class SampleDataDialog : Form
    {
        private TextBox _nameTextBox = null!;
        private TextBox _valueTextBox = null!;
        private TextBox _descriptionTextBox = null!;
        private CheckBox _enabledCheckBox = null!;
        private TextBox _tagsTextBox = null!;

        private readonly SampleData? _existingData;

        public SampleDataDialog(SampleData? data = null)
        {
            _existingData = data;
            InitializeComponent();
            LoadDataToForm();
            
            // 应用主题
            ThemeManager.ApplyTheme(this);
        }

        private void InitializeComponent()
        {
            Text = _existingData == null ? "添加数据" : "编辑数据";
            Size = new Size(450, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 数据名称
            var nameLabel = new Label
            {
                Text = "数据名称:",
                Location = new Point(20, 20),
                Size = new Size(80, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _nameTextBox = new TextBox
            {
                Location = new Point(110, 20),
                Size = new Size(300, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            // 数据值
            var valueLabel = new Label
            {
                Text = "数据值:",
                Location = new Point(20, 60),
                Size = new Size(80, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _valueTextBox = new TextBox
            {
                Location = new Point(110, 60),
                Size = new Size(300, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            // 是否启用
            _enabledCheckBox = new CheckBox
            {
                Text = "启用此数据",
                Location = new Point(110, 100),
                Size = new Size(200, 23),
                Font = new Font("Microsoft YaHei", 9F),
                Checked = true
            };

            // 标签
            var tagsLabel = new Label
            {
                Text = "标签:",
                Location = new Point(20, 140),
                Size = new Size(80, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _tagsTextBox = new TextBox
            {
                Location = new Point(110, 140),
                Size = new Size(300, 23),
                Font = new Font("Microsoft YaHei", 9F),
                PlaceholderText = "多个标签用逗号分隔"
            };

            // 描述
            var descriptionLabel = new Label
            {
                Text = "描述信息:",
                Location = new Point(20, 180),
                Size = new Size(80, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _descriptionTextBox = new TextBox
            {
                Location = new Point(110, 180),
                Size = new Size(300, 120),
                Font = new Font("Microsoft YaHei", 9F),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            // 按钮
            var okButton = new Button
            {
                Text = "确定",
                Location = new Point(250, 320),
                Size = new Size(75, 30),
                Font = new Font("Microsoft YaHei", 9F),
                DialogResult = DialogResult.OK,
                UseVisualStyleBackColor = true
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(335, 320),
                Size = new Size(75, 30),
                Font = new Font("Microsoft YaHei", 9F),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true
            };

            // 添加控件
            Controls.AddRange(new Control[]
            {
                nameLabel, _nameTextBox,
                valueLabel, _valueTextBox,
                _enabledCheckBox,
                tagsLabel, _tagsTextBox,
                descriptionLabel, _descriptionTextBox,
                okButton, cancelButton
            });

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadDataToForm()
        {
            if (_existingData != null)
            {
                _nameTextBox.Text = _existingData.Name;
                _valueTextBox.Text = _existingData.Value;
                _enabledCheckBox.Checked = _existingData.IsEnabled;
                _descriptionTextBox.Text = _existingData.Description;
                _tagsTextBox.Text = string.Join(", ", _existingData.Tags);
            }
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            if (!ValidateInput())
            {
                DialogResult = DialogResult.None;
                return;
            }
        }

        private bool ValidateInput()
        {
            // 验证数据名称
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("请输入数据名称", "验证失败", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _nameTextBox.Focus();
                return false;
            }

            // 验证数据值
            if (string.IsNullOrWhiteSpace(_valueTextBox.Text))
            {
                MessageBox.Show("请输入数据值", "验证失败", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _valueTextBox.Focus();
                return false;
            }

            return true;
        }

        public SampleData GetSampleData()
        {
            var data = _existingData?.Clone() ?? new SampleData();
            
            data.Name = _nameTextBox.Text.Trim();
            data.Value = _valueTextBox.Text.Trim();
            data.IsEnabled = _enabledCheckBox.Checked;
            data.Description = _descriptionTextBox.Text.Trim();
            
            // 解析标签
            var tagsText = _tagsTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(tagsText))
            {
                data.Tags = tagsText.Split(',')
                    .Select(tag => tag.Trim())
                    .Where(tag => !string.IsNullOrEmpty(tag))
                    .ToList();
            }
            else
            {
                data.Tags.Clear();
            }

            return data;
        }
    }
}
