using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 系统配置响应模型
/// </summary>
public class SystemConfigResponse
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 异常格口ID
    /// </summary>
    public long ExceptionChuteId { get; set; }

    /// <summary>
    /// MQTT默认端口
    /// </summary>
    public int MqttDefaultPort { get; set; }

    /// <summary>
    /// TCP默认端口
    /// </summary>
    public int TcpDefaultPort { get; set; }

    /// <summary>
    /// 格口分配超时时间（毫秒）
    /// </summary>
    public int ChuteAssignmentTimeoutMs { get; set; }

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public int RequestTimeoutMs { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    public int RetryDelayMs { get; set; }

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; set; }

    /// <summary>
    /// 分拣模式
    /// </summary>
    public SortingMode SortingMode { get; set; }

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// </summary>
    public long? FixedChuteId { get; set; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// </summary>
    public List<long> AvailableChuteIds { get; set; } = new();

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间（UTC）
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
