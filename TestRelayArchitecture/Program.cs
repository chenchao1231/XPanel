using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using XPluginTcpRelay.Services;
using XPluginTcpRelay.Models;

namespace TestRelayArchitecture
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("🚀 XPluginTcpRelay 架构验证测试");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            try
            {
                // 测试基本功能
                await TestBasicFunctionality();
                
                Console.WriteLine();
                Console.WriteLine("✅ 基本功能测试完成");
                Console.WriteLine();
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 测试失败: {ex.Message}");
                Console.WriteLine($"详细信息: {ex}");
                
                Console.WriteLine();
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                
                return 1;
            }
        }

        static async Task TestBasicFunctionality()
        {
            Console.WriteLine("📋 测试1: 基本架构验证");
            
            // 1. 创建路由规则
            var rule = new RouteRule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "测试规则",
                DataSourceIp = "127.0.0.1",
                DataSourcePort = 18080,
                LocalServerPort = 19999,
                IsEnabled = true,
                Description = "架构验证测试规则"
            };
            
            Console.WriteLine($"   - 创建路由规则: {rule.Name}");
            Console.WriteLine($"   - 数据源: {rule.DataSourceIp}:{rule.DataSourcePort}");
            Console.WriteLine($"   - 本地监听: {rule.LocalServerPort}");

            // 2. 创建中继服务
            var relayService = new TcpRelayService(null);
            Console.WriteLine("   - 中继服务已创建");

            // 3. 启动服务
            var serviceStarted = await relayService.StartAsync();
            Console.WriteLine($"   - 服务启动: {(serviceStarted ? "成功" : "失败")}");

            if (serviceStarted)
            {
                // 4. 启动规则
                var ruleStarted = await relayService.StartRelayRuleAsync(rule);
                Console.WriteLine($"   - 规则启动: {(ruleStarted ? "成功" : "失败")}");

                if (ruleStarted)
                {
                    // 5. 检查数据源连接状态
                    await Task.Delay(2000); // 等待连接尝试
                    
                    var dataSourceStatus = relayService.GetDataSourceConnectionStatus("127.0.0.1", 18080);
                    Console.WriteLine($"   - 数据源连接状态: {dataSourceStatus}");
                    
                    // 6. 检查规则是否活跃
                    var isActive = relayService.IsRuleActive(rule.Id);
                    Console.WriteLine($"   - 规则活跃状态: {isActive}");
                    
                    // 7. 获取统计信息
                    var stats = relayService.GetStatistics();
                    Console.WriteLine($"   - 活跃连接数: {stats.ActiveConnections}");
                    Console.WriteLine($"   - 总传输字节: {stats.TotalBytesTransferred}");
                }

                // 8. 停止服务
                await relayService.StopAsync();
                Console.WriteLine("   - 服务已停止");
            }

            relayService.Dispose();
            Console.WriteLine("   - 资源已清理");
        }
    }
}
