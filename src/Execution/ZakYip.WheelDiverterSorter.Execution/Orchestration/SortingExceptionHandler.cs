using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;

namespace ZakYip.WheelDiverterSorter.Execution.Orchestration;

/// <summary>
/// 分拣异常处理器实现
/// </summary>
/// <remarks>
/// 统一处理分拣流程中的所有异常场景，避免重复代码。
/// 确保异常处理逻辑的一致性和可维护性。
/// </remarks>
public class SortingExceptionHandler : ISortingExceptionHandler
{
    private readonly ISwitchingPathGenerator _pathGenerator;
    private readonly ILogger<SortingExceptionHandler> _logger;

    public SortingExceptionHandler(
        ISwitchingPathGenerator pathGenerator,
        ILogger<SortingExceptionHandler> logger)
    {
        _pathGenerator = pathGenerator ?? throw new ArgumentNullException(nameof(pathGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 尝试生成到异常格口的路径
    /// </summary>
    public SwitchingPath? GenerateExceptionPath(long exceptionChuteId, long parcelId, string reason)
    {
        _logger.LogWarning(
            "包裹 {ParcelId} 路由到异常格口 {ExceptionChuteId}。原因: {Reason}",
            parcelId,
            exceptionChuteId,
            reason);

        var path = _pathGenerator.GeneratePath(exceptionChuteId);

        if (path == null)
        {
            _logger.LogError(
                "【严重错误】包裹 {ParcelId} 连异常格口 {ExceptionChuteId} 的路径都无法生成，分拣失败。" +
                "请检查异常格口配置是否正确。原因: {Reason}",
                parcelId,
                exceptionChuteId,
                reason);
        }

        return path;
    }

    /// <summary>
    /// 处理路径生成完全失败的情况（连异常格口路径都无法生成）
    /// </summary>
    public SortingResult CreatePathGenerationFailureResult(
        long parcelId,
        long targetChuteId,
        long exceptionChuteId,
        string reason)
    {
        _logger.LogError(
            "包裹 {ParcelId} 路径生成完全失败：无法生成到目标格口 {TargetChuteId} 或异常格口 {ExceptionChuteId} 的路径。原因: {Reason}",
            parcelId,
            targetChuteId,
            exceptionChuteId,
            reason);

        return new SortingResult(
            IsSuccess: false,
            ParcelId: parcelId.ToString(),
            ActualChuteId: 0,
            TargetChuteId: targetChuteId,
            ExecutionTimeMs: 0,
            FailureReason: $"路径生成失败: {reason}，连异常格口路径都无法生成"
        );
    }
}
