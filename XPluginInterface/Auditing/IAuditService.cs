using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XPlugin.Auditing
{
    /// <summary>
    /// 审计服务接口 - 定义数据审计的标准规范
    /// </summary>
    public interface IAuditService : IDisposable
    {
        /// <summary>
        /// 新审计日志事件
        /// </summary>
        event EventHandler<AuditLogEventArgs>? NewAuditLog;

        /// <summary>
        /// 是否启用审计
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 审计级别
        /// </summary>
        AuditLevel Level { get; set; }

        /// <summary>
        /// 记录审计日志
        /// </summary>
        /// <param name="entry">审计条目</param>
        /// <returns>记录是否成功</returns>
        Task<bool> LogAsync(IAuditEntry entry);

        /// <summary>
        /// 记录数据传输审计
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <param name="direction">数据方向</param>
        /// <param name="data">数据内容</param>
        /// <param name="metadata">元数据</param>
        /// <returns>记录是否成功</returns>
        Task<bool> LogDataTransferAsync(string connectionId, DataDirection direction, byte[] data, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// 记录连接事件审计
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="details">事件详情</param>
        /// <param name="metadata">元数据</param>
        /// <returns>记录是否成功</returns>
        Task<bool> LogConnectionEventAsync(string connectionId, ConnectionEventType eventType, string details, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// 记录系统事件审计
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="message">事件消息</param>
        /// <param name="metadata">元数据</param>
        /// <returns>记录是否成功</returns>
        Task<bool> LogSystemEventAsync(SystemEventType eventType, string message, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// 查询审计日志
        /// </summary>
        /// <param name="criteria">查询条件</param>
        /// <returns>审计日志列表</returns>
        Task<IEnumerable<IAuditEntry>> QueryAsync(AuditQueryCriteria criteria);

        /// <summary>
        /// 清理过期的审计日志
        /// </summary>
        /// <param name="retentionPeriod">保留期间</param>
        /// <returns>清理的日志数量</returns>
        Task<int> CleanupAsync(TimeSpan retentionPeriod);

        /// <summary>
        /// 导出审计日志
        /// </summary>
        /// <param name="criteria">导出条件</param>
        /// <param name="format">导出格式</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>导出是否成功</returns>
        Task<bool> ExportAsync(AuditQueryCriteria criteria, AuditExportFormat format, string filePath);
    }

    /// <summary>
    /// 审计条目接口
    /// </summary>
    public interface IAuditEntry
    {
        /// <summary>条目ID</summary>
        string Id { get; }

        /// <summary>时间戳</summary>
        DateTime Timestamp { get; }

        /// <summary>审计级别</summary>
        AuditLevel Level { get; }

        /// <summary>审计类型</summary>
        AuditType Type { get; }

        /// <summary>连接ID</summary>
        string? ConnectionId { get; }

        /// <summary>消息</summary>
        string Message { get; }

        /// <summary>详细信息</summary>
        string? Details { get; }

        /// <summary>元数据</summary>
        IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>数据内容（可选）</summary>
        byte[]? Data { get; }
    }

    /// <summary>
    /// 审计级别
    /// </summary>
    public enum AuditLevel
    {
        /// <summary>调试</summary>
        Debug = 0,
        /// <summary>信息</summary>
        Info = 1,
        /// <summary>警告</summary>
        Warning = 2,
        /// <summary>错误</summary>
        Error = 3,
        /// <summary>严重</summary>
        Critical = 4
    }

    /// <summary>
    /// 审计类型
    /// </summary>
    public enum AuditType
    {
        /// <summary>数据传输</summary>
        DataTransfer,
        /// <summary>连接事件</summary>
        ConnectionEvent,
        /// <summary>系统事件</summary>
        SystemEvent,
        /// <summary>配置变更</summary>
        ConfigurationChange,
        /// <summary>用户操作</summary>
        UserAction
    }

    /// <summary>
    /// 数据方向
    /// </summary>
    public enum DataDirection
    {
        /// <summary>接收</summary>
        Received,
        /// <summary>发送</summary>
        Sent,
        /// <summary>转发</summary>
        Forwarded
    }

    /// <summary>
    /// 连接事件类型
    /// </summary>
    public enum ConnectionEventType
    {
        /// <summary>连接建立</summary>
        Connected,
        /// <summary>连接断开</summary>
        Disconnected,
        /// <summary>连接错误</summary>
        Error,
        /// <summary>连接超时</summary>
        Timeout,
        /// <summary>认证成功</summary>
        AuthenticationSuccess,
        /// <summary>认证失败</summary>
        AuthenticationFailure
    }

    /// <summary>
    /// 系统事件类型
    /// </summary>
    public enum SystemEventType
    {
        /// <summary>服务启动</summary>
        ServiceStarted,
        /// <summary>服务停止</summary>
        ServiceStopped,
        /// <summary>配置加载</summary>
        ConfigurationLoaded,
        /// <summary>配置保存</summary>
        ConfigurationSaved,
        /// <summary>错误发生</summary>
        ErrorOccurred,
        /// <summary>性能警告</summary>
        PerformanceWarning
    }

    /// <summary>
    /// 审计查询条件
    /// </summary>
    public class AuditQueryCriteria
    {
        /// <summary>开始时间</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>结束时间</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>审计级别</summary>
        public AuditLevel? Level { get; set; }

        /// <summary>审计类型</summary>
        public AuditType? Type { get; set; }

        /// <summary>连接ID</summary>
        public string? ConnectionId { get; set; }

        /// <summary>关键词搜索</summary>
        public string? Keyword { get; set; }

        /// <summary>最大返回数量</summary>
        public int? MaxResults { get; set; }

        /// <summary>跳过数量</summary>
        public int? Skip { get; set; }

        /// <summary>排序字段</summary>
        public string? OrderBy { get; set; }

        /// <summary>是否降序</summary>
        public bool Descending { get; set; } = true;
    }

    /// <summary>
    /// 审计导出格式
    /// </summary>
    public enum AuditExportFormat
    {
        /// <summary>JSON格式</summary>
        Json,
        /// <summary>CSV格式</summary>
        Csv,
        /// <summary>XML格式</summary>
        Xml,
        /// <summary>文本格式</summary>
        Text
    }

    /// <summary>
    /// 审计日志事件参数
    /// </summary>
    public class AuditLogEventArgs : EventArgs
    {
        /// <summary>审计条目</summary>
        public IAuditEntry Entry { get; }

        public AuditLogEventArgs(IAuditEntry entry)
        {
            Entry = entry;
        }
    }

    /// <summary>
    /// 审计条目实现
    /// </summary>
    public class AuditEntry : IAuditEntry
    {
        public string Id { get; }
        public DateTime Timestamp { get; }
        public AuditLevel Level { get; }
        public AuditType Type { get; }
        public string? ConnectionId { get; }
        public string Message { get; }
        public string? Details { get; }
        public IReadOnlyDictionary<string, object> Metadata { get; }
        public byte[]? Data { get; }

        public AuditEntry(
            AuditLevel level,
            AuditType type,
            string message,
            string? connectionId = null,
            string? details = null,
            Dictionary<string, object>? metadata = null,
            byte[]? data = null)
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
            Level = level;
            Type = type;
            ConnectionId = connectionId;
            Message = message;
            Details = details;
            Metadata = metadata?.AsReadOnly() ?? new Dictionary<string, object>().AsReadOnly();
            Data = data;
        }
    }
}
