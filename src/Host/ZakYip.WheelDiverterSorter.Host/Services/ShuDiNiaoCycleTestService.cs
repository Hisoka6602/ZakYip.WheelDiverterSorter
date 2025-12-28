using System.Collections.Concurrent;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 数递鸟摆轮循环测试服务
/// </summary>
/// <remarks>
/// 负责管理摆轮的循环测试任务，支持启动和停止多个并发测试。
/// 每个测试任务在独立的后台任务中运行，互不干扰。
/// </remarks>
public class ShuDiNiaoCycleTestService
{
    private readonly IWheelDiverterDriverManager? _driverManager;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<ShuDiNiaoCycleTestService> _logger;
    
    // 使用 ConcurrentDictionary 存储正在运行的测试任务
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTests = new();
    
    /// <summary>
    /// 最小指令间隔（毫秒）- 数递鸟摆轮硬件要求
    /// </summary>
    private const int MinCommandIntervalMs = 90;

    /// <summary>
    /// 初始化循环测试服务
    /// </summary>
    public ShuDiNiaoCycleTestService(
        IWheelDiverterDriverManager? driverManager,
        ISafeExecutionService safeExecutor,
        ILogger<ShuDiNiaoCycleTestService> logger)
    {
        _driverManager = driverManager;
        _safeExecutor = safeExecutor;
        _logger = logger;
    }

    /// <summary>
    /// 启动循环测试
    /// </summary>
    /// <param name="testId">测试唯一标识</param>
    /// <param name="diverterId">摆轮ID</param>
    /// <param name="direction">测试方向</param>
    /// <param name="cycleCount">循环次数</param>
    /// <param name="intervalMs">指令间隔（毫秒）</param>
    /// <returns>是否成功启动</returns>
    public async Task<bool> StartCycleTestAsync(
        string testId,
        long diverterId,
        DiverterDirection direction,
        int cycleCount,
        int intervalMs)
    {
        if (_driverManager == null)
        {
            _logger.LogWarning("[循环测试] 摆轮驱动管理器未初始化，无法启动测试");
            return false;
        }

        // 验证参数
        if (intervalMs < MinCommandIntervalMs)
        {
            _logger.LogWarning(
                "[循环测试] 指令间隔 {IntervalMs}ms 小于最小要求 {MinIntervalMs}ms，已自动调整",
                intervalMs, MinCommandIntervalMs);
            intervalMs = MinCommandIntervalMs;
        }

        // 检查测试是否已存在
        if (_runningTests.ContainsKey(testId))
        {
            _logger.LogWarning("[循环测试] 测试 {TestId} 已在运行中", testId);
            return false;
        }

        // 创建取消令牌
        var cts = new CancellationTokenSource();
        if (!_runningTests.TryAdd(testId, cts))
        {
            _logger.LogWarning("[循环测试] 测试 {TestId} 添加失败（并发冲突）", testId);
            cts.Dispose();
            return false;
        }

        _logger.LogInformation(
            "[循环测试] 启动测试 {TestId}: 摆轮={DiverterId}, 方向={Direction}, 次数={CycleCount}, 间隔={IntervalMs}ms",
            testId, diverterId, direction, cycleCount, intervalMs);

        // 启动后台任务
        _ = _safeExecutor.ExecuteAsync(
            async () => await RunCycleTestAsync(testId, diverterId, direction, cycleCount, intervalMs, cts.Token),
            operationName: $"ShuDiNiaoCycleTest-{testId}",
            cancellationToken: cts.Token);

        return true;
    }

    /// <summary>
    /// 停止循环测试
    /// </summary>
    /// <param name="testId">测试ID，如果为空则停止所有测试</param>
    /// <returns>停止的测试数量</returns>
    public int StopCycleTest(string? testId = null)
    {
        if (string.IsNullOrEmpty(testId))
        {
            // 停止所有测试
            var count = _runningTests.Count;
            _logger.LogInformation("[循环测试] 停止所有测试，共 {Count} 个", count);
            
            foreach (var kvp in _runningTests)
            {
                try
                {
                    kvp.Value.Cancel();
                    kvp.Value.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[循环测试] 停止测试 {TestId} 时发生异常", kvp.Key);
                }
            }
            
            _runningTests.Clear();
            return count;
        }
        else
        {
            // 停止指定测试
            if (_runningTests.TryRemove(testId, out var cts))
            {
                _logger.LogInformation("[循环测试] 停止测试 {TestId}", testId);
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[循环测试] 停止测试 {TestId} 时发生异常", testId);
                }
                return 1;
            }
            
            _logger.LogWarning("[循环测试] 测试 {TestId} 不存在或已停止", testId);
            return 0;
        }
    }

    /// <summary>
    /// 获取正在运行的测试数量
    /// </summary>
    public int GetRunningTestCount() => _runningTests.Count;

    /// <summary>
    /// 执行循环测试
    /// </summary>
    private async Task RunCycleTestAsync(
        string testId,
        long diverterId,
        DiverterDirection direction,
        int cycleCount,
        int intervalMs,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "[循环测试] {TestId} 开始执行: 摆轮={DiverterId}, 方向={Direction}, 次数={CycleCount}",
                testId, diverterId, direction, cycleCount);

            var driver = _driverManager!.GetDriver(diverterId.ToString());
            if (driver == null)
            {
                _logger.LogError("[循环测试] {TestId} 失败: 摆轮 {DiverterId} 驱动未找到", testId, diverterId);
                return;
            }

            int successCount = 0;
            int failureCount = 0;

            for (int i = 1; i <= cycleCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "[循环测试] {TestId} 被取消: 已完成 {SuccessCount}/{TotalCount} 次",
                        testId, successCount, cycleCount);
                    break;
                }

                // 执行摆轮动作
                bool success = direction switch
                {
                    DiverterDirection.Left => await driver.TurnLeftAsync(cancellationToken),
                    DiverterDirection.Right => await driver.TurnRightAsync(cancellationToken),
                    DiverterDirection.Straight => await driver.PassThroughAsync(cancellationToken),
                    _ => false
                };

                if (success)
                {
                    successCount++;
                    _logger.LogDebug(
                        "[循环测试] {TestId} 进度: {Current}/{Total}, 摆轮={DiverterId}, 方向={Direction}",
                        testId, i, cycleCount, diverterId, direction);
                }
                else
                {
                    failureCount++;
                    _logger.LogWarning(
                        "[循环测试] {TestId} 第 {Current} 次失败, 摆轮={DiverterId}, 方向={Direction}",
                        testId, i, diverterId, direction);
                }

                // 等待指令间隔
                if (i < cycleCount) // 最后一次不需要等待
                {
                    await Task.Delay(intervalMs, cancellationToken);
                }
            }

            _logger.LogInformation(
                "[循环测试] {TestId} 完成: 成功={SuccessCount}, 失败={FailureCount}, 总计={TotalCount}",
                testId, successCount, failureCount, cycleCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[循环测试] {TestId} 被取消", testId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[循环测试] {TestId} 发生异常", testId);
        }
        finally
        {
            // 清理
            if (_runningTests.TryRemove(testId, out var cts))
            {
                cts.Dispose();
            }
        }
    }
}
