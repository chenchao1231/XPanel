using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XPluginTcpServer.Utils
{
    /// <summary>
    /// 网络辅助工具类
    /// </summary>
    public static class NetworkHelper
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
        /// 异步检查端口是否可用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="ipAddress">IP地址</param>
        /// <returns>true表示端口可用</returns>
        public static async Task<bool> TryBindPortAsync(int port, IPAddress? ipAddress = null)
        {
            return await Task.Run(() => TryBindPort(port, ipAddress));
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

        /// <summary>
        /// 验证IP地址格式
        /// </summary>
        /// <param name="ipAddress">IP地址字符串</param>
        /// <returns>true表示格式正确</returns>
        public static bool IsValidIpAddress(string ipAddress)
        {
            return IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// 验证端口号范围
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>true表示端口号有效</returns>
        public static bool IsValidPort(int port)
        {
            return port >= 1 && port <= 65535;
        }

        /// <summary>
        /// 获取本机IP地址列表
        /// </summary>
        /// <returns>IP地址数组</returns>
        public static IPAddress[] GetLocalIpAddresses()
        {
            try
            {
                var hostName = Dns.GetHostName();
                var hostEntry = Dns.GetHostEntry(hostName);
                return hostEntry.AddressList;
            }
            catch
            {
                return new[] { IPAddress.Loopback };
            }
        }

        /// <summary>
        /// 获取本机IPv4地址列表
        /// </summary>
        /// <returns>IPv4地址数组</returns>
        public static IPAddress[] GetLocalIPv4Addresses()
        {
            try
            {
                var allAddresses = GetLocalIpAddresses();
                var ipv4Addresses = new System.Collections.Generic.List<IPAddress>();

                foreach (var address in allAddresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4Addresses.Add(address);
                    }
                }

                return ipv4Addresses.ToArray();
            }
            catch
            {
                return new[] { IPAddress.Loopback };
            }
        }

        /// <summary>
        /// 测试网络连接
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口号</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>true表示连接成功</returns>
        public static async Task<bool> TestConnectionAsync(string host, int port, int timeout = 5000)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == connectTask && client.Connected)
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化字节数为可读字符串
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }

        /// <summary>
        /// 格式化网络速度
        /// </summary>
        /// <param name="bytesPerSecond">每秒字节数</param>
        /// <returns>格式化后的速度字符串</returns>
        public static string FormatSpeed(long bytesPerSecond)
        {
            return $"{FormatBytes(bytesPerSecond)}/s";
        }

        /// <summary>
        /// 生成唯一的连接ID
        /// </summary>
        /// <param name="remoteEndPoint">远程端点</param>
        /// <returns>连接ID</returns>
        public static string GenerateConnectionId(EndPoint? remoteEndPoint)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var endpointStr = remoteEndPoint?.ToString() ?? "unknown";
            return $"{endpointStr}_{timestamp}";
        }

        /// <summary>
        /// 解析端点字符串
        /// </summary>
        /// <param name="endpointString">端点字符串（格式：IP:Port）</param>
        /// <returns>IPEndPoint对象，解析失败返回null</returns>
        public static IPEndPoint? ParseEndPoint(string endpointString)
        {
            try
            {
                var parts = endpointString.Split(':');
                if (parts.Length == 2 && 
                    IPAddress.TryParse(parts[0], out var ip) && 
                    int.TryParse(parts[1], out var port) &&
                    IsValidPort(port))
                {
                    return new IPEndPoint(ip, port);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
