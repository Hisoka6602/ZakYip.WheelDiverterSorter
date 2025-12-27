using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using CoreConfig = ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 系统配置服务接口（Application层扩展）
/// </summary>
/// <remarks>
/// 负责系统配置的业务逻辑，包括验证、更新、默认模板生成等。
/// 继承 Core 层的 ISystemConfigService 接口，提供配置更新和高级操作。
/// </remarks>
public interface ISystemConfigService : CoreConfig.ISystemConfigService
{
    // GetSystemConfig() inherited from CoreConfig.ISystemConfigService

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
    Task<SystemConfigUpdateResult> UpdateSystemConfigAsync(UpdateSystemConfigCommand request);

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
    Task<SortingModeUpdateResult> UpdateSortingModeAsync(UpdateSortingModeCommand request);
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
/// 系统配置更新命令
/// </summary>
/// <remarks>
/// Application层的命令对象，由Host层映射
/// </remarks>
public record UpdateSystemConfigCommand
{
    /// <summary>
    /// 异常格口ID
    /// </summary>
    public long ExceptionChuteId { get; init; } = 999;

    /// <summary>
    /// 设备初次开机后延迟连接驱动的时间（秒）
    /// </summary>
    public int DriverStartupDelaySeconds { get; init; } = 0;

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
    
    /// <summary>
    /// 启用提前触发检测功能
    /// </summary>
    public bool EnableEarlyTriggerDetection { get; init; } = false;
}

/// <summary>
/// 分拣模式更新命令
/// </summary>
public record UpdateSortingModeCommand
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
