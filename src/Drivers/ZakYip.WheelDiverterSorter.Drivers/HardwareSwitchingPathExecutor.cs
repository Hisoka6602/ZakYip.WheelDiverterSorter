using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 基于真实硬件的摆轮路径执行器
/// </summary>
/// <remarks>
/// <para>通过统一的 <see cref="IWheelCommandExecutor"/> 接口执行摆轮命令。</para>
/// <para>"发送命令 + 等待反馈 + 超时处理 + 日志记录"的逻辑已收敛到 
/// <see cref="IWheelCommandExecutor"/> 中，本类仅负责路径级别的编排。</para>
/// <para>仿真模式与真实模式共用此路径执行器，区别只在于注入的驱动实现。</para>
/// </remarks>
public class HardwareSwitchingPathExecutor : ISwitchingPathExecutor
{
    private readonly ILogger<HardwareSwitchingPathExecutor> _logger;
    private readonly IWheelCommandExecutor _wheelCommandExecutor;
    private static readonly Regex LogSanitizer = new(@"[\r\n]", RegexOptions.Compiled);

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
    /// <param name="wheelCommandExecutor">统一的摆轮命令执行器</param>
    public HardwareSwitchingPathExecutor(
        ILogger<HardwareSwitchingPathExecutor> logger,
        IWheelCommandExecutor wheelCommandExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wheelCommandExecutor = wheelCommandExecutor ?? throw new ArgumentNullException(nameof(wheelCommandExecutor));
        
        _logger.LogInformation("已初始化硬件摆轮路径执行器（使用统一命令执行器）");
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
        ArgumentNullException.ThrowIfNull(path);

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

                // 构造摆轮命令并通过统一执行器执行
                var command = new WheelCommand
                {
                    DiverterId = segment.DiverterId.ToString(),
                    Direction = segment.TargetDirection,
                    Timeout = TimeSpan.FromMilliseconds(segment.TtlMilliseconds),
                    SequenceNumber = segment.SequenceNumber
                };

                var result = await _wheelCommandExecutor.ExecuteAsync(command, cancellationToken);

                if (!result.IsSuccess)
                {
                    // 根据错误码转换为路径执行错误
                    var pathErrorCode = result.ErrorCode switch
                    {
                        ErrorCodes.WheelCommandTimeout => ErrorCodes.PathSegmentTimeout,
                        ErrorCodes.WheelNotFound => ErrorCodes.WheelNotFound,
                        _ => ErrorCodes.PathSegmentFailed
                    };

                    _logger.LogWarning(
                        "段 {SequenceNumber} 执行失败 | 摆轮={DiverterId} | 原因={Reason}",
                        segment.SequenceNumber, segment.DiverterId, result.ErrorMessage);
                    
                    return PathExecutionResult.Failure(
                        pathErrorCode,
                        result.ErrorMessage ?? $"段 {segment.SequenceNumber} 执行失败",
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
