using System;
using System.Drawing;
using System.Windows.Forms;
using XPlugin;

namespace XPluginSample
{
    /// <summary>
    /// 示例插件
    /// </summary>
    public class SamplePlugin : IXPanelInterface
    {
        public string Name => "示例插件";

        public UserControl CreatePanel()
        {
            return new SamplePanel();
        }
    }

    /// <summary>
    /// 示例面板
    /// </summary>
    public class SamplePanel : UserControl
    {
        public SamplePanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // 设置面板属性
            Size = new Size(600, 400);
            BackColor = Color.LightCyan;

            // 创建标题
            var titleLabel = new Label
            {
                Text = "这是一个示例插件面板",
                Font = new Font("Microsoft YaHei", 14F, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(300, 30),
                ForeColor = Color.DarkBlue
            };

            // 创建描述
            var descLabel = new Label
            {
                Text = "这个插件演示了如何创建XPanel插件：\n\n" +
                       "1. 实现 IXPanelInterface 接口\n" +
                       "2. 提供插件名称\n" +
                       "3. 创建用户控件面板\n" +
                       "4. 编译为 XPlugin*.dll 格式\n" +
                       "5. 通过插件管理器上传安装",
                Location = new Point(20, 60),
                Size = new Size(550, 150),
                Font = new Font("Microsoft YaHei", 10F),
                ForeColor = Color.DarkGreen
            };

            // 创建按钮
            var testButton = new Button
            {
                Text = "测试按钮",
                Location = new Point(20, 230),
                Size = new Size(100, 30),
                BackColor = Color.LightBlue,
                UseVisualStyleBackColor = false
            };
            testButton.Click += TestButton_Click;

            // 创建文本框
            var textBox = new TextBox
            {
                Location = new Point(140, 230),
                Size = new Size(200, 30),
                Text = "这是一个文本框"
            };

            // 添加控件
            Controls.AddRange(new Control[] { titleLabel, descLabel, testButton, textBox });

            ResumeLayout(false);
        }

        private void TestButton_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("示例插件按钮被点击了！", "插件消息", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
