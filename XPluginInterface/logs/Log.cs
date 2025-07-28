namespace XPlugin.logs
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Forms;

    /// <summary>
    /// 日志工具类 <see cref="Log" />
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// 日誌註冊容器
        /// </summary>
        private static readonly List<ILogOutput> _outputs = new();

        /// <summary>
        /// 註冊一个日志输出器（UI、文件、控制台等）
        /// </summary>
        /// <param name="output">The output<see cref="ILogOutput"/></param>
        public static void RegisterOutput(ILogOutput output)
        {
            if (output != null && !_outputs.Contains(output))
            {
                _outputs.Add(output);
            }
        }

        /// <summary>
        /// Info日誌
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        public static void Info(string message) => Write("INFO", message);

        /// <summary>
        /// Error日誌
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        public static void Error(string message) => Write("ERROR", message);

        /// <summary>
        /// Debug日誌
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        public static void Debug(string message) => Write("DEBUG", message);

        /// <summary>
        /// 便利日志容器所有的实例进行日志输出
        /// </summary>
        /// <param name="level">日志等级<see cref="string"/></param>
        /// <param name="message">日志消息<see cref="string"/></param>
        private static void Write(string level, string message)
        {
            var time = DateTime.Now;
            var line = $"[{time:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            foreach (var output in _outputs)
            {
                try
                {
                    output.AppendLog(line);
                }
                catch (Exception e)
                {
                    // 日志输出失败时输出到控制台
                    Console.WriteLine($"[LOG ERROR] {e.Message}");
                }
            }
        }
    }
}