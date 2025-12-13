using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 落格回调配置模型
/// </summary>
/// <remarks>
/// 定义落格回调的触发模式，决定何时向上游发送分拣完成通知
/// </remarks>
public record ChuteDropoffCallbackConfiguration
{
    /// <summary>
    /// 配置ID（数据库主键）
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 配置名称
    /// </summary>
    public required string ConfigName { get; init; }

    /// <summary>
    /// 落格回调触发模式
    /// </summary>
    public ChuteDropoffCallbackMode TriggerMode { get; init; }

    /// <summary>
    /// 是否启用落格回调
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 版本号（用于并发控制）
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static ChuteDropoffCallbackConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new ChuteDropoffCallbackConfiguration
        {
            Id = 1,
            ConfigName = "chute-dropoff-callback",
            TriggerMode = ChuteDropoffCallbackMode.OnSensorTrigger,  // 默认使用传感器触发
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }
}
