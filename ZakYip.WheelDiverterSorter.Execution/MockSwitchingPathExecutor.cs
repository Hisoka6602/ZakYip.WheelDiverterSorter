using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 模拟的摆轮路径执行器，用于测试和调试
/// </summary>
/// <remarks>
/// 此实现模拟真实执行器的行为，但不进行实际的设备通信。
/// 所有路径段都会模拟延迟后标记为成功。
/// </remarks>
public class MockSwitchingPathExecutor : ISwitchingPathExecutor
{
    /// <summary>
    /// 异步执行摆轮路径（模拟实现）
    /// </summary>
    /// <param name="path">要执行的完整摆轮路径</param>
    /// <param name="cancellationToken">用于取消操作的令牌</param>
    /// <returns>返回模拟的路径执行结果</returns>
    public async Task<PathExecutionResult> ExecuteAsync(
        SwitchingPath path,
        CancellationToken cancellationToken = default)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        try
        {
            // 模拟每个段的执行
            foreach (var segment in path.Segments)
            {
                // 模拟执行延迟（实际环境中是等待设备响应）
                var delayMs = Math.Min(segment.TtlMilliseconds / 2, 100); // 使用较短的模拟延迟
                await Task.Delay(delayMs, cancellationToken);

                // 在实际实现中，这里会检查段是否在TTL内完成
                // 模拟实现总是返回成功
            }

            // 所有段执行成功
            return new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = path.TargetChuteId
            };
        }
        catch (OperationCanceledException)
        {
            // 操作被取消，返回失败结果
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = "操作被取消",
                FailureTime = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            // 捕获所有异常，转换为失败结果
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = $"执行异常: {ex.Message}",
                FailureTime = DateTimeOffset.UtcNow
            };
        }
    }
}
