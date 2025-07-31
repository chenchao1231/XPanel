using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace XPluginTcpRelay.Utils
{
    /// <summary>
    /// 端口检测工具类
    /// </summary>
    public static class PortChecker
    {
        /// <summary>
        /// 检查端口是否被占用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="protocol">协议类型（TCP/UDP）</param>
        /// <returns>true表示端口被占用，false表示端口可用</returns>
        public static bool IsPortInUse(int port, ProtocolType protocol = ProtocolType.Tcp)
        {
            try
            {
                if (protocol == ProtocolType.Tcp)
                {
                    return IsPortInUseTcp(port);
                }
                else if (protocol == ProtocolType.Udp)
                {
                    return IsPortInUseUdp(port);
                }
                return false;
            }
            catch
            {
                return true; // 检测异常时认为端口被占用
            }
        }

        /// <summary>
        /// 检查TCP端口是否被占用
        /// </summary>
        private static bool IsPortInUseTcp(int port)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (var endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == port)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查UDP端口是否被占用
        /// </summary>
        private static bool IsPortInUseUdp(int port)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var udpConnInfoArray = ipGlobalProperties.GetActiveUdpListeners();

            foreach (var endpoint in udpConnInfoArray)
            {
                if (endpoint.Port == port)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试绑定端口以检查是否可用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="ipAddress">IP地址，默认为任意地址</param>
        /// <returns>true表示端口可用，false表示端口被占用</returns>
        public static bool TryBindPort(int port, IPAddress? ipAddress = null)
        {
            ipAddress ??= IPAddress.Any;
            
            try
            {
                using var listener = new TcpListener(ipAddress, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取端口状态描述
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="protocol">协议类型</param>
        /// <returns>端口状态描述</returns>
        public static string GetPortStatusDescription(int port, ProtocolType protocol = ProtocolType.Tcp)
        {
            if (IsPortInUse(port, protocol))
            {
                return $"端口 {port} 已被占用";
            }
            else
            {
                return $"端口 {port} 可用";
            }
        }

        /// <summary>
        /// 查找可用端口
        /// </summary>
        /// <param name="startPort">起始端口</param>
        /// <param name="endPort">结束端口</param>
        /// <param name="protocol">协议类型</param>
        /// <returns>可用端口号，如果没有找到返回-1</returns>
        public static int FindAvailablePort(int startPort = 1024, int endPort = 65535, ProtocolType protocol = ProtocolType.Tcp)
        {
            for (int port = startPort; port <= endPort; port++)
            {
                if (!IsPortInUse(port, protocol))
                {
                    return port;
                }
            }
            return -1;
        }
    }
}
