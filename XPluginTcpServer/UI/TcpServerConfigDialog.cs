using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using XPluginTcpServer.Models;
using XPlugin.Theme;

namespace XPluginTcpServer.UI
{
    /// <summary>
    /// TCP服务器配置对话框
    /// </summary>
    public partial class TcpServerConfigDialog : Form
    {
        private TextBox _nameTextBox = null!;
        private TextBox _ipTextBox = null!;
        private NumericUpDown _portNumericUpDown = null!;
        private CheckBox _autoStartCheckBox = null!;
        private TextBox _descriptionTextBox = null!;
        private NumericUpDown _maxConnectionsNumericUpDown = null!;
        private NumericUpDown _bufferSizeNumericUpDown = null!;
        private CheckBox _enableLoggingCheckBox = null!;

        private readonly TcpServerConfig? _existingConfig;

        public TcpServerConfigDialog(TcpServerConfig? config = null)
        {
            _existingConfig = config;
            InitializeComponent();
            LoadConfigData();
            
            // 应用主题
            ThemeManager.ApplyTheme(this);
        }

        private void InitializeComponent()
        {
            Text = _existingConfig == null ? "添加TCP服务器" : "编辑TCP服务器";
            Size = new Size(450, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 服务器名称
            var nameLabel = new Label
            {
                Text = "服务器名称:",
                Location = new Point(20, 20),
                Size = new Size(100, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _nameTextBox = new TextBox
            {
                Location = new Point(130, 20),
                Size = new Size(280, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            // IP地址
            var ipLabel = new Label
            {
                Text = "IP地址:",
                Location = new Point(20, 60),
                Size = new Size(100, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _ipTextBox = new TextBox
            {
                Location = new Point(130, 60),
                Size = new Size(280, 23),
                Font = new Font("Microsoft YaHei", 9F),
                Text = "127.0.0.1"
            };

            // 端口号
            var portLabel = new Label
            {
                Text = "端口号:",
                Location = new Point(20, 100),
                Size = new Size(100, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _portNumericUpDown = new NumericUpDown
            {
                Location = new Point(130, 100),
                Size = new Size(120, 23),
                Font = new Font("Microsoft YaHei", 9F),
                Minimum = 1,
                Maximum = 65535,
                Value = 8080
            };

            // 最大连接数
            var maxConnectionsLabel = new Label
            {
                Text = "最大连接数:",
                Location = new Point(20, 140),
                Size = new Size(100, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _maxConnectionsNumericUpDown = new NumericUpDown
            {
                Location = new Point(130, 140),
                Size = new Size(120, 23),
                Font = new Font("Microsoft YaHei", 9F),
                Minimum = 1,
                Maximum = 10000,
                Value = 1000
            };

            // 缓冲区大小
            var bufferSizeLabel = new Label
            {
                Text = "缓冲区大小:",
                Location = new Point(20, 180),
                Size = new Size(100, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _bufferSizeNumericUpDown = new NumericUpDown
            {
                Location = new Point(130, 180),
                Size = new Size(120, 23),
                Font = new Font("Microsoft YaHei", 9F),
                Minimum = 512,
                Maximum = 65536,
                Value = 1024,
                Increment = 512
            };

            var bufferSizeUnitLabel = new Label
            {
                Text = "字节",
                Location = new Point(260, 180),
                Size = new Size(40, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            // 自动启动
            _autoStartCheckBox = new CheckBox
            {
                Text = "程序启动时自动启动此服务器",
                Location = new Point(130, 220),
                Size = new Size(280, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            // 启用日志
            _enableLoggingCheckBox = new CheckBox
            {
                Text = "启用详细日志记录",
                Location = new Point(130, 250),
                Size = new Size(280, 23),
                Font = new Font("Microsoft YaHei", 9F),
                Checked = true
            };

            // 描述
            var descriptionLabel = new Label
            {
                Text = "描述信息:",
                Location = new Point(20, 290),
                Size = new Size(100, 23),
                Font = new Font("Microsoft YaHei", 9F)
            };

            _descriptionTextBox = new TextBox
            {
                Location = new Point(130, 290),
                Size = new Size(280, 80),
                Font = new Font("Microsoft YaHei", 9F),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            // 按钮
            var okButton = new Button
            {
                Text = "确定",
                Location = new Point(250, 400),
                Size = new Size(75, 30),
                Font = new Font("Microsoft YaHei", 9F),
                DialogResult = DialogResult.OK,
                UseVisualStyleBackColor = true
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(335, 400),
                Size = new Size(75, 30),
                Font = new Font("Microsoft YaHei", 9F),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true
            };

            // 添加控件
            Controls.AddRange(new Control[]
            {
                nameLabel, _nameTextBox,
                ipLabel, _ipTextBox,
                portLabel, _portNumericUpDown,
                maxConnectionsLabel, _maxConnectionsNumericUpDown,
                bufferSizeLabel, _bufferSizeNumericUpDown, bufferSizeUnitLabel,
                _autoStartCheckBox,
                _enableLoggingCheckBox,
                descriptionLabel, _descriptionTextBox,
                okButton, cancelButton
            });

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadConfigData()
        {
            if (_existingConfig != null)
            {
                _nameTextBox.Text = _existingConfig.Name;
                _ipTextBox.Text = _existingConfig.IpAddress;
                _portNumericUpDown.Value = _existingConfig.Port;
                _maxConnectionsNumericUpDown.Value = _existingConfig.MaxConnections;
                _bufferSizeNumericUpDown.Value = _existingConfig.BufferSize;
                _autoStartCheckBox.Checked = _existingConfig.AutoStart;
                _enableLoggingCheckBox.Checked = _existingConfig.EnableLogging;
                _descriptionTextBox.Text = _existingConfig.Description;
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
            // 验证服务器名称
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("请输入服务器名称", "验证失败", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _nameTextBox.Focus();
                return false;
            }

            // 验证IP地址
            if (!IPAddress.TryParse(_ipTextBox.Text, out _))
            {
                MessageBox.Show("请输入有效的IP地址", "验证失败", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _ipTextBox.Focus();
                return false;
            }

            // 验证端口号
            if (_portNumericUpDown.Value < 1 || _portNumericUpDown.Value > 65535)
            {
                MessageBox.Show("端口号必须在1-65535之间", "验证失败", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _portNumericUpDown.Focus();
                return false;
            }

            return true;
        }

        public TcpServerConfig GetServerConfig()
        {
            var config = _existingConfig?.Clone() ?? new TcpServerConfig();
            
            config.Name = _nameTextBox.Text.Trim();
            config.IpAddress = _ipTextBox.Text.Trim();
            config.Port = (int)_portNumericUpDown.Value;
            config.MaxConnections = (int)_maxConnectionsNumericUpDown.Value;
            config.BufferSize = (int)_bufferSizeNumericUpDown.Value;
            config.AutoStart = _autoStartCheckBox.Checked;
            config.EnableLogging = _enableLoggingCheckBox.Checked;
            config.Description = _descriptionTextBox.Text.Trim();
            config.LastModified = DateTime.Now;

            return config;
        }
    }
}
