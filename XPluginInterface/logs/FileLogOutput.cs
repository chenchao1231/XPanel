using System;
using System.IO;
using System.Threading;

namespace XPlugin.logs
{
    public class FileLogOutput : ILogOutput
    {
        private readonly string _logDirectory;
        private readonly ReaderWriterLockSlim _lock = new();

        public FileLogOutput(string? logDir = null)
        {
            _logDirectory = logDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);
        }

        public void AppendLog(string message)
        {
            try
            {
                var time = DateTime.Now;
                var fileName = Path.Combine(_logDirectory, $"{time:yyyy-MM-dd}.log");

                _lock.EnterWriteLock();
                File.AppendAllText(fileName, message + Environment.NewLine);
            }
            catch
            {
                // 可选：写入失败处理
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
