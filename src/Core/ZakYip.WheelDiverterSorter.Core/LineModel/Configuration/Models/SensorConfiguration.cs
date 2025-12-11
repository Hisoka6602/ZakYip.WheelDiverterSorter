using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 感应IO配置（存储在LiteDB中，支持热更新）
/// </summary>
/// <remarks>
/// 感应IO配置定义了系统中所有感应IO的逻辑配置。
/// 
/// **重要说明**：
/// - 厂商相关的硬件配置（如雷赛、西门子等）已移至 IO驱动器配置（/api/config/io-driver）
/// - 本配置仅包含感应IO的逻辑定义和业务类型配置
/// - 感应IO类型按业务功能分类：ParcelCreation（创建包裹）、WheelFront（摆轮前）、ChuteLock（锁格）
/// </remarks>
public class SensorConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 感应IO配置列表
    /// </summary>
    /// <remarks>
    /// 定义系统中所有感应IO的逻辑配置，包括业务类型和关联的IO点位。
    /// 注意：只能有一个 ParcelCreation 类型的感应IO处于激活状态。
    /// </remarks>
    public List<SensorIoEntry> Sensors { get; set; } = new();

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static SensorConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() 
                { 
                    SensorId = 1, 
                    SensorName = "创建包裹感应IO", 
                    IoType = SensorIoType.ParcelCreation, 
                    BitNumber = 0, 
                    IsEnabled = true 
                },
                new() 
                { 
                    SensorId = 2, 
                    SensorName = "摆轮1前感应IO", 
                    IoType = SensorIoType.WheelFront, 
                    BitNumber = 1, 
                    BoundWheelDiverterId = 1,  // 绑定到摆轮ID=1（long类型）
                    IsEnabled = true 
                },
                new() 
                { 
                    SensorId = 3, 
                    SensorName = "格口1锁格感应IO", 
                    IoType = SensorIoType.ChuteLock, 
                    BitNumber = 2, 
                    BoundChuteId = 1,  // 绑定到格口ID=1（long类型）
                    IsEnabled = true 
                }
            },
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (Sensors != null)
        {
            // 检查SensorId不能重复
            var duplicateIds = Sensors
                .GroupBy(s => s.SensorId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                return (false, $"感应IO ID重复: {string.Join(", ", duplicateIds)}");
            }

            // 检查只能有一个激活的 ParcelCreation 类型感应IO
            var activeParcelCreationSensors = Sensors
                .Where(s => s.IoType == SensorIoType.ParcelCreation && s.IsEnabled)
                .ToList();

            if (activeParcelCreationSensors.Count > 1)
            {
                return (false, $"只能有一个激活的创建包裹感应IO，当前有 {activeParcelCreationSensors.Count} 个: {string.Join(", ", activeParcelCreationSensors.Select(s => s.SensorName ?? s.SensorId.ToString()))}");
            }

            // 检查 WheelFront 类型必须绑定摆轮节点
            var wheelFrontWithoutBinding = Sensors
                .Where(s => s.IoType == SensorIoType.WheelFront && s.IsEnabled && !s.BoundWheelDiverterId.HasValue)
                .ToList();

            if (wheelFrontWithoutBinding.Any())
            {
                return (false, $"摆轮前感应IO必须绑定摆轮节点: {string.Join(", ", wheelFrontWithoutBinding.Select(s => s.SensorName ?? s.SensorId.ToString()))}");
            }

            // 检查 ChuteLock 类型必须绑定格口
            var chuteLockWithoutBinding = Sensors
                .Where(s => s.IoType == SensorIoType.ChuteLock && s.IsEnabled && !s.BoundChuteId.HasValue)
                .ToList();

            if (chuteLockWithoutBinding.Any())
            {
                return (false, $"锁格感应IO必须绑定格口: {string.Join(", ", chuteLockWithoutBinding.Select(s => s.SensorName ?? s.SensorId.ToString()))}");
            }
        }

        return (true, null);
    }
}

