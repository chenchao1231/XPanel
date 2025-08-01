using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace XPlugin.Network
{
    /// <summary>
    /// 网络服务接口 - 定义网络通信的标准规范
    /// </summary>
    public interface INetworkService : IDisposable
    {
        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 连接变化事件
        /// </summary>
        event EventHandler<ConnectionEventArgs>? ConnectionChanged;

        /// <summary>
        /// 数据传输事件
        /// </summary>
        event EventHandler<DataTransferEventArgs>? DataTransferred;

        /// <summary>
        /// 日志消息事件
        /// </summary>
        event EventHandler<string>? LogMessage;

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动是否成功</returns>
        Task<bool> StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止服务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止是否成功</returns>
        Task<bool> StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取连接统计信息
        /// </summary>
        /// <returns>连接统计</returns>
        NetworkStatistics GetStatistics();
    }

    /// <summary>
    /// TCP服务器接口
    /// </summary>
    public interface ITcpServer : INetworkService
    {
        /// <summary>
        /// 监听端点
        /// </summary>
        IPEndPoint? ListenEndPoint { get; }

        /// <summary>
        /// 最大连接数
        /// </summary>
        int MaxConnections { get; set; }

        /// <summary>
        /// 获取活动连接列表
        /// </summary>
        /// <returns>连接信息列表</returns>
        IEnumerable<IConnectionInfo> GetActiveConnections();

        /// <summary>
        /// 断开指定连接
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <returns>断开是否成功</returns>
        Task<bool> DisconnectAsync(string connectionId);
    }

    /// <summary>
    /// TCP客户端接口
    /// </summary>
    public interface ITcpClient : INetworkService
    {
        /// <summary>
        /// 远程端点
        /// </summary>
        IPEndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// 连接状态
        /// </summary>
        ConnectionState State { get; }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="endPoint">服务器端点</param>
        /// <param name="timeout">连接超时时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接是否成功</returns>
        Task<bool> ConnectAsync(IPEndPoint endPoint, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送是否成功</returns>
        Task<bool> SendAsync(byte[] data, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 数据中继服务接口
    /// </summary>
    public interface IDataRelayService : INetworkService
    {
        /// <summary>
        /// 添加中继规则
        /// </summary>
        /// <param name="rule">中继规则</param>
        /// <returns>添加是否成功</returns>
        Task<bool> AddRelayRuleAsync(IRelayRule rule);

        /// <summary>
        /// 移除中继规则
        /// </summary>
        /// <param name="ruleId">规则ID</param>
        /// <returns>移除是否成功</returns>
        Task<bool> RemoveRelayRuleAsync(string ruleId);

        /// <summary>
        /// 获取所有中继规则
        /// </summary>
        /// <returns>中继规则列表</returns>
        IEnumerable<IRelayRule> GetRelayRules();

        /// <summary>
        /// 获取中继统计信息
        /// </summary>
        /// <returns>中继统计</returns>
        RelayStatistics GetRelayStatistics();

        /// <summary>
        /// 检查规则是否处于活动状态
        /// </summary>
        /// <param name="ruleId">规则ID</param>
        /// <returns>规则是否活动</returns>
        bool IsRuleActive(string ruleId);

        /// <summary>
        /// 启动指定的中继规则
        /// </summary>
        /// <param name="rule">要启动的规则</param>
        /// <returns>启动是否成功</returns>
        Task<bool> StartRelayRuleAsync(IRelayRule rule);
    }

    /// <summary>
    /// 连接状态枚举
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>断开</summary>
        Disconnected,
        /// <summary>连接中</summary>
        Connecting,
        /// <summary>已连接</summary>
        Connected,
        /// <summary>断开中</summary>
        Disconnecting,
        /// <summary>错误</summary>
        Error
    }

    /// <summary>
    /// 连接信息接口
    /// </summary>
    public interface IConnectionInfo
    {
        /// <summary>连接ID</summary>
        string Id { get; }

        /// <summary>本地端点</summary>
        IPEndPoint? LocalEndPoint { get; }

        /// <summary>远程端点</summary>
        IPEndPoint? RemoteEndPoint { get; }

        /// <summary>连接状态</summary>
        ConnectionState State { get; }

        /// <summary>连接时间</summary>
        DateTime ConnectedTime { get; }

        /// <summary>最后活动时间</summary>
        DateTime LastActiveTime { get; }

        /// <summary>接收字节数</summary>
        long BytesReceived { get; }

        /// <summary>发送字节数</summary>
        long BytesSent { get; }
    }

    /// <summary>
    /// 中继规则接口
    /// </summary>
    public interface IRelayRule
    {
        /// <summary>规则ID</summary>
        string Id { get; }

        /// <summary>规则名称</summary>
        string Name { get; }

        /// <summary>是否启用</summary>
        bool IsEnabled { get; }

        /// <summary>源端点（A方）</summary>
        IPEndPoint SourceEndPoint { get; }

        /// <summary>目标端点（C方）</summary>
        IPEndPoint TargetEndPoint { get; }

        /// <summary>描述</summary>
        string Description { get; }

        /// <summary>验证规则是否有效</summary>
        bool IsValid();
    }

    /// <summary>
    /// 网络统计信息
    /// </summary>
    public class NetworkStatistics
    {
        /// <summary>总连接数</summary>
        public int TotalConnections { get; set; }

        /// <summary>活动连接数</summary>
        public int ActiveConnections { get; set; }

        /// <summary>总接收字节数</summary>
        public long TotalBytesReceived { get; set; }

        /// <summary>总发送字节数</summary>
        public long TotalBytesSent { get; set; }

        /// <summary>启动时间</summary>
        public DateTime StartTime { get; set; }

        /// <summary>运行时长</summary>
        public TimeSpan Uptime => DateTime.Now - StartTime;
    }

    /// <summary>
    /// 中继统计信息
    /// </summary>
    public class RelayStatistics : NetworkStatistics
    {
        /// <summary>活动规则数</summary>
        public int ActiveRules { get; set; }

        /// <summary>总规则数</summary>
        public int TotalRules { get; set; }

        /// <summary>转发消息数</summary>
        public long ForwardedMessages { get; set; }

        /// <summary>转发错误数</summary>
        public long ForwardErrors { get; set; }
    }

    /// <summary>
    /// 连接事件参数
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        /// <summary>连接信息</summary>
        public IConnectionInfo Connection { get; }

        /// <summary>事件类型</summary>
        public ConnectionEventType EventType { get; }

        /// <summary>事件消息</summary>
        public string Message { get; }

        public ConnectionEventArgs(IConnectionInfo connection, ConnectionEventType eventType, string message = "")
        {
            Connection = connection;
            EventType = eventType;
            Message = message;
        }
    }

    /// <summary>
    /// 数据传输事件参数
    /// </summary>
    public class DataTransferEventArgs : EventArgs
    {
        /// <summary>连接ID</summary>
        public string ConnectionId { get; }

        /// <summary>数据方向</summary>
        public DataDirection Direction { get; }

        /// <summary>数据长度</summary>
        public int DataLength { get; }

        /// <summary>数据内容（可选）</summary>
        public byte[]? Data { get; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; }

        public DataTransferEventArgs(string connectionId, DataDirection direction, int dataLength, byte[]? data = null)
        {
            ConnectionId = connectionId;
            Direction = direction;
            DataLength = dataLength;
            Data = data;
            Timestamp = DateTime.Now;
        }
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
        Timeout
    }

    /// <summary>
    /// 数据方向
    /// </summary>
    public enum DataDirection
    {
        /// <summary>接收</summary>
        Received,
        /// <summary>发送</summary>
        Sent
    }
}
