using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using XPluginTcpRelay.Models;

namespace XPluginTcpRelay.UI
{
    /// <summary>
    /// 路由规则配置对话框
    /// </summary>
    public partial class RouteRuleDialog : Form
    {
        private TextBox _nameTextBox;
        private TextBox _aSourceIpTextBox;
        private NumericUpDown _aSourcePortNumeric;
        private TextBox _cTargetIpTextBox;
        private NumericUpDown _cTargetPortNumeric;
        private CheckBox _isEnabledCheckBox;
        private TextBox _descriptionTextBox;
        private Button _okButton;
        private Button _cancelButton;

        /// <summary>
        /// 配置的路由规则
        /// </summary>
        public RouteRule? RouteRule { get; private set; }

        /// <summary>
        /// 创建新规则的构造函数
        /// </summary>
        public RouteRuleDialog()
        {
            InitializeComponent();
            SetupNewRule();
        }

        /// <summary>
        /// 编辑现有规则的构造函数
        /// </summary>
        public RouteRuleDialog(RouteRule existingRule)
        {
            InitializeComponent();
            LoadExistingRule(existingRule);
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // 设置窗体属性
            Text = "路由规则配置";
            Size = new Size(450, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 规则名称
            var nameLabel = new Label
            {
                Text = "规则名称:",
                Location = new Point(20, 20),
                Size = new Size(80, 20)
            };

            _nameTextBox = new TextBox
            {
                Location = new Point(110, 18),
                Size = new Size(300, 23)
            };

            // A方（数据源）配置
            var aSourceGroupBox = new GroupBox
            {
                Text = "A方（数据源）",
                Location = new Point(20, 55),
                Size = new Size(390, 80)
            };

            var aSourceIpLabel = new Label
            {
                Text = "IP地址:",
                Location = new Point(10, 25),
                Size = new Size(50, 20)
            };

            _aSourceIpTextBox = new TextBox
            {
                Location = new Point(70, 23),
                Size = new Size(150, 23),
                Text = "192.168.1.100"
            };

            var aSourcePortLabel = new Label
            {
                Text = "端口:",
                Location = new Point(240, 25),
                Size = new Size(40, 20)
            };

            _aSourcePortNumeric = new NumericUpDown
            {
                Location = new Point(285, 23),
                Size = new Size(80, 23),
                Minimum = 1,
                Maximum = 65535,
                Value = 8080
            };

            aSourceGroupBox.Controls.AddRange(new Control[]
            {
                aSourceIpLabel, _aSourceIpTextBox,
                aSourcePortLabel, _aSourcePortNumeric
            });

            // C方（目标）配置
            var cTargetGroupBox = new GroupBox
            {
                Text = "C方（目标）",
                Location = new Point(20, 145),
                Size = new Size(390, 80)
            };

            var cTargetIpLabel = new Label
            {
                Text = "IP地址:",
                Location = new Point(10, 25),
                Size = new Size(50, 20)
            };

            _cTargetIpTextBox = new TextBox
            {
                Location = new Point(70, 23),
                Size = new Size(150, 23),
                Text = "10.0.0.50"
            };

            var cTargetPortLabel = new Label
            {
                Text = "端口:",
                Location = new Point(240, 25),
                Size = new Size(40, 20)
            };

            _cTargetPortNumeric = new NumericUpDown
            {
                Location = new Point(285, 23),
                Size = new Size(80, 23),
                Minimum = 1,
                Maximum = 65535,
                Value = 8080
            };

            cTargetGroupBox.Controls.AddRange(new Control[]
            {
                cTargetIpLabel, _cTargetIpTextBox,
                cTargetPortLabel, _cTargetPortNumeric
            });

            // 启用状态
            _isEnabledCheckBox = new CheckBox
            {
                Text = "启用此规则",
                Location = new Point(20, 240),
                Size = new Size(100, 20),
                Checked = true
            };

            // 描述
            var descriptionLabel = new Label
            {
                Text = "描述:",
                Location = new Point(20, 270),
                Size = new Size(50, 20)
            };

            _descriptionTextBox = new TextBox
            {
                Location = new Point(80, 268),
                Size = new Size(330, 23),
                PlaceholderText = "可选的规则描述信息"
            };

            // 按钮
            _okButton = new Button
            {
                Text = "确定",
                Location = new Point(250, 320),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK
            };

            _cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(335, 320),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel
            };

            // 添加控件到窗体
            Controls.AddRange(new Control[]
            {
                nameLabel, _nameTextBox,
                aSourceGroupBox, cTargetGroupBox,
                _isEnabledCheckBox,
                descriptionLabel, _descriptionTextBox,
                _okButton, _cancelButton
            });

            // 设置事件处理
            _okButton.Click += OkButton_Click;
            _nameTextBox.TextChanged += ValidateInput;
            _aSourceIpTextBox.TextChanged += ValidateInput;
            _cTargetIpTextBox.TextChanged += ValidateInput;

            ResumeLayout(false);
        }

        private void SetupNewRule()
        {
            Text = "添加路由规则";
            _nameTextBox.Text = $"规则_{DateTime.Now:MMddHHmm}";
        }

        private void LoadExistingRule(RouteRule rule)
        {
            Text = "编辑路由规则";
            _nameTextBox.Text = rule.Name;
            _aSourceIpTextBox.Text = rule.ASourceIp;
            _aSourcePortNumeric.Value = rule.ASourcePort;
            _cTargetIpTextBox.Text = rule.CTargetIp;
            _cTargetPortNumeric.Value = rule.CTargetPort;
            _isEnabledCheckBox.Checked = rule.IsEnabled;
            _descriptionTextBox.Text = rule.Description;

            RouteRule = rule; // 保存原始规则用于编辑
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            if (!ValidateAllInputs())
            {
                return;
            }

            try
            {
                // 创建或更新路由规则
                if (RouteRule == null)
                {
                    // 创建新规则
                    RouteRule = new RouteRule();
                }

                RouteRule.Name = _nameTextBox.Text.Trim();
                RouteRule.ASourceIp = _aSourceIpTextBox.Text.Trim();
                RouteRule.ASourcePort = (int)_aSourcePortNumeric.Value;
                RouteRule.CTargetIp = _cTargetIpTextBox.Text.Trim();
                RouteRule.CTargetPort = (int)_cTargetPortNumeric.Value;
                RouteRule.IsEnabled = _isEnabledCheckBox.Checked;
                RouteRule.Description = _descriptionTextBox.Text.Trim();
                RouteRule.LastModified = DateTime.Now;

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存规则时发生错误: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ValidateInput(object? sender, EventArgs e)
        {
            _okButton.Enabled = ValidateAllInputs();
        }

        private bool ValidateAllInputs()
        {
            // 验证规则名称
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                return false;
            }

            // 验证A方IP地址
            if (!IsValidIpAddress(_aSourceIpTextBox.Text))
            {
                return false;
            }

            // 验证C方IP地址
            if (!IsValidIpAddress(_cTargetIpTextBox.Text))
            {
                return false;
            }

            return true;
        }

        private bool IsValidIpAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return false;
            }

            return IPAddress.TryParse(ipAddress.Trim(), out _);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _nameTextBox.Focus();
            _nameTextBox.SelectAll();
        }
    }
}
