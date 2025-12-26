using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

/// <summary>
/// 基准测试用的简单路径执行器
/// Simple path executor for benchmarks
/// </summary>
/// <remarks>
/// 此实现仅用于性能测试，模拟路径执行但不实际操作硬件。
/// 所有路径段都会立即标记为成功，以便专注于测量核心逻辑性能。
/// </remarks>
public class BenchmarkPathExecutor : ISwitchingPathExecutor
{
    /// <summary>
    /// 异步执行摆轮路径（基准测试实现）
    /// </summary>
    /// <param name="path">要执行的完整摆轮路径</param>
    /// <param name="cancellationToken">用于取消操作的令牌</param>
    /// <returns>返回路径执行结果</returns>
    public ValueTask<PathExecutionResult> ExecuteAsync(
        SwitchingPath path,
        CancellationToken cancellationToken = default)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        try
        {
            // 基准测试：立即返回成功，不实际执行
            return ValueTask.FromResult(PathExecutionResult.Success(path.TargetChuteId));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(PathExecutionResult.Failure(
                ErrorCodes.Cancelled,
                "操作被取消",
                path.FallbackChuteId));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(PathExecutionResult.Failure(
                ErrorCodes.Unknown,
                $"执行异常: {ex.Message}",
                path.FallbackChuteId));
        }
    }
}
