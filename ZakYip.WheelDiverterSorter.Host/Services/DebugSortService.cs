using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Models;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 调试服务，用于测试直线摆轮分拣方案
/// </summary>
public class DebugSortService
{
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ISwitchingPathExecutor _pathExecutor;
    private readonly ILogger<DebugSortService> _logger;

    public DebugSortService(
        ISwitchingPathGenerator pathGenerator,
        ISwitchingPathExecutor pathExecutor,
        ILogger<DebugSortService> logger)
    {
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _pathExecutor = pathExecutor ?? throw new ArgumentNullException(nameof(pathExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行调试分拣操作
    /// </summary>
    /// <param name="parcelId">包裹标识</param>
    /// <param name="targetChuteId">目标格口标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调试分拣响应</returns>
    public async Task<DebugSortResponse> ExecuteDebugSortAsync(
        string parcelId,
        string targetChuteId,
        CancellationToken cancellationToken = default)
    {
        // 清理输入以防止日志注入攻击
        var sanitizedParcelId = LoggingHelper.SanitizeForLogging(parcelId);
        var sanitizedTargetChuteId = LoggingHelper.SanitizeForLogging(targetChuteId);

        _logger.LogInformation("开始调试分拣: 包裹ID={ParcelId}, 目标格口={TargetChuteId}", 
            sanitizedParcelId, sanitizedTargetChuteId);

        // 转换字符串格口ID为整数
        if (!ChuteIdHelper.TryParseChuteId(targetChuteId, out var numericChuteId))
        {
            _logger.LogWarning("无效的格口ID格式: {TargetChuteId}", sanitizedTargetChuteId);
            return new DebugSortResponse
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                IsSuccess = false,
                ActualChuteId = "未知",
                Message = "格口ID格式无效",
                FailureReason = $"无法解析格口ID: {targetChuteId}",
                PathSegmentCount = 0
            };
        }

        // 1. 调用路径生成器生成 SwitchingPath
        var path = _pathGenerator.GeneratePath(numericChuteId);

        if (path == null)
        {
            _logger.LogWarning("无法生成路径: 目标格口={TargetChuteId}", sanitizedTargetChuteId);
            return new DebugSortResponse
            {
                ParcelId = parcelId,
                TargetChuteId = targetChuteId,
                IsSuccess = false,
                ActualChuteId = "未知",
                Message = "路径生成失败：目标格口无法映射到任何摆轮组合",
                FailureReason = "目标格口未配置或不存在",
                PathSegmentCount = 0
            };
        }

        _logger.LogInformation("路径生成成功: 段数={SegmentCount}, 目标格口={TargetChuteId}",
            path.Segments.Count, LoggingHelper.SanitizeForLogging(path.TargetChuteId));

        // 2. 调用执行器执行路径
        var executionResult = await _pathExecutor.ExecuteAsync(path, cancellationToken);

        _logger.LogInformation("路径执行完成: 成功={IsSuccess}, 实际格口={ActualChuteId}",
            executionResult.IsSuccess, LoggingHelper.SanitizeForLogging(executionResult.ActualChuteId));

        // 3. 返回执行结果
        return new DebugSortResponse
        {
            ParcelId = parcelId,
            TargetChuteId = targetChuteId,
            IsSuccess = executionResult.IsSuccess,
            ActualChuteId = ChuteIdHelper.FormatChuteId(executionResult.ActualChuteId),
            Message = executionResult.IsSuccess
                ? $"分拣成功：包裹 {parcelId} 已成功分拣到格口 {executionResult.ActualChuteId}"
                : $"分拣失败：包裹 {parcelId} 落入异常格口 {executionResult.ActualChuteId}",
            FailureReason = executionResult.FailureReason,
            PathSegmentCount = path.Segments.Count
        };
    }
}
