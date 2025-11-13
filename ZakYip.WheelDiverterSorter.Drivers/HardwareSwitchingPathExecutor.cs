using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 基于真实硬件的摆轮路径执行器
/// 通过IDiverterController接口与实际的摆轮设备通信
/// </summary>
public class HardwareSwitchingPathExecutor : ISwitchingPathExecutor
{
    private readonly ILogger<HardwareSwitchingPathExecutor> _logger;
    private readonly Dictionary<string, IDiverterController> _diverters;
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
    /// <param name="diverters">摆轮控制器字典，键为摆轮ID</param>
    public HardwareSwitchingPathExecutor(
        ILogger<HardwareSwitchingPathExecutor> logger,
        IEnumerable<IDiverterController> diverters)
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
                    return new PathExecutionResult
                    {
                        IsSuccess = false,
                        ActualChuteId = path.FallbackChuteId,
                        FailureReason = $"找不到摆轮控制器: {segment.DiverterId}"
                    };
                }

                // 使用TTL作为超时时间执行段
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(segment.TtlMilliseconds));

                bool success;
                try
                {
                    // 将DiverterDirection转换为物理角度
                    // 注意：具体的角度映射取决于硬件配置，这里使用通用映射
                    // 直行=0度, 左转=45度（或根据硬件配置调整）, 右转=45度（反方向）
                    int physicalAngle = ConvertDirectionToAngle(segment.TargetDirection);
                    success = await diverter.SetAngleAsync(physicalAngle, cts.Token);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // TTL超时
                    _logger.LogWarning(
                        "段 {SequenceNumber} 执行超时（TTL={Ttl}ms），摆轮={DiverterId}",
                        segment.SequenceNumber, segment.TtlMilliseconds, segment.DiverterId);
                    
                    return new PathExecutionResult
                    {
                        IsSuccess = false,
                        ActualChuteId = path.FallbackChuteId,
                        FailureReason = $"段 {segment.SequenceNumber} 执行超时"
                    };
                }

                if (!success)
                {
                    _logger.LogError(
                        "段 {SequenceNumber} 执行失败，摆轮={DiverterId}",
                        segment.SequenceNumber, segment.DiverterId);
                    
                    return new PathExecutionResult
                    {
                        IsSuccess = false,
                        ActualChuteId = path.FallbackChuteId,
                        FailureReason = $"段 {segment.SequenceNumber} 执行失败"
                    };
                }

                _logger.LogDebug(
                    "段 {SequenceNumber} 执行成功",
                    segment.SequenceNumber);
            }

            // 所有段执行成功
            _logger.LogInformation(
                "路径执行成功，到达目标格口: {TargetChuteId}",
                SanitizeForLog(path.TargetChuteId.ToString()));

            return new PathExecutionResult
            {
                IsSuccess = true,
                ActualChuteId = path.TargetChuteId
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("路径执行被取消");
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = "操作被取消"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "路径执行发生异常");
            return new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = $"执行异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 将摆轮方向转换为物理角度
    /// </summary>
    /// <param name="direction">摆轮转向方向</param>
    /// <returns>物理角度（度）</returns>
    /// <remarks>
    /// 这里使用的是通用映射，实际部署时应根据具体硬件配置进行调整。
    /// 可以将此方法改为从配置文件读取方向到角度的映射关系。
    /// </remarks>
    private static int ConvertDirectionToAngle(DiverterDirection direction)
    {
        return direction switch
        {
            DiverterDirection.Straight => 0,    // 直行：0度
            DiverterDirection.Left => 45,       // 左转：45度（或根据硬件配置）
            DiverterDirection.Right => 45,      // 右转：45度（反方向，具体实现取决于硬件）
            _ => throw new ArgumentException($"不支持的摆轮方向: {direction}", nameof(direction))
        };
    }
}
