using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XPlugin.Auditing;
using XPlugin.Configuration;
using XPlugin.Network;
using XPlugin.Services;
using XPluginTcpRelay.Models;

namespace XPluginTcpRelay.Services
{
    /// <summary>
    /// 默认服务工厂实现
    /// </summary>
    public class DefaultServiceFactory : ServiceFactoryBase
    {
        /// <summary>
        /// 创建网络服务
        /// </summary>
        public override T CreateNetworkService<T>(params object[] parameters)
        {
            var serviceType = typeof(T);
            
            if (serviceType == typeof(IDataRelayService))
            {
                var auditService = TryGetService<IAuditService>(out var audit) ? audit : null;
                return (T)(object)new TcpRelayService(auditService);
            }
            
            if (serviceType == typeof(ITcpServer))
            {
                // TODO: 实现TCP服务器创建逻辑
                throw new NotImplementedException("TCP服务器创建尚未实现");
            }
            
            if (serviceType == typeof(ITcpClient))
            {
                // TODO: 实现TCP客户端创建逻辑
                throw new NotImplementedException("TCP客户端创建尚未实现");
            }
            
            throw new NotSupportedException($"不支持的网络服务类型: {serviceType.Name}");
        }

        /// <summary>
        /// 创建配置服务
        /// </summary>
        public override IConfigurationService<T> CreateConfigurationService<T>(string configPath, bool supportHotReload = true)
        {
            if (typeof(T) == typeof(RelayConfig))
            {
                return (IConfigurationService<T>)(object)new RelayConfigService(configPath, supportHotReload);
            }
            
            throw new NotSupportedException($"不支持的配置类型: {typeof(T).Name}");
        }

        /// <summary>
        /// 创建审计服务
        /// </summary>
        public override IAuditService CreateAuditService(AuditServiceOptions options)
        {
            return new FileAuditService(options);
        }
    }

    /// <summary>
    /// 基于文件的审计服务实现
    /// </summary>
    internal class FileAuditService : IAuditService
    {
        private readonly AuditServiceOptions _options;
        private readonly Queue<IAuditEntry> _auditQueue = new();
        private readonly object _lockObject = new();
        private bool _disposed = false;

        public event EventHandler<AuditLogEventArgs>? NewAuditLog;

        public bool IsEnabled { get; set; }
        public AuditLevel Level { get; set; }

        public FileAuditService(AuditServiceOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            IsEnabled = options.Enabled;
            Level = options.Level;
        }

