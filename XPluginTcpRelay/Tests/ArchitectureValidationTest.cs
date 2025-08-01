using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XPluginTcpRelay.Services;
using XPluginTcpRelay.Models;

namespace XPluginTcpRelay.Tests
{
    /// <summary>
    /// 架构验证测试 - 验证修复后的功能
    /// </summary>
    public class ArchitectureValidationTest
    {
        private TcpListener? _mockDataSource;
        private TcpRelayService? _relayService;
        private TcpClient? _mockConsumer;
        private bool _isRunning = false;

        /// <summary>
        /// 运行完整的架构验证测试
        /// </summary>
        public async Task<bool> RunValidationTestAsync()
        {
            Console.WriteLine("🚀 开始XPluginTcpRelay架构验证测试...");
            Console.WriteLine();

            try
            {
                // 1. 测试独立数据源连接管理
                Console.WriteLine("📋 测试1: 独立数据源连接管理");
                var test1Result = await TestIndependentDataSourceConnection();
                Console.WriteLine($"   结果: {(test1Result ? "✅ 通过" : "❌ 失败")}");
                Console.WriteLine();

                // 2. 测试连接解耦
                Console.WriteLine("📋 测试2: 连接解耦验证");
                var test2Result = await TestConnectionDecoupling();
                Console.WriteLine($"   结果: {(test2Result ? "✅ 通过" : "❌ 失败")}");
                Console.WriteLine();

                // 3. 测试持续重连机制
                Console.WriteLine("📋 测试3: 持续重连机制");
                var test3Result = await TestContinuousReconnection();
                Console.WriteLine($"   结果: {(test3Result ? "✅ 通过" : "❌ 失败")}");
                Console.WriteLine();

                // 4. 测试数据源连接状态显示
                Console.WriteLine("📋 测试4: 数据源连接状态显示");
                var test4Result = await TestDataSourceStatusDisplay();
                Console.WriteLine($"   结果: {(test4Result ? "✅ 通过" : "❌ 失败")}");
                Console.WriteLine();

                var overallResult = test1Result && test2Result && test3Result && test4Result;
                Console.WriteLine($"🎯 总体测试结果: {(overallResult ? "✅ 全部通过" : "❌ 部分失败")}");
                
                return overallResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 测试过程中发生异常: {ex.Message}");
                return false;
            }
            finally
            {
                await CleanupAsync();
            }
        }

