using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

/// <summary>
/// 分拣系统配置选项（强类型）
/// </summary>
/// <remarks>
/// 统一管理分拣模式、格口分配、异常处理等核心配置
/// 通过 IValidateOptions 实现启动时校验，避免运行时配置错误
/// </remarks>
public record SortingSystemOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "SortingSystem";

    /// <summary>
    /// 分拣模式
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>Formal</c>: 正式分拣模式，由上游 RuleEngine 分配格口</item>
    ///   <item><c>FixedChute</c>: 固定格口模式，所有包裹发送到指定格口</item>
    ///   <item><c>RoundRobin</c>: 轮询模式，按顺序循环分配格口</item>
    /// </list>
    /// </remarks>
    public SortingMode SortingMode { get; init; } = SortingMode.Formal;

    /// <summary>
    /// 异常格口ID
    /// </summary>
    /// <remarks>
    /// 当分拣失败或无法分配格口时使用的目标格口。
    /// 必须大于0，建议配置为线体末端的格口。
    /// </remarks>
    public long ExceptionChuteId { get; init; } = 999;

    /// <summary>
    /// 固定格口ID（仅在 FixedChute 模式下有效）
    /// </summary>
    /// <remarks>
    /// 当 SortingMode 为 FixedChute 时，所有包裹将发送到此格口。
    /// 在 FixedChute 模式下必须配置且大于0。
    /// </remarks>
    public long? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在 RoundRobin 模式下有效）
    /// </summary>
    /// <remarks>
    /// 当 SortingMode 为 RoundRobin 时，系统按顺序循环使用这些格口。
    /// 在 RoundRobin 模式下必须配置至少一个有效格口。
    /// </remarks>
    public List<long> AvailableChuteIds { get; init; } = new();

    /// <summary>
    /// 格口分配超时安全系数
    /// </summary>
    /// <remarks>
    /// 实际等待时间 = 理论物理极限时间 × SafetyFactor。
    /// 范围：0.1 ~ 1.0，默认 0.9。
    /// </remarks>
    public decimal ChuteAssignmentTimeoutSafetyFactor { get; init; } = 0.9m;

    /// <summary>
    /// 格口分配降级超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 当无法计算理论物理极限时间时使用的保守默认值。
    /// 范围：1000 ~ 60000 毫秒，默认 5000 毫秒。
    /// </remarks>
    public int ChuteAssignmentFallbackTimeoutMs { get; init; } = 5000;
}

/// <summary>
/// SortingSystemOptions 校验器
/// </summary>
/// <remarks>
/// 实现 IValidateOptions，在应用启动时校验配置合法性。
/// 当配置非法时，应用启动失败并输出清晰的中文错误信息。
/// </remarks>
public class SortingSystemOptionsValidator : IValidateOptions<SortingSystemOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SortingSystemOptions options)
    {
        var errors = new List<string>();

        // 校验异常格口
        if (options.ExceptionChuteId <= 0)
        {
            errors.Add("异常格口ID（ExceptionChuteId）必须大于0");
        }

        // 校验分拣模式相关配置
        switch (options.SortingMode)
        {
            case SortingMode.FixedChute:
                if (!options.FixedChuteId.HasValue || options.FixedChuteId.Value <= 0)
                {
                    errors.Add("指定落格分拣模式（FixedChute）下，固定格口ID（FixedChuteId）必须配置且大于0");
                }
                break;

            case SortingMode.RoundRobin:
                if (options.AvailableChuteIds == null || options.AvailableChuteIds.Count == 0)
                {
                    errors.Add("循环格口落格模式（RoundRobin）下，必须配置至少一个可用格口（AvailableChuteIds）");
                }
                else if (options.AvailableChuteIds.Any(id => id <= 0))
                {
                    errors.Add("可用格口ID列表（AvailableChuteIds）中不能包含小于等于0的值");
                }
                break;
        }

        // 校验超时配置
        if (options.ChuteAssignmentTimeoutSafetyFactor < 0.1m || options.ChuteAssignmentTimeoutSafetyFactor > 1.0m)
        {
            errors.Add("格口分配超时安全系数（ChuteAssignmentTimeoutSafetyFactor）必须在0.1到1.0之间");
        }

        if (options.ChuteAssignmentFallbackTimeoutMs < 1000 || options.ChuteAssignmentFallbackTimeoutMs > 60000)
        {
            errors.Add("格口分配降级超时时间（ChuteAssignmentFallbackTimeoutMs）必须在1000到60000毫秒之间");
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