        public async Task<bool> LogAsync(IAuditEntry entry)
        {
            if (!IsEnabled || entry == null || entry.Level < Level)
                return false;

            try
            {
                lock (_lockObject)
                {
                    _auditQueue.Enqueue(entry);
                    
                    // 限制队列大小
                    while (_auditQueue.Count > _options.BufferSize)
                    {
                        _auditQueue.Dequeue();
                    }
                }

                NewAuditLog?.Invoke(this, new AuditLogEventArgs(entry));

                // 如果配置了日志文件，写入文件
                if (!string.IsNullOrEmpty(_options.LogFilePath))
                {
                    _ = Task.Run(async () => await WriteToFileAsync(entry));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> LogDataTransferAsync(string connectionId, XPlugin.Auditing.DataDirection direction, byte[] data, Dictionary<string, object>? metadata = null)
        {
            if (!_options.LogDataContent)
                return true;

            var entry = new AuditEntry(
                AuditLevel.Info,
                AuditType.DataTransfer,
                $"数据传输: {direction}",
                connectionId,
                $"数据长度: {data.Length} 字节",
                metadata,
                _options.LogDataContent ? data.Take(_options.MaxDataLength).ToArray() : null
            );

            return await LogAsync(entry);
        }

        public async Task<bool> LogConnectionEventAsync(string connectionId, XPlugin.Auditing.ConnectionEventType eventType, string details, Dictionary<string, object>? metadata = null)
        {
            var entry = new AuditEntry(
                AuditLevel.Info,
                AuditType.ConnectionEvent,
                $"连接事件: {eventType}",
                connectionId,
                details,
                metadata
            );

            return await LogAsync(entry);
        }

        public async Task<bool> LogSystemEventAsync(SystemEventType eventType, string message, Dictionary<string, object>? metadata = null)
        {
            var level = eventType == SystemEventType.ErrorOccurred ? AuditLevel.Error : AuditLevel.Info;
            
            var entry = new AuditEntry(
                level,
                AuditType.SystemEvent,
                $"系统事件: {eventType}",
                null,
                message,
                metadata
            );

            return await LogAsync(entry);
        }

        public async Task<IEnumerable<IAuditEntry>> QueryAsync(AuditQueryCriteria criteria)
        {
            // 简单实现：从内存队列查询
            IEnumerable<IAuditEntry> result;

            lock (_lockObject)
            {
                var query = _auditQueue.AsEnumerable();

                if (criteria.StartTime.HasValue)
                    query = query.Where(e => e.Timestamp >= criteria.StartTime.Value);

                if (criteria.EndTime.HasValue)
                    query = query.Where(e => e.Timestamp <= criteria.EndTime.Value);

                if (criteria.Level.HasValue)
                    query = query.Where(e => e.Level >= criteria.Level.Value);

                if (criteria.Type.HasValue)
                    query = query.Where(e => e.Type == criteria.Type.Value);

                if (!string.IsNullOrEmpty(criteria.ConnectionId))
                    query = query.Where(e => e.ConnectionId == criteria.ConnectionId);

                if (!string.IsNullOrEmpty(criteria.Keyword))
                    query = query.Where(e => e.Message.Contains(criteria.Keyword) ||
                                           (e.Details?.Contains(criteria.Keyword) ?? false));

                if (criteria.MaxResults.HasValue)
                    query = query.Take(criteria.MaxResults.Value);

                result = query.ToList();
            }

            return await Task.FromResult(result);
        }

        public async Task<int> CleanupAsync(TimeSpan retentionPeriod)
        {
            var cutoffTime = DateTime.Now - retentionPeriod;
            var removedCount = 0;

            lock (_lockObject)
            {
                var itemsToRemove = _auditQueue.Where(e => e.Timestamp < cutoffTime).ToList();
                removedCount = itemsToRemove.Count;

                // 重建队列，移除过期项
                var remainingItems = _auditQueue.Where(e => e.Timestamp >= cutoffTime).ToArray();
                _auditQueue.Clear();
                foreach (var item in remainingItems)
                {
                    _auditQueue.Enqueue(item);
                }
            }

            return await Task.FromResult(removedCount);
        }

        public async Task<bool> ExportAsync(AuditQueryCriteria criteria, AuditExportFormat format, string filePath)
        {
            try
            {
                var entries = await QueryAsync(criteria);
                
                switch (format)
                {
                    case AuditExportFormat.Json:
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(entries, Newtonsoft.Json.Formatting.Indented);
                        await System.IO.File.WriteAllTextAsync(filePath, json);
                        break;
                        
                    case AuditExportFormat.Text:
                        var lines = entries.Select(e => $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] [{e.Level}] [{e.Type}] {e.Message}");
                        await System.IO.File.WriteAllLinesAsync(filePath, lines);
                        break;
                        
                    default:
                        throw new NotSupportedException($"不支持的导出格式: {format}");
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task WriteToFileAsync(IAuditEntry entry)
        {
            if (string.IsNullOrEmpty(_options.LogFilePath))
                return;

            try
            {
                var logLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] [{entry.Type}] {entry.Message}";
                if (!string.IsNullOrEmpty(entry.Details))
                    logLine += $" - {entry.Details}";

                await System.IO.File.AppendAllTextAsync(_options.LogFilePath, logLine + Environment.NewLine);
            }
            catch
            {
                // 忽略文件写入错误
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