        /// <summary>
        /// 测试1: 独立数据源连接管理
        /// </summary>
        private async Task<bool> TestIndependentDataSourceConnection()
        {
            try
            {
                // 启动模拟数据源
                _mockDataSource = new TcpListener(IPAddress.Loopback, 18080);
                _mockDataSource.Start();
                Console.WriteLine("   - 模拟数据源已启动 (127.0.0.1:18080)");

                // 创建路由规则
                var rule = new RouteRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "测试规则1",
                    DataSourceIp = "127.0.0.1",
                    DataSourcePort = 18080,
                    LocalServerPort = 19999,
                    IsEnabled = true,
                    Description = "架构验证测试规则"
                };

                // 创建中继服务
                _relayService = new TcpRelayService(null);
                
                // 启动服务
                var serviceStarted = await _relayService.StartAsync();
                if (!serviceStarted)
                {
                    Console.WriteLine("   - ❌ 中继服务启动失败");
                    return false;
                }
                Console.WriteLine("   - 中继服务已启动");

                // 启动规则
                var ruleStarted = await _relayService.StartRelayRuleAsync(rule);
                if (!ruleStarted)
                {
                    Console.WriteLine("   - ❌ 规则启动失败");
                    return false;
                }
                Console.WriteLine("   - 规则已启动，开始监听端口 19999");

                // 等待数据源连接建立
                await Task.Delay(2000);

                // 检查数据源连接状态
                var dataSourceStatus = _relayService.GetDataSourceConnectionStatus("127.0.0.1", 18080);
                Console.WriteLine($"   - 数据源连接状态: {dataSourceStatus}");

                return dataSourceStatus == DataSourceConnectionStatus.Connected || 
                       dataSourceStatus == DataSourceConnectionStatus.Connecting;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   - ❌ 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试2: 连接解耦验证
        /// </summary>
        private async Task<bool> TestConnectionDecoupling()
        {
            try
            {
                if (_relayService == null)
                {
                    Console.WriteLine("   - ❌ 中继服务未初始化");
                    return false;
                }

                // 模拟消费端连接
                _mockConsumer = new TcpClient();
                await _mockConsumer.ConnectAsync(IPAddress.Loopback, 19999);
                Console.WriteLine("   - 消费端已连接到中继服务");

                // 等待连接稳定
                await Task.Delay(1000);

                // 检查数据源连接状态（应该仍然保持）
                var dataSourceStatusBefore = _relayService.GetDataSourceConnectionStatus("127.0.0.1", 18080);
                Console.WriteLine($"   - 消费端连接前数据源状态: {dataSourceStatusBefore}");

                // 断开消费端连接
                _mockConsumer.Close();
                _mockConsumer.Dispose();
                _mockConsumer = null;
                Console.WriteLine("   - 消费端连接已断开");

                // 等待一段时间
                await Task.Delay(2000);

                // 检查数据源连接状态（应该仍然保持连接）
                var dataSourceStatusAfter = _relayService.GetDataSourceConnectionStatus("127.0.0.1", 18080);
                Console.WriteLine($"   - 消费端断开后数据源状态: {dataSourceStatusAfter}");

                // 验证数据源连接未受影响
                return dataSourceStatusAfter == DataSourceConnectionStatus.Connected ||
                       dataSourceStatusAfter == DataSourceConnectionStatus.Connecting;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   - ❌ 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试3: 持续重连机制
        /// </summary>
        private async Task<bool> TestContinuousReconnection()
        {
            try
            {
                if (_relayService == null || _mockDataSource == null)
                {
                    Console.WriteLine("   - ❌ 服务未初始化");
                    return false;
                }

                // 停止数据源服务器
                _mockDataSource.Stop();
                Console.WriteLine("   - 数据源服务器已停止");

                // 等待检测到断开
                await Task.Delay(3000);

                var statusAfterStop = _relayService.GetDataSourceConnectionStatus("127.0.0.1", 18080);
                Console.WriteLine($"   - 停止后数据源状态: {statusAfterStop}");

                // 重新启动数据源服务器
                _mockDataSource = new TcpListener(IPAddress.Loopback, 18080);
                _mockDataSource.Start();
                Console.WriteLine("   - 数据源服务器已重启");

                // 等待重连
                await Task.Delay(8000); // 等待足够时间进行重连

                var statusAfterRestart = _relayService.GetDataSourceConnectionStatus("127.0.0.1", 18080);
                Console.WriteLine($"   - 重启后数据源状态: {statusAfterRestart}");

                // 验证能够重新连接
                return statusAfterRestart == DataSourceConnectionStatus.Connected ||
                       statusAfterRestart == DataSourceConnectionStatus.Connecting;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   - ❌ 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试4: 数据源连接状态显示
        /// </summary>
        private async Task<bool> TestDataSourceStatusDisplay()
        {
            try
            {
                if (_relayService == null)
                {
                    Console.WriteLine("   - ❌ 中继服务未初始化");
                    return false;
                }

                // 测试状态获取方法
                var status = _relayService.GetDataSourceConnectionStatus("127.0.0.1", 18080);
                Console.WriteLine($"   - 当前数据源连接状态: {status}");

                // 测试不存在的连接
                var nonExistentStatus = _relayService.GetDataSourceConnectionStatus("192.168.1.100", 8080);
                Console.WriteLine($"   - 不存在连接的状态: {nonExistentStatus}");

                // 验证状态获取功能正常
                return status != DataSourceConnectionStatus.Error && 
                       nonExistentStatus == DataSourceConnectionStatus.Disconnected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   - ❌ 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private async Task CleanupAsync()
        {
            try
            {
                Console.WriteLine("🧹 清理测试资源...");

                _mockConsumer?.Close();
                _mockConsumer?.Dispose();

                if (_relayService != null)
                {
                    await _relayService.StopAsync();
                    _relayService.Dispose();
                }

                _mockDataSource?.Stop();

                Console.WriteLine("✅ 资源清理完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 清理过程中发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 静态测试入口
        /// </summary>
        public static async Task<int> Main(string[] args)
        {
            var test = new ArchitectureValidationTest();
            var result = await test.RunValidationTestAsync();
            
            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
            
            return result ? 0 : 1;
        }
    }
}
