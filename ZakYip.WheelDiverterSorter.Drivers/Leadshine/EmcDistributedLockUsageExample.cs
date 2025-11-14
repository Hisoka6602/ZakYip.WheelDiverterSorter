using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Leadshine;

namespace ZakYip.WheelDiverterSorter.Examples
{
    /// <summary>
    /// 演示 EMC 分布式锁使用的示例代码
    /// </summary>
    public class EmcDistributedLockUsageExample
    {
        /// <summary>
        /// 示例 1：基本的命名互斥锁使用模式
        /// </summary>
        public static async Task Example1_BasicNamedMutexUsage()
        {
            Console.WriteLine("=== 示例 1：基本的命名互斥锁使用模式 ===\n");

            // 创建日志记录器（实际应用中应该通过依赖注入获取）
            var logger = NullLogger<EmcNamedMutexLock>.Instance;

            // 创建分布式锁（基于卡号）
            using var resourceLock = new EmcNamedMutexLock(logger, "CardNo_0");

            try
            {
                // 尝试获取锁（30 秒超时）
                Console.WriteLine("尝试获取 EMC 资源锁...");
                var acquired = await resourceLock.TryAcquireAsync(TimeSpan.FromSeconds(30));

                if (acquired)
                {
                    Console.WriteLine("✓ 成功获取锁");

                    // 在这里执行需要独占访问的操作
                    Console.WriteLine("执行冷重置操作...");
                    await Task.Delay(2000); // 模拟重置操作

                    Console.WriteLine("操作完成");
                }
                else
                {
                    Console.WriteLine("✗ 无法获取锁（超时），可能其他实例正在使用");
                }
            }
            finally
            {
                // 释放锁
                resourceLock.Release();
                Console.WriteLine("锁已释放\n");
            }
        }

