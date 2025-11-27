using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Execution;

/// <summary>
/// 表示摆轮路径执行的结果
/// </summary>
/// <remarks>
/// <para>遵循统一的 Result 模式，包含错误码支持。</para>
/// <para>
/// 使用示例：
/// <code>
/// var result = await executor.ExecuteAsync(path);
/// if (!result.IsSuccess)
/// {
///     switch (result.ErrorCode)
///     {
///         case ErrorCodes.WheelCommandTimeout:
///             // 处理超时
///             break;
///         case ErrorCodes.WheelNotFound:
///             // 处理摆轮未找到
///             break;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public record class PathExecutionResult
{
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 实际落格的格口标识（数字ID）
    /// </summary>
    /// <remarks>
    /// <para>当执行成功时，此值应等于 <see cref="SwitchingPath.TargetChuteId"/>；</para>
    /// <para>当执行失败时，此值应为 <see cref="SwitchingPath.FallbackChuteId"/>（最终异常格口）。</para>
    /// <para>注意：如果旧代码中存在硬编码的异常口逻辑，应统一使用 
    /// <see cref="SwitchingPath.FallbackChuteId"/> 字段，删除旧的实现方式。</para>
    /// </remarks>
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 错误码（当 IsSuccess = false 时）
    /// </summary>
    /// <remarks>
    /// 使用 <see cref="ErrorCodes"/> 中定义的标准错误码
    /// </remarks>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 失败原因说明
    /// 当 IsSuccess = false 时，此字段描述失败的具体原因
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 失败的路径段（如果执行失败）
    /// </summary>
    /// <remarks>
    /// 当路径段执行失败时，此字段包含失败的段信息，用于定位失败位置
    /// </remarks>
    public SwitchingPathSegment? FailedSegment { get; init; }

    /// <summary>
    /// 失败时间
    /// </summary>
    public DateTimeOffset? FailureTime { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="actualChuteId">实际落格的格口ID</param>
    public static PathExecutionResult Success(long actualChuteId) => new()
    {
        IsSuccess = true,
        ActualChuteId = actualChuteId
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="failureReason">失败原因</param>
    /// <param name="fallbackChuteId">回退格口ID</param>
    /// <param name="failedSegment">失败的路径段</param>
    public static PathExecutionResult Failure(
        string errorCode,
        string failureReason,
        long fallbackChuteId,
        SwitchingPathSegment? failedSegment = null) => new()
    {
        IsSuccess = false,
        ActualChuteId = fallbackChuteId,
        ErrorCode = errorCode,
        FailureReason = failureReason,
        FailedSegment = failedSegment,
        FailureTime = DateTimeOffset.Now
    };

    /// <summary>
    /// 转换为通用 OperationResult
    /// </summary>
    public OperationResult ToOperationResult() => IsSuccess
        ? OperationResult.Success()
        : OperationResult.Failure(ErrorCode ?? ErrorCodes.Unknown, FailureReason ?? string.Empty);
}
