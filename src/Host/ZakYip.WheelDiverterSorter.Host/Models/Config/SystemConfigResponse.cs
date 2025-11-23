using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 系统配置响应模型
/// </summary>
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
    /// MQTT默认端口
    /// </summary>
    public required int MqttDefaultPort { get; init; }

    /// <summary>
    /// TCP默认端口
    /// </summary>
    public required int TcpDefaultPort { get; init; }

    /// <summary>
    /// 格口分配超时时间（毫秒）
    /// </summary>
    public required int ChuteAssignmentTimeoutMs { get; init; }

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public required int RequestTimeoutMs { get; init; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public required int RetryCount { get; init; }

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    public required int RetryDelayMs { get; init; }

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public required bool EnableAutoReconnect { get; init; }

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
