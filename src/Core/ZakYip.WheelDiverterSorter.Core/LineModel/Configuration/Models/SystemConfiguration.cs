using LiteDB;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 系统配置模型
/// </summary>
/// <remarks>
/// 存储系统级别的配置参数，支持热重载和环境迁移
/// </remarks>
public class SystemConfiguration
{
    /// <summary>
    /// 配置ID（LiteDB自动生成）
    /// </summary>
    [BsonId]
    public int Id { get; set; }

    /// <summary>
    /// 配置名称（唯一标识符）
    /// </summary>
    /// <remarks>
    /// 使用固定值 "system" 确保只有一条系统配置记录
    /// </remarks>
    public string ConfigName { get; set; } = "system";

    /// <summary>
    /// 异常格口ID（数字ID，与路由配置中的格口ID对应）
    /// </summary>
    /// <remarks>
    /// <para>当包裹分拣失败或无法分配格口时使用的目标格口</para>
    /// <para>异常格口永远不能为空。如果未配置，系统将使用默认值</para>
    /// <para>建议：配置为在最末端一个摆轮的直行方向的格口，确保包裹能够安全通过系统</para>
    /// </remarks>
    public long ExceptionChuteId { get; set; } = 999;

    /// <summary>
    /// 格口分配超时配置
    /// </summary>
    /// <remarks>
    /// 用于配置格口分配等待超时的动态计算参数
    /// </remarks>
    public ChuteAssignmentTimeoutOptions ChuteAssignmentTimeout { get; set; } = new();

    /// <summary>
    /// 控制面板 IO 模块配置
    /// </summary>
    /// <remarks>
    /// 用于配置电柜面板上的物理按键和指示灯，包括急停、启动、停止、复位按钮以及三色灯等。
    /// 此配置是厂商无关的，通过 VendorProfileKey 关联具体厂商实现。
    /// </remarks>
    public CabinetIoOptions CabinetIo { get; set; } = new();

    /// <summary>
    /// IO 联动配置
    /// </summary>
    /// <remarks>
    /// 用于配置系统在不同状态（运行/停止）下需要联动控制的 IO 端口
    /// </remarks>
    public IoLinkageOptions IoLinkage { get; set; } = new();

    /// <summary>
    /// 分拣模式
    /// </summary>
    /// <remarks>
    /// 定义系统使用的分拣模式：正式分拣模式（默认）、指定落格分拣模式、循环格口落格模式
    /// </remarks>
    public SortingMode SortingMode { get; set; } = SortingMode.Formal;

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// </summary>
    /// <remarks>
    /// 当分拣模式为 FixedChute 时，所有包裹（异常除外）都将发送到此格口
    /// </remarks>
    public long? FixedChuteId { get; set; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// </summary>
    /// <remarks>
    /// 当分拣模式为 RoundRobin 时，系统会按顺序循环使用这些格口
    /// </remarks>
    public List<long> AvailableChuteIds { get; set; } = new();

    // ========== PR-08: 拥堵检测与背压控制配置 / Congestion Detection and Throttling Config ==========

    /// <summary>
    /// 启用节流功能
    /// </summary>
    public bool ThrottleEnabled { get; set; } = true;

    /// <summary>
    /// 警告级别延迟阈值（毫秒）
    /// </summary>
    public int ThrottleWarningLatencyMs { get; set; } = 5000;

    /// <summary>
    /// 严重级别延迟阈值（毫秒）
    /// </summary>
    public int ThrottleSevereLatencyMs { get; set; } = 10000;

    /// <summary>
    /// 警告级别成功率阈值（0.0-1.0）
    /// </summary>
    public double ThrottleWarningSuccessRate { get; set; } = 0.9;

    /// <summary>
    /// 严重级别成功率阈值（0.0-1.0）
    /// </summary>
    public double ThrottleSevereSuccessRate { get; set; } = 0.7;

    /// <summary>
    /// 警告级别在途包裹数阈值
    /// </summary>
    public int ThrottleWarningInFlightParcels { get; set; } = 50;

    /// <summary>
    /// 严重级别在途包裹数阈值
    /// </summary>
    public int ThrottleSevereInFlightParcels { get; set; } = 100;

    /// <summary>
    /// 正常状态下的放包间隔（毫秒）
    /// </summary>
    public int ThrottleNormalIntervalMs { get; set; } = 300;

    /// <summary>
    /// 警告状态下的放包间隔（毫秒）
    /// </summary>
    public int ThrottleWarningIntervalMs { get; set; } = 500;

    /// <summary>
    /// 严重状态下的放包间隔（毫秒）
    /// </summary>
    public int ThrottleSevereIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 是否在严重拥堵时暂停放包
    /// </summary>
    public bool ThrottleShouldPauseOnSevere { get; set; } = false;

    /// <summary>
    /// 指标采样时间窗口（秒）
    /// </summary>
    public int ThrottleMetricsWindowSeconds { get; set; } = 60;

    /// <summary>
    /// 配置版本号
    /// </summary>
    /// <remarks>
    /// 用于跟踪配置变更历史
    /// </remarks>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 创建时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在创建时通过 ISystemClock.LocalNow 设置，使用本地时间存储
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间（本地时间）
    /// </summary>
    /// <remarks>
    /// 由仓储在更新时通过 ISystemClock.LocalNow 设置，使用本地时间存储
    /// </remarks>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 验证配置参数的有效性
    /// </summary>
    /// <returns>验证结果和错误消息</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (ExceptionChuteId <= 0)
        {
            return (false, "异常格口ID必须大于0");
        }

        // 验证格口分配超时配置
        var timeoutValidation = ChuteAssignmentTimeout.Validate();
        if (!timeoutValidation.IsValid)
        {
            return timeoutValidation;
        }

        // 验证分拣模式相关配置
        if (SortingMode == SortingMode.FixedChute)
        {
            if (!FixedChuteId.HasValue || FixedChuteId.Value <= 0)
            {
                return (false, "指定落格分拣模式下，固定格口ID必须配置且大于0");
            }
        }

        if (SortingMode == SortingMode.RoundRobin)
        {
            if (AvailableChuteIds == null || AvailableChuteIds.Count == 0)
            {
                return (false, "循环格口落格模式下，必须配置至少一个可用格口");
            }

            if (AvailableChuteIds.Any(id => id <= 0))
            {
                return (false, "可用格口ID列表中不能包含小于等于0的值");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static SystemConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new SystemConfiguration
        {
            ConfigName = "system",
            ExceptionChuteId = 999,
            ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions(),
            CabinetIo = new CabinetIoOptions(),
            IoLinkage = new IoLinkageOptions(),
            SortingMode = SortingMode.Formal,
            FixedChuteId = null,
            AvailableChuteIds = new List<long>(),
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
