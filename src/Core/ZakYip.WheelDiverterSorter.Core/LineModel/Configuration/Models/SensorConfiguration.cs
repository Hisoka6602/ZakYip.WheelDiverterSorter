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
/// - **绑定关系通过拓扑配置管理**：
///   - WheelFront 传感器通过 DiverterPathNode.FrontSensorId 绑定到摆轮
///   - 不需要在此配置中指定 boundWheelDiverterId 或 boundChuteId
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
                    IsEnabled = true 
                },
                new() 
                { 
                    SensorId = 3, 
                    SensorName = "格口1锁格感应IO", 
                    IoType = SensorIoType.ChuteLock, 
                    BitNumber = 2, 
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

            // Note: WheelFront and ChuteLock binding is now managed via topology configuration
            // WheelFront sensors are bound via DiverterPathNode.FrontSensorId
            // ChuteLock sensors can be configured separately if needed
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
/// 
/// **绑定关系**：
/// - WheelFront 传感器与摆轮的绑定通过拓扑配置的 DiverterPathNode.FrontSensorId 定义
/// - 不需要在此配置中指定 boundWheelDiverterId 或 boundChuteId
/// </remarks>
public class SensorIoEntry
{
    /// <summary>
    /// 默认防抖窗口时间（毫秒）
    /// </summary>
    private const int DefaultDeduplicationWindowMs = 400;
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
    /// - WheelFront: 摆轮前感应IO（通过拓扑配置的 frontSensorId 关联到摆轮）
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
    /// 重复触发判定窗口（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>设置此传感器的防抖时间窗口。在此时间窗口内，同一传感器的重复触发将被完全忽略（不创建包裹）。</para>
    /// <para>**默认值**: 400ms</para>
    /// <para>**建议范围**：100ms - 2000ms</para>
    /// <list type="bullet">
    /// <item>100-400ms: 高速分拣场景（快速移动的包裹，默认推荐）</item>
    /// <item>400-1000ms: 标准速度（平衡防抖和灵敏度）</item>
    /// <item>1000-2000ms: 低速场景或机械抖动明显的传感器</item>
    /// </list>
    /// <para>**与 StateChangeIgnoreWindowMs 的区别**：</para>
    /// <list type="bullet">
    /// <item>DeduplicationWindowMs: 防止同一个包裹的多次重复检测（只检测上升沿）</item>
    /// <item>StateChangeIgnoreWindowMs: 处理镂空包裹的多次状态变化（忽略所有状态变化）</item>
    /// </list>
    /// </remarks>
    /// <example>400</example>
    [Range(100, 5000, ErrorMessage = "重复触发判定窗口必须在 100-5000ms 之间")]
    public int DeduplicationWindowMs { get; set; } = DefaultDeduplicationWindowMs;

    /// <summary>
    /// 状态变化忽略窗口（毫秒）
    /// </summary>
    /// <remarks>
    /// <para>设置此传感器的状态变化忽略时间窗口。在首次上升沿触发后的此时间窗口内，所有状态变化（包括上升沿和下降沿）都将被忽略。</para>
    /// <para>**应用场景**: 镂空包裹经过传感器时，会触发多次上升沿/下降沿变化，导致被误认为多个包裹。</para>
    /// <para>**默认值**: 0（禁用，不忽略状态变化）</para>
    /// <para>**建议范围**：0ms - 500ms</para>
    /// <list type="bullet">
    /// <item>0ms: 禁用状态变化忽略（默认，适用于实心包裹）</item>
    /// <item>50-100ms: 小型镂空包裹（镂空间隙较小）</item>
    /// <item>100-300ms: 中型镂空包裹（常见场景，推荐）</item>
    /// <item>300-500ms: 大型镂空包裹（镂空间隙较大或移动速度较慢）</item>
    /// </list>
    /// <para>**工作原理**: 在传感器首次触发（上升沿）后，启动忽略窗口。在窗口内的所有状态变化（上升沿、下降沿）都会被忽略，防止镂空部分被误判为多个包裹。</para>
    /// <para>**与 DeduplicationWindowMs 的区别**：</para>
    /// <list type="bullet">
    /// <item>DeduplicationWindowMs: 防止同一个包裹的多次重复检测（只检测上升沿，在窗口内忽略重复上升沿）</item>
    /// <item>StateChangeIgnoreWindowMs: 处理镂空包裹的多次状态变化（在窗口内忽略所有状态变化，包括上升沿和下降沿）</item>
    /// </list>
    /// </remarks>
    /// <example>0</example>
    [Range(0, 500, ErrorMessage = "状态变化忽略窗口必须在 0-500ms 之间")]
    public int StateChangeIgnoreWindowMs { get; set; } = 0;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
