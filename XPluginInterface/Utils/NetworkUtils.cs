using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XPlugin.Utils
{
    /// <summary>
    /// 网络工具类 - 提供网络相关的通用功能
    /// </summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// 验证IP地址格式是否有效
        /// </summary>
        /// <param name="ipAddress">IP地址字符串</param>
        /// <returns>true表示有效</returns>
        public static bool IsValidIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            return IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// 验证端口号是否有效
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>true表示有效</returns>
        public static bool IsValidPort(int port)
        {
            return port > 0 && port <= 65535;
        }

        /// <summary>
        /// 验证端点是否有效
        /// </summary>
        /// <param name="endPoint">端点</param>
        /// <returns>true表示有效</returns>
        public static bool IsValidEndPoint(IPEndPoint? endPoint)
        {
            return endPoint != null && IsValidPort(endPoint.Port);
        }

        /// <summary>
        /// 解析端点字符串
        /// </summary>
        /// <param name="endPointString">端点字符串，格式：IP:Port</param>
        /// <returns>解析的端点，失败返回null</returns>
        public static IPEndPoint? ParseEndPoint(string endPointString)
        {
            if (string.IsNullOrWhiteSpace(endPointString))
                return null;

            var parts = endPointString.Split(':');
            if (parts.Length != 2)
                return null;

            if (!IPAddress.TryParse(parts[0], out var ip))
                return null;

            if (!int.TryParse(parts[1], out var port) || !IsValidPort(port))
                return null;

            return new IPEndPoint(ip, port);
        }

        /// <summary>
        /// 检查端口是否被占用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="protocol">协议类型</param>
        /// <returns>true表示被占用</returns>
        public static bool IsPortInUse(int port, ProtocolType protocol = ProtocolType.Tcp)
        {
            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                
                if (protocol == ProtocolType.Tcp)
                {
                    var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
                    foreach (var endpoint in tcpConnInfoArray)
                    {
                        if (endpoint.Port == port)
                            return true;
                    }
                }
                else if (protocol == ProtocolType.Udp)
                {
                    var udpConnInfoArray = ipGlobalProperties.GetActiveUdpListeners();
                    foreach (var endpoint in udpConnInfoArray)
                    {
                        if (endpoint.Port == port)
                            return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 测试TCP连接
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <param name="port">端口号</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>连接测试结果</returns>
        public static async Task<ConnectionTestResult> TestTcpConnectionAsync(string host, int port, int timeout = 5000)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return new ConnectionTestResult(false, "连接超时");
                }

                if (connectTask.IsFaulted)
                {
                    return new ConnectionTestResult(false, connectTask.Exception?.GetBaseException().Message ?? "连接失败");
                }

                return new ConnectionTestResult(true, "连接成功");
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult(false, ex.Message);
            }
        }

        /// <summary>
        /// 获取本机IP地址列表
        /// </summary>
        /// <returns>IP地址列表</returns>
        public static IPAddress[] GetLocalIPAddresses()
        {
            try
            {
                var hostName = Dns.GetHostName();
                var hostEntry = Dns.GetHostEntry(hostName);
                return hostEntry.AddressList;
            }
            catch
            {
                return Array.Empty<IPAddress>();
            }
        }

        /// <summary>
        /// 获取可用的端口号
        /// </summary>
        /// <param name="startPort">起始端口</param>
        /// <param name="endPort">结束端口</param>
        /// <returns>可用端口号，没有可用端口返回-1</returns>
        public static int GetAvailablePort(int startPort = 1024, int endPort = 65535)
        {
            for (int port = startPort; port <= endPort; port++)
            {
                if (!IsPortInUse(port))
                    return port;
            }
            return -1;
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
        /// 将字节数组转换为十六进制字符串
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <param name="separator">分隔符</param>
        /// <returns>十六进制字符串</returns>
        public static string BytesToHex(byte[] data, string separator = " ")
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            return BitConverter.ToString(data).Replace("-", separator);
        }

        /// <summary>
        /// 将字节数组转换为ASCII字符串（不可打印字符用.替代）
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <returns>ASCII字符串</returns>
        public static string BytesToAscii(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (byte b in data)
            {
                if (b >= 32 && b <= 126) // 可打印ASCII字符
                    sb.Append((char)b);
                else
                    sb.Append('.');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 格式化网络数据为Hex+ASCII格式
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="maxLength">最大显示长度</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatNetworkData(byte[] data, int maxLength = 64)
        {
            if (data == null || data.Length == 0)
                return "[空数据]";

            var displayData = data.Length > maxLength ? data[..maxLength] : data;
            var hex = BytesToHex(displayData);
            var ascii = BytesToAscii(displayData);
            var truncated = data.Length > maxLength ? "..." : "";

            return $"Hex: {hex}{truncated} | ASCII: {ascii}{truncated}";
        }

        /// <summary>
        /// 生成唯一的连接ID
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <returns>连接ID</returns>
        public static string GenerateConnectionId(string prefix = "conn")
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = new Random().Next(1000, 9999);
            return $"{prefix}_{timestamp}_{random}";
        }

        /// <summary>
        /// 验证主机名或IP地址格式
        /// </summary>
        /// <param name="hostOrIp">主机名或IP地址</param>
        /// <returns>true表示格式有效</returns>
        public static bool IsValidHostOrIP(string hostOrIp)
        {
            if (string.IsNullOrWhiteSpace(hostOrIp))
                return false;

            // 检查是否为有效IP地址
            if (IsValidIPAddress(hostOrIp))
                return true;

            // 检查是否为有效主机名
            return IsValidHostName(hostOrIp);
        }

        /// <summary>
        /// 验证主机名格式
        /// </summary>
        /// <param name="hostName">主机名</param>
        /// <returns>true表示格式有效</returns>
        public static bool IsValidHostName(string hostName)
        {
            if (string.IsNullOrWhiteSpace(hostName))
                return false;

            // 主机名长度限制
            if (hostName.Length > 253)
                return false;

            // 主机名格式验证（简化版）
            var hostNamePattern = @"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$";
            return Regex.IsMatch(hostName, hostNamePattern);
        }

        /// <summary>
        /// 计算网络延迟
        /// </summary>
        /// <param name="host">目标主机</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>延迟时间（毫秒），失败返回-1</returns>
        public static async Task<long> PingAsync(string host, int timeout = 5000)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, timeout);
                return reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>
    /// 连接测试结果
    /// </summary>
    public class ConnectionTestResult
    {
        /// <summary>是否成功</summary>
        public bool Success { get; }

        /// <summary>消息</summary>
        public string Message { get; }

        /// <summary>测试时间</summary>
        public DateTime TestTime { get; }

        public ConnectionTestResult(bool success, string message)
        {
            Success = success;
            Message = message;
            TestTime = DateTime.Now;
        }

        public override string ToString()
        {
            return $"{(Success ? "成功" : "失败")}: {Message}";
        }
    }
}