/// <summary>
/// 感应IO配置条目
/// </summary>
/// <remarks>
/// 定义单个感应IO的逻辑配置，包括业务类型和关联的IO点位。
/// IO点位的具体硬件映射由 IO驱动器配置（/api/config/io-driver）定义。
/// </remarks>
public class SensorIoEntry
{
    /// <summary>
    /// 感应IO标识符（数字ID）
    /// </summary>
    public required long SensorId { get; set; }

    /// <summary>
    /// 感应IO名称（可选）
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "创建包裹感应IO"、"摆轮1前感应IO"
    /// </remarks>
    public string? SensorName { get; set; }

    /// <summary>
    /// 感应IO类型（业务功能分类）
    /// </summary>
    /// <remarks>
    /// 按业务功能分为三种类型：
    /// - ParcelCreation: 创建包裹感应IO（只能同时存在一个激活的）
    /// - WheelFront: 摆轮前感应IO（与摆轮 frontIoId 关联）
    /// - ChuteLock: 锁格感应IO
    /// </remarks>
    public required SensorIoType IoType { get; set; }

    /// <summary>
    /// IO端口编号（0-1023，对应硬件IO点位）
    /// </summary>
    /// <remarks>
    /// 此编号对应 IO驱动器配置中定义的输入IO点位。
    /// 具体的硬件映射（如雷赛控制卡的 InputBit）由 IO驱动器配置管理。
    /// 与 IoLinkagePoint.BitNumber 命名保持一致。
    /// </remarks>
    [Required(ErrorMessage = "IO端口编号不能为空")]
    [Range(0, 1023, ErrorMessage = "IO端口编号必须在 0-1023 之间")]
    public required int BitNumber { get; set; }

    /// <summary>
    /// 绑定的摆轮节点ID（仅当 IoType 为 WheelFront 时使用）
    /// </summary>
    /// <remarks>
    /// WheelFront 类型的感应IO必须绑定一个摆轮节点，
    /// 用于在包裹到达摆轮前触发摆轮提前动作。
    /// 使用 long 类型的 DiverterId 进行匹配，符合项目ID匹配规范。
    /// </remarks>
    public long? BoundWheelDiverterId { get; set; }

    /// <summary>
    /// 绑定的格口ID（仅当 IoType 为 ChuteLock 时使用）
    /// </summary>
    /// <remarks>
    /// ChuteLock 类型的感应IO必须绑定一个格口，
    /// 用于确认包裹已成功落入目标格口。
    /// 使用 long 类型的 ChuteId 进行匹配，符合项目ID匹配规范。
    /// </remarks>
    public long? BoundChuteId { get; set; }

    /// <summary>
    /// IO触发电平配置（高电平有效/低电平有效）
    /// </summary>
    /// <remarks>
    /// 默认值：ActiveHigh（高电平有效）
    /// - ActiveHigh: 高电平有效（常开按键）
    /// - ActiveLow: 低电平有效（常闭按键）
    /// </remarks>
    public TriggerLevel TriggerLevel { get; set; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 传感器轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>设置此传感器的独立轮询周期。如果为 null，则使用全局默认值 (SensorOptions.PollingIntervalMs = 10ms)。</para>
    /// <para>**建议范围**：5ms - 50ms</para>
    /// <list type="bullet">
    /// <item>5-10ms: 高精度检测，适用于快速移动的包裹</item>
    /// <item>10-20ms: 标准精度，平衡检测精度和CPU占用（推荐）</item>
    /// <item>20-50ms: 降低CPU占用，适用于低速场景</item>
    /// </list>
    /// </remarks>
    /// <example>10</example>
    public int? PollingIntervalMs { get; set; }

    /// <summary>
    /// 防抖/去重时间窗口（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>在此时间窗口内，同一传感器的重复触发将被检测并标记为异常。</para>
    /// <para>如果为 null，则使用全局默认值 (1000ms)。</para>
    /// <para>**建议范围**：500ms - 3000ms</para>
    /// <list type="bullet">
    /// <item>500-1000ms: 快速响应，适用于高速分拣场景</item>
    /// <item>1000-2000ms: 标准防抖，平衡误触发和响应速度（推荐）</item>
    /// <item>2000-3000ms: 强防抖，适用于传感器不稳定的场景</item>
    /// </list>
    /// </remarks>
    /// <example>1000</example>
    public int? DeduplicationWindowMs { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
