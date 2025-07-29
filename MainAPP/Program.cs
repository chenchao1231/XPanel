using System.Runtime.InteropServices;
using XPanel.Forms;
namespace XPanel
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 分配控制台窗口用于调试输出
            AllocConsole();
            Console.WriteLine("XPanel 调试控制台已启动");
            Console.WriteLine("插件加载和调试信息将在此显示");
            Console.WriteLine("========================================");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            // 释放控制台窗口
            FreeConsole();
        }
    }
}