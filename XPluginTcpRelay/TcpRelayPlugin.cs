using System;
using System.Drawing;
using System.Windows.Forms;
using XPlugin;
using XPluginTcpRelay.UI;

namespace XPluginTcpRelay
{
    /// <summary>
    /// TCP数据转发系统中继平台插件
    /// </summary>
    public class TcpRelayPlugin : IXPanelInterface
    {
        private static RelayControlPanel? _currentPanel;

        /// <summary>
        /// 插件名称
        /// </summary>
        public string Name => "TCP数据转发系统";

        /// <summary>
        /// 创建插件面板
        /// </summary>
        public UserControl CreatePanel()
        {
            try
            {
                // 如果已有面板实例，先清理
                if (_currentPanel != null)
                {
                    _currentPanel.Dispose();
                    _currentPanel = null;
                }

                _currentPanel = new RelayControlPanel();
                return _currentPanel;
            }
            catch (Exception ex)
            {
                // 如果创建面板失败，返回错误信息面板
                return CreateErrorPanel(ex);
            }
        }

        /// <summary>
        /// 创建错误信息面板
        /// </summary>
        private UserControl CreateErrorPanel(Exception ex)
        {
            var errorPanel = new UserControl
            {
                Size = new Size(1000, 700),
                BackColor = Color.LightPink
            };

            var titleLabel = new Label
            {
                Text = "TCP数据转发系统插件加载失败",
                Font = new Font("Microsoft YaHei", 16F, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(400, 30),
                ForeColor = Color.DarkRed
            };

            var errorLabel = new Label
            {
                Text = $"错误详情：\n{ex.Message}\n\n堆栈跟踪：\n{ex.StackTrace}",
                Location = new Point(20, 60),
                Size = new Size(950, 300),
                Font = new Font("Microsoft YaHei", 10F),
                ForeColor = Color.Red
            };

            var instructionLabel = new Label
            {
                Text = "解决方案：\n" +
                       "1. 检查是否安装了所需的依赖包（TouchSocket、Newtonsoft.Json）\n" +
                       "2. 确保.NET 8.0运行时已正确安装\n" +
                       "3. 检查插件文件是否完整\n" +
                       "4. 查看应用程序日志获取更多信息\n" +
                       "5. 重新编译并部署插件",
                Location = new Point(20, 380),
                Size = new Size(950, 150),
                Font = new Font("Microsoft YaHei", 10F),
                ForeColor = Color.DarkBlue
            };

            var retryButton = new Button
            {
                Text = "重试加载",
                Location = new Point(20, 550),
                Size = new Size(100, 30),
                BackColor = Color.LightBlue
            };

            retryButton.Click += (s, e) =>
            {
                try
                {
                    var newPanel = new RelayControlPanel();
                    errorPanel.Parent?.Controls.Remove(errorPanel);
                    errorPanel.Parent?.Controls.Add(newPanel);
                    newPanel.Dock = DockStyle.Fill;
                }
                catch (Exception retryEx)
                {
                    MessageBox.Show($"重试失败: {retryEx.Message}", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            errorPanel.Controls.AddRange(new Control[]
            {
                titleLabel, errorLabel, instructionLabel, retryButton
            });

            return errorPanel;
        }
    }
}
