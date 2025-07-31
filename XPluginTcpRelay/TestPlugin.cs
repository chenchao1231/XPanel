using System;
using System.Windows.Forms;
using XPluginTcpRelay;

namespace XPluginTcpRelay.Test
{
    /// <summary>
    /// 插件测试程序
    /// </summary>
    public class TestPlugin
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // 创建插件实例
                var plugin = new TcpRelayPlugin();
                Console.WriteLine($"插件名称: {plugin.Name}");

                // 创建插件面板
                var panel = plugin.CreatePanel();
                Console.WriteLine($"插件面板类型: {panel.GetType().Name}");

                // 创建测试窗体
                var form = new Form
                {
                    Text = "TCP数据转发系统测试",
                    Size = new System.Drawing.Size(1200, 800),
                    StartPosition = FormStartPosition.CenterScreen
                };

                // 将插件面板添加到窗体
                panel.Dock = DockStyle.Fill;
                form.Controls.Add(panel);

                Console.WriteLine("插件测试窗体已创建，准备显示...");

                // 显示窗体
                Application.Run(form);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试过程中发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show($"测试失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
