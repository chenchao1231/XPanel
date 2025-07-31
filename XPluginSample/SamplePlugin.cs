using System;
using System.Drawing;
using System.Windows.Forms;
using XPlugin;
using XPluginSample.UI;

namespace XPluginSample
{
    /// <summary>
    /// 示例插件 - 展示标准插件开发规范
    /// </summary>
    public class SamplePlugin : IXPanelInterface
    {
        private static SampleMainPanel? _currentPanel;

        public string Name => "示例插件";

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

                _currentPanel = new SampleMainPanel();
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
        private static UserControl CreateErrorPanel(Exception ex)
        {
            var errorPanel = new UserControl
            {
                Size = new Size(800, 600),
                BackColor = Color.LightPink
            };

            var errorLabel = new Label
            {
                Text = $"示例插件加载失败：\n{ex.Message}\n\n堆栈跟踪：\n{ex.StackTrace}",
                Location = new Point(20, 20),
                Size = new Size(750, 500),
                Font = new Font("Microsoft YaHei", 10F),
                ForeColor = Color.Red,
                AutoSize = false
            };

            errorPanel.Controls.Add(errorLabel);
            return errorPanel;
        }

        /// <summary>
        /// 清理资源（当插件被卸载时调用）
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (_currentPanel != null)
                {
                    System.Diagnostics.Debug.WriteLine("开始清理示例插件静态资源...");
                    _currentPanel.Dispose();
                    _currentPanel = null;
                    System.Diagnostics.Debug.WriteLine("示例插件静态资源清理完成");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理示例插件资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 当面板被外部释放时调用此方法清理静态引用
        /// </summary>
        internal static void OnPanelDisposed(SampleMainPanel panel)
        {
            try
            {
                if (_currentPanel == panel)
                {
                    System.Diagnostics.Debug.WriteLine("示例插件面板被外部释放，清理静态引用");
                    _currentPanel = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理示例插件面板静态引用失败: {ex.Message}");
            }
        }
    }
}
