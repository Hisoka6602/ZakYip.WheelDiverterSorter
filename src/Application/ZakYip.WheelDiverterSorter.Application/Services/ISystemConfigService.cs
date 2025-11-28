using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Application.Services;

/// <summary>
/// 系统配置服务接口
/// </summary>
/// <remarks>
/// 负责系统配置的业务逻辑，包括验证、更新、默认模板生成等
/// </remarks>
public interface ISystemConfigService
{
    /// <summary>
    /// 获取当前系统配置
    /// </summary>
    /// <returns>系统配置</returns>
    SystemConfiguration GetSystemConfig();

    /// <summary>
    /// 获取默认系统配置模板
    /// </summary>
    /// <returns>默认配置</returns>
    SystemConfiguration GetDefaultTemplate();

    /// <summary>
    /// 更新系统配置
    /// </summary>
    /// <param name="request">配置请求</param>
    /// <returns>更新结果</returns>
    Task<SystemConfigUpdateResult> UpdateSystemConfigAsync(SystemConfigRequest request);

    /// <summary>
    /// 重置系统配置为默认值
    /// </summary>
    /// <returns>重置后的配置</returns>
    Task<SystemConfiguration> ResetSystemConfigAsync();

    /// <summary>
    /// 获取分拣模式配置
    /// </summary>
    /// <returns>分拣模式</returns>
    SortingModeInfo GetSortingMode();

    /// <summary>
    /// 更新分拣模式配置
    /// </summary>
    /// <param name="request">分拣模式请求</param>
    /// <returns>更新结果</returns>
    Task<SortingModeUpdateResult> UpdateSortingModeAsync(SortingModeRequest request);
}

/// <summary>
/// 系统配置更新结果
/// </summary>
public record SystemConfigUpdateResult(
    bool Success,
    string? ErrorMessage,
    SystemConfiguration? UpdatedConfig);

/// <summary>
/// 分拣模式信息
/// </summary>
public record SortingModeInfo(
    SortingMode SortingMode,
    long? FixedChuteId,
    List<long> AvailableChuteIds);

/// <summary>
/// 分拣模式更新结果
/// </summary>
public record SortingModeUpdateResult(
    bool Success,
    string? ErrorMessage,
    SortingModeInfo? UpdatedMode);

/// <summary>
/// 系统配置请求模型
/// </summary>
/// <remarks>
/// 系统配置不包含通信相关字段，通信配置请使用 /api/communication/config/persisted 端点
/// </remarks>
public record SystemConfigRequest
{
    /// <summary>
    /// 异常格口ID
    /// </summary>
    public long ExceptionChuteId { get; init; } = 999;

    /// <summary>
    /// 分拣模式
    /// </summary>
    public SortingMode SortingMode { get; init; } = SortingMode.Formal;

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// </summary>
    public long? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// </summary>
    public List<long> AvailableChuteIds { get; init; } = new();
}

/// <summary>
/// 分拣模式配置请求模型
/// </summary>
public record SortingModeRequest
{
    /// <summary>
    /// 分拣模式
    /// </summary>
    public required SortingMode SortingMode { get; init; }

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// </summary>
    public long? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// </summary>
    public List<long>? AvailableChuteIds { get; init; }
}
