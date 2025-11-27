using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 基于真实硬件的摆轮路径执行器
/// 通过IWheelDiverterDriver接口与实际的摆轮设备通信，不直接操作硬件细节
/// </summary>
public class HardwareSwitchingPathExecutor : ISwitchingPathExecutor
{
    private readonly ILogger<HardwareSwitchingPathExecutor> _logger;
    private readonly Dictionary<string, IWheelDiverterDriver> _diverters;
    private static readonly Regex LogSanitizer = new Regex(@"[\r\n]", RegexOptions.Compiled);

    /// <summary>
    /// 清理日志字符串，防止日志注入
    /// </summary>
    private static string SanitizeForLog(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        return LogSanitizer.Replace(input, "");
    }

    /// <summary>
    /// 初始化硬件摆轮路径执行器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="diverters">摆轮驱动器集合，键为摆轮ID</param>
    public HardwareSwitchingPathExecutor(
        ILogger<HardwareSwitchingPathExecutor> logger,
        IEnumerable<IWheelDiverterDriver> diverters)
    {
        _logger = logger;
        _diverters = diverters.ToDictionary(d => d.DiverterId, d => d);
        
        _logger.LogInformation("已初始化硬件摆轮路径执行器，管理 {Count} 个摆轮", _diverters.Count);
    }

    /// <summary>
    /// 执行摆轮路径
    /// </summary>
    /// <param name="path">要执行的完整摆轮路径</param>
    /// <param name="cancellationToken">用于取消操作的令牌</param>
    /// <returns>路径执行结果</returns>
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
            _logger.LogInformation(
                "开始执行路径，目标格口: {TargetChuteId}，段数: {SegmentCount}",
                SanitizeForLog(path.TargetChuteId.ToString()), path.Segments.Count);

            // 按顺序执行每个路径段
            foreach (var segment in path.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug(
                    "执行段 {SequenceNumber}: 摆轮={DiverterId}, 方向={Direction}, TTL={Ttl}ms",
                    segment.SequenceNumber, segment.DiverterId, segment.TargetDirection, segment.TtlMilliseconds);

                // 检查摆轮是否存在
                var diverterIdString = segment.DiverterId.ToString();
                if (!_diverters.TryGetValue(diverterIdString, out var diverter))
                {
                    _logger.LogError("找不到摆轮控制器: {DiverterId}", segment.DiverterId);
                    return PathExecutionResult.Failure(
                        ErrorCodes.WheelNotFound,
                        $"找不到摆轮控制器: {segment.DiverterId}",
                        path.FallbackChuteId,
                        segment);
                }

                // 使用TTL作为超时时间执行段
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(segment.TtlMilliseconds));

                bool success;
                try
                {
                    // 使用语义化驱动接口执行摆轮动作
                    // 不再直接操作角度，而是使用业务语义方法
                    success = segment.TargetDirection switch
                    {
                        DiverterDirection.Left => await diverter.TurnLeftAsync(cts.Token),
                        DiverterDirection.Right => await diverter.TurnRightAsync(cts.Token),
                        DiverterDirection.Straight => await diverter.PassThroughAsync(cts.Token),
                        _ => throw new ArgumentException($"不支持的摆轮方向: {segment.TargetDirection}", nameof(segment))
                    };
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // TTL超时
                    _logger.LogWarning(
                        "段 {SequenceNumber} 执行超时（TTL={Ttl}ms），摆轮={DiverterId}",
                        segment.SequenceNumber, segment.TtlMilliseconds, segment.DiverterId);
                    
                    return PathExecutionResult.Failure(
                        ErrorCodes.PathSegmentTimeout,
                        $"段 {segment.SequenceNumber} 执行超时",
                        path.FallbackChuteId,
                        segment);
                }

                if (!success)
                {
                    _logger.LogError(
                        "段 {SequenceNumber} 执行失败，摆轮={DiverterId}",
                        segment.SequenceNumber, segment.DiverterId);
                    
                    return PathExecutionResult.Failure(
                        ErrorCodes.PathSegmentFailed,
                        $"段 {segment.SequenceNumber} 执行失败",
                        path.FallbackChuteId,
                        segment);
                }

                _logger.LogDebug(
                    "段 {SequenceNumber} 执行成功",
                    segment.SequenceNumber);
            }

            // 所有段执行成功
            _logger.LogInformation(
                "路径执行成功，到达目标格口: {TargetChuteId}",
                SanitizeForLog(path.TargetChuteId.ToString()));

            return PathExecutionResult.Success(path.TargetChuteId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("路径执行被取消");
            return PathExecutionResult.Failure(
                ErrorCodes.Cancelled,
                "操作被取消",
                path.FallbackChuteId);
        }
        catch (WheelDriverException ex)
        {
            // 驱动层异常统一转换为 PathExecutionResult
            _logger.LogError(ex, "摆轮驱动异常: {ErrorCode}", ex.ErrorCode);
            return PathExecutionResult.Failure(
                ex.ErrorCode,
                ex.Message,
                path.FallbackChuteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "路径执行发生异常");
            return PathExecutionResult.Failure(
                ErrorCodes.Unknown,
                $"执行异常: {ex.Message}",
                path.FallbackChuteId);
        }
    }
}
