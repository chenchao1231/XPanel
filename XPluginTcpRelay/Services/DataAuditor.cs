using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace XPluginTcpRelay.Services
{
    /// <summary>
    /// 数据审计服务 - 负责记录和显示转发的数据
    /// </summary>
    public class DataAuditor : IDisposable
    {
        private readonly ConcurrentQueue<AuditLogEntry> _logQueue;
        private readonly string _logDirectory;
        private readonly object _fileLock = new object();
        private bool _isEnabled = true;

        /// <summary>
        /// 新的审计日志事件
        /// </summary>
        public event Action<AuditLogEntry>? NewAuditLog;

        public DataAuditor()
        {
            _logQueue = new ConcurrentQueue<AuditLogEntry>();
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "DataAudit");
            
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 启动后台日志写入任务
            Task.Run(ProcessLogQueueAsync);
        }

        /// <summary>
        /// 是否启用审计
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// 记录数据转发
        /// </summary>
        public void LogDataForward(string direction, byte[] data, string connectionId, string? routeRuleId = null)
        {
            if (!_isEnabled || data == null || data.Length == 0)
                return;

            var logEntry = new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                Direction = direction,
                ConnectionId = connectionId,
                RouteRuleId = routeRuleId,
                DataLength = data.Length,
                HexData = BytesToHex(data),
                AsciiData = BytesToAscii(data)
            };

            _logQueue.Enqueue(logEntry);
            NewAuditLog?.Invoke(logEntry);
        }

        /// <summary>
        /// 记录连接事件
        /// </summary>
        public void LogConnectionEvent(string eventType, string connectionId, string details)
        {
            if (!_isEnabled)
                return;

            var logEntry = new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                Direction = "EVENT",
                ConnectionId = connectionId,
                EventType = eventType,
                Details = details,
                DataLength = 0
            };

            _logQueue.Enqueue(logEntry);
            NewAuditLog?.Invoke(logEntry);
        }

        /// <summary>
        /// 获取今日日志文件路径
        /// </summary>
        private string GetTodayLogFilePath()
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(_logDirectory, $"audit_log_{today}.txt");
        }

        /// <summary>
        /// 处理日志队列的后台任务
        /// </summary>
        private async Task ProcessLogQueueAsync()
        {
            while (true)
            {
                try
                {
                    if (_logQueue.TryDequeue(out var logEntry))
                    {
                        await WriteLogToFileAsync(logEntry);
                    }
                    else
                    {
                        await Task.Delay(100); // 没有日志时等待100ms
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误但不中断日志处理
                    Console.WriteLine($"处理审计日志时发生错误: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }

        /// <summary>
        /// 将日志写入文件
        /// </summary>
        private async Task WriteLogToFileAsync(AuditLogEntry logEntry)
        {
            try
            {
                var logFilePath = GetTodayLogFilePath();
                var logLine = FormatLogEntry(logEntry);

                lock (_fileLock)
                {
                    File.AppendAllText(logFilePath, logLine + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入审计日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化日志条目
        /// </summary>
        private string FormatLogEntry(AuditLogEntry logEntry)
        {
            var sb = new StringBuilder();
            
            sb.Append($"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}]");
            sb.Append($"[{logEntry.Direction}]");
            sb.Append($"[{logEntry.ConnectionId}]");

            if (!string.IsNullOrEmpty(logEntry.EventType))
            {
                sb.Append($"[{logEntry.EventType}] {logEntry.Details}");
            }
            else if (logEntry.DataLength > 0)
            {
                sb.Append($" Length:{logEntry.DataLength}");
                
                if (!string.IsNullOrEmpty(logEntry.HexData))
                {
                    sb.Append($" Hex:{logEntry.HexData}");
                }
                
                if (!string.IsNullOrEmpty(logEntry.AsciiData))
                {
                    sb.Append($" ASCII:{logEntry.AsciiData}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 字节数组转十六进制字符串
        /// </summary>
        private string BytesToHex(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            // 限制显示的数据长度，避免日志过长
            var maxLength = Math.Min(data.Length, 64);
            var sb = new StringBuilder(maxLength * 3);

            for (int i = 0; i < maxLength; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }

            if (data.Length > maxLength)
            {
                sb.Append("...");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 字节数组转ASCII字符串
        /// </summary>
        private string BytesToAscii(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            // 限制显示的数据长度
            var maxLength = Math.Min(data.Length, 64);
            var sb = new StringBuilder(maxLength);

            for (int i = 0; i < maxLength; i++)
            {
                var b = data[i];
                // 只显示可打印的ASCII字符，其他用'.'代替
                sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }

            if (data.Length > maxLength)
            {
                sb.Append("...");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 清理旧日志文件
        /// </summary>
        public void CleanupOldLogs(int keepDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-keepDays);
                var files = Directory.GetFiles(_logDirectory, "audit_log_*.txt");

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理旧日志文件失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _isEnabled = false;
            // 等待队列处理完成
            while (!_logQueue.IsEmpty)
            {
                Task.Delay(100).Wait();
            }
        }
    }

    /// <summary>
    /// 审计日志条目
    /// </summary>
    public class AuditLogEntry
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 方向（A→C, C→A, EVENT）
        /// </summary>
        public string Direction { get; set; } = string.Empty;

        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// 路由规则ID
        /// </summary>
        public string? RouteRuleId { get; set; }

        /// <summary>
        /// 事件类型（用于非数据事件）
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// 详细信息
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// 数据长度
        /// </summary>
        public int DataLength { get; set; }

        /// <summary>
        /// 十六进制数据
        /// </summary>
        public string? HexData { get; set; }

        /// <summary>
        /// ASCII数据
        /// </summary>
        public string? AsciiData { get; set; }

        /// <summary>
        /// 获取显示文本
        /// </summary>
        public string GetDisplayText()
        {
            if (!string.IsNullOrEmpty(EventType))
            {
                return $"[{Timestamp:HH:mm:ss}][{Direction}] {EventType}: {Details}";
            }
            else
            {
                return $"[{Timestamp:HH:mm:ss}][{Direction}] {DataLength}字节 {HexData} {AsciiData}";
            }
        }
    }
}