        /// <summary>
        /// 示例 2：使用 CoordinatedEmcController 自动管理锁
        /// </summary>
        public static async Task Example2_UsingCoordinatedEmcController()
        {
            Console.WriteLine("=== 示例 2：使用 CoordinatedEmcController 自动管理锁 ===\n");

            // 创建日志记录器（实际应用中应该通过依赖注入获取）
            var lockLogger = NullLogger<EmcNamedMutexLock>.Instance;
            var controllerLogger = NullLogger<CoordinatedEmcController>.Instance;

            // 创建 EMC 资源锁
            using var resourceLock = new EmcNamedMutexLock(lockLogger, "CardNo_0");

            // 创建底层 EMC 控制器（这里使用模拟实现）
            // 实际应用中应该是 LeadshineEmcController 实例
            var emcController = new MockEmcController(0);

            // 创建具有分布式锁协调能力的 EMC 控制器
            var coordinatedController = new CoordinatedEmcController(
                controllerLogger,
                emcController,
                resourceLock
            );

            try
            {
                // 初始化控制器
                Console.WriteLine("初始化 EMC 控制器...");
                var initialized = await coordinatedController.InitializeAsync();
                if (!initialized)
                {
                    Console.WriteLine("✗ 初始化失败");
                    return;
                }
                Console.WriteLine("✓ 初始化成功");

                // 执行冷重置（会自动获取锁、执行重置、释放锁）
                Console.WriteLine("\n执行冷重置（会自动管理锁）...");
                var resetSuccess = await coordinatedController.ColdResetAsync();
                
                if (resetSuccess)
                {
                    Console.WriteLine("✓ 冷重置成功");
                }
                else
                {
                    Console.WriteLine("✗ 冷重置失败");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 操作失败: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 示例 3：多实例协同场景说明
        /// </summary>
        public static void Example3_MultiInstanceScenario()
        {
            Console.WriteLine("=== 示例 3：多实例协同场景 ===\n");

            Console.WriteLine("场景描述：");
            Console.WriteLine("  实例 A (进程 1234) ───┐");
            Console.WriteLine("  实例 B (进程 5678) ───┼──→ EMC 控制器 (CardNo: 0)");
            Console.WriteLine("  实例 C (进程 9012) ───┘");
            Console.WriteLine();

            Console.WriteLine("工作流程：");
            Console.WriteLine();

            Console.WriteLine("1. 实例 A 需要执行冷复位");
            Console.WriteLine("   → 调用 coordinatedController.ColdResetAsync()");
            Console.WriteLine();

            Console.WriteLine("2. 实例 A 自动执行：");
            Console.WriteLine("   → 尝试获取命名互斥锁（Global\\ZakYip_EMC_CardNo_0）");
            Console.WriteLine("   → 如果获取成功，开始执行冷复位操作");
            Console.WriteLine("   → 执行冷复位（约 10-15 秒）");
            Console.WriteLine("   → 完成后释放锁");
            Console.WriteLine();

            Console.WriteLine("3. 实例 B 和 C 的行为：");
            Console.WriteLine("   → 如果在实例 A 持有锁期间尝试执行重置");
            Console.WriteLine("   → TryAcquireAsync 会等待（超时 30 秒）");
            Console.WriteLine("   → 如果实例 A 在超时前完成，实例 B/C 可以获取锁");
            Console.WriteLine("   → 如果超时，TryAcquireAsync 返回 false，重置操作取消");
            Console.WriteLine();

            Console.WriteLine("关键特性：");
            Console.WriteLine("✓ 进程级别的互斥，确保同一时间只有一个实例能执行重置");
            Console.WriteLine("✓ 自动锁管理，无需手动调用获取/释放");
            Console.WriteLine("✓ 超时保护，避免无限等待");
            Console.WriteLine("✓ 异常安全，即使进程崩溃，锁也会被释放（abandoned mutex）");
            Console.WriteLine();

            Console.WriteLine("与 TCP 锁的对比：");
            Console.WriteLine("命名互斥锁优点：");
            Console.WriteLine("  + 不需要额外的锁服务器");
            Console.WriteLine("  + 由操作系统管理，更可靠");
            Console.WriteLine("  + 配置更简单");
            Console.WriteLine("TCP 锁优点：");
            Console.WriteLine("  + 可以跨机器协调");
            Console.WriteLine("  + 支持更复杂的通知机制");
            Console.WriteLine("  + 可以收集所有实例的状态");
            Console.WriteLine();
        }

        /// <summary>
        /// 示例 4：处理锁获取失败的情况
        /// </summary>
        public static async Task Example4_HandlingLockFailure()
        {
            Console.WriteLine("=== 示例 4：处理锁获取失败 ===\n");

            var logger = NullLogger<EmcNamedMutexLock>.Instance;

            // 模拟两个实例同时尝试获取锁
            using var lock1 = new EmcNamedMutexLock(logger, "CardNo_0");
            using var lock2 = new EmcNamedMutexLock(logger, "CardNo_0");

            // 实例 1 获取锁
            Console.WriteLine("实例 1 尝试获取锁...");
            var acquired1 = await lock1.TryAcquireAsync(TimeSpan.FromSeconds(30));
            if (acquired1)
            {
                Console.WriteLine("✓ 实例 1 成功获取锁");

                // 实例 2 尝试获取锁（会失败）
                Console.WriteLine("\n实例 2 尝试获取锁（短超时）...");
                var acquired2 = await lock2.TryAcquireAsync(TimeSpan.FromSeconds(2));
                
                if (!acquired2)
                {
                    Console.WriteLine("✗ 实例 2 获取锁失败（预期行为）");
                    Console.WriteLine("   → 可以选择重试、等待或取消操作");
                    Console.WriteLine("   → 记录日志并通知用户");
                }

                // 实例 1 释放锁
                Console.WriteLine("\n实例 1 释放锁...");
                lock1.Release();

                // 实例 2 再次尝试
                Console.WriteLine("实例 2 再次尝试获取锁...");
                acquired2 = await lock2.TryAcquireAsync(TimeSpan.FromSeconds(30));
                if (acquired2)
                {
                    Console.WriteLine("✓ 实例 2 成功获取锁");
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 运行所有示例
        /// </summary>
        public static async Task RunAllExamples()
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   EMC 分布式锁使用示例                                      ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

            try
            {
                await Example1_BasicNamedMutexUsage();
                await Example2_UsingCoordinatedEmcController();
                Example3_MultiInstanceScenario();
                await Example4_HandlingLockFailure();

                Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║   所有示例运行完成                                          ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ 示例运行失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// 模拟 EMC 控制器，用于示例演示
    /// </summary>
    internal class MockEmcController : ZakYip.WheelDiverterSorter.Drivers.Abstractions.IEmcController
    {
        public ushort CardNo { get; }

        public MockEmcController(ushort cardNo)
        {
            CardNo = cardNo;
        }

        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"  [MockEMC] 初始化 CardNo {CardNo}");
            return Task.FromResult(true);
        }

        public Task<bool> ColdResetAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"  [MockEMC] 执行冷重置 CardNo {CardNo}");
            Thread.Sleep(1000); // 模拟重置时间
            Console.WriteLine($"  [MockEMC] 冷重置完成 CardNo {CardNo}");
            return Task.FromResult(true);
        }

        public Task<bool> HotResetAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"  [MockEMC] 执行热重置 CardNo {CardNo}");
            Thread.Sleep(500); // 模拟重置时间
            Console.WriteLine($"  [MockEMC] 热重置完成 CardNo {CardNo}");
            return Task.FromResult(true);
        }

        public Task<bool> StopAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"  [MockEMC] 停止 CardNo {CardNo}");
            return Task.FromResult(true);
        }

        public Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"  [MockEMC] 恢复 CardNo {CardNo}");
            return Task.FromResult(true);
        }

        public bool IsAvailable()
        {
            return true;
        }
    }
}
