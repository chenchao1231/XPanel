using XPlugin.logs;
using System.Collections.Generic;

namespace XPlugin
{
    /// <summary>
    /// 服务插件接口
    /// </summary>
    public interface IServerPlugin
    {
        /// <summary>
        /// 插件名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 启动服务
        /// </summary>
        void Start();
        
        /// <summary>
        /// 启动服务（带参数）
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="output">日志输出</param>
        void Start(string ip, int port, ILogOutput output);
        
        /// <summary>
        /// 停止服务
        /// </summary>
        void Stop();
        
        /// <summary>
        /// 初始化端口
        /// </summary>
        /// <param name="ports">端口列表</param>
        void InitializePorts(IEnumerable<int> ports);
    }
}
