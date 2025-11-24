using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 系统配置响应模型
/// </summary>
/// <remarks>
/// 系统配置不包含通信相关字段，通信配置请使用 /api/communication/config/persisted 端点
/// </remarks>
public record SystemConfigResponse
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// 异常格口ID
    /// </summary>
    public required long ExceptionChuteId { get; init; }

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
    public List<long> AvailableChuteIds { get; init; } = new();

    /// <summary>
    /// 配置版本号
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间（UTC）
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
