using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using LiteDB;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 电柜操作面板完整配置模型
/// </summary>
/// <remarks>
/// 包含面板所有按钮和指示灯的IO绑定及触发电平配置
/// </remarks>
public sealed record class PanelConfiguration
{
    /// <summary>
    /// 配置ID（LiteDB自动生成）
    /// </summary>
    [BsonId]
    public int Id { get; init; }

    /// <summary>
    /// 配置名称（固定为"panel"）
    /// </summary>
    public string ConfigName { get; init; } = "panel";

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// 是否启用面板功能
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 是否使用仿真模式
    /// </summary>
    /// <remarks>
    /// true: 使用仿真驱动（SimulatedPanelInputReader / SimulatedSignalTowerOutput）
    /// false: 使用真实硬件驱动
    /// </remarks>
    public bool UseSimulation { get; init; } = true;

    /// <summary>
    /// 面板按钮轮询间隔（毫秒）
    /// </summary>
    /// <remarks>有效范围：50-1000 毫秒</remarks>
    public int PollingIntervalMs { get; init; } = 100;

    /// <summary>
    /// 按钮防抖时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 在此时间内的重复触发将被忽略
    /// 有效范围：10-500 毫秒
    /// </remarks>
    public int DebounceMs { get; init; } = 50;

    // ========== 按钮输入配置 ==========

    /// <summary>
    /// 开始按钮 IO 绑定（输入位）
    /// </summary>
    public int? StartButtonInputBit { get; init; }

    /// <summary>
    /// 开始按钮 IO 触发电平配置
    /// </summary>
    public TriggerLevel StartButtonTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 停止按钮 IO 绑定（输入位）
    /// </summary>
    public int? StopButtonInputBit { get; init; }

    /// <summary>
    /// 停止按钮 IO 触发电平配置
    /// </summary>
    public TriggerLevel StopButtonTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 急停按钮 IO 绑定（输入位）
    /// </summary>
    public int? EmergencyStopButtonInputBit { get; init; }

    /// <summary>
    /// 急停按钮 IO 触发电平配置
    /// </summary>
    public TriggerLevel EmergencyStopButtonTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    // ========== 指示灯输出配置 ==========

    /// <summary>
    /// 开始按钮灯 IO 绑定（输出位）
    /// </summary>
    public int? StartLightOutputBit { get; init; }

    /// <summary>
    /// 开始按钮灯 IO 输出电平配置
    /// </summary>
    public TriggerLevel StartLightOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 停止按钮灯 IO 绑定（输出位）
    /// </summary>
    public int? StopLightOutputBit { get; init; }

    /// <summary>
    /// 停止按钮灯 IO 输出电平配置
    /// </summary>
    public TriggerLevel StopLightOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 连接按钮灯 IO 绑定（输出位）
    /// </summary>
    public int? ConnectionLightOutputBit { get; init; }

    /// <summary>
    /// 连接按钮灯 IO 输出电平配置
    /// </summary>
    public TriggerLevel ConnectionLightOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    // ========== 三色信号塔输出配置 ==========

    /// <summary>
    /// 三色灯红色 IO 绑定（输出位）
    /// </summary>
    public int? SignalTowerRedOutputBit { get; init; }

    /// <summary>
    /// 三色灯红色 IO 输出电平配置
    /// </summary>
    public TriggerLevel SignalTowerRedOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 三色灯黄色 IO 绑定（输出位）
    /// </summary>
    public int? SignalTowerYellowOutputBit { get; init; }

    /// <summary>
    /// 三色灯黄色 IO 输出电平配置
    /// </summary>
    public TriggerLevel SignalTowerYellowOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 三色灯绿色 IO 绑定（输出位）
    /// </summary>
    public int? SignalTowerGreenOutputBit { get; init; }

    /// <summary>
    /// 三色灯绿色 IO 输出电平配置
    /// </summary>
    public TriggerLevel SignalTowerGreenOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    // ========== 运行前预警配置 ==========

    /// <summary>
    /// 运行前预警持续时间（秒）
    /// </summary>
    /// <remarks>
    /// 按下电柜面板启动按钮时，先触发预警输出（如红灯闪烁）持续N秒，
    /// 然后才真正开始运行。目的是告诉现场人员离开设备，避免安全事故。
    /// </remarks>
    public int? PreStartWarningDurationSeconds { get; init; }

    /// <summary>
    /// 运行前预警输出 IO 绑定（输出位）
    /// </summary>
    /// <remarks>
    /// 用于绑定运行前预警输出（如红灯）的 IO 地址
    /// </remarks>
    public int? PreStartWarningOutputBit { get; init; }

    /// <summary>
    /// 运行前预警输出 IO 电平配置
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public TriggerLevel PreStartWarningOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    // ========== 元数据 ==========

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 获取默认面板配置
    /// </summary>
    /// <param name="systemClock">系统时钟（可选，测试时可为 null 使用固定时间）</param>
    /// <returns>默认配置实例</returns>
    public static PanelConfiguration GetDefault(ISystemClock? systemClock = null)
    {
        var now = systemClock?.LocalNow ?? ConfigurationDefaults.DefaultTimestamp;
        return new PanelConfiguration
        {
            ConfigName = "panel",
            Version = 1,
            Enabled = false,
            UseSimulation = true,
            PollingIntervalMs = 100,
            DebounceMs = 50,
            StartButtonInputBit = null,
            StartButtonTriggerLevel = TriggerLevel.ActiveHigh,
            StopButtonInputBit = null,
            StopButtonTriggerLevel = TriggerLevel.ActiveHigh,
            EmergencyStopButtonInputBit = null,
            EmergencyStopButtonTriggerLevel = TriggerLevel.ActiveHigh,
            StartLightOutputBit = null,
            StartLightOutputLevel = TriggerLevel.ActiveHigh,
            StopLightOutputBit = null,
            StopLightOutputLevel = TriggerLevel.ActiveHigh,
            ConnectionLightOutputBit = null,
            ConnectionLightOutputLevel = TriggerLevel.ActiveHigh,
            SignalTowerRedOutputBit = null,
            SignalTowerRedOutputLevel = TriggerLevel.ActiveHigh,
            SignalTowerYellowOutputBit = null,
            SignalTowerYellowOutputLevel = TriggerLevel.ActiveHigh,
            SignalTowerGreenOutputBit = null,
            SignalTowerGreenOutputLevel = TriggerLevel.ActiveHigh,
            PreStartWarningDurationSeconds = null,
            PreStartWarningOutputBit = null,
            PreStartWarningOutputLevel = TriggerLevel.ActiveHigh,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 验证配置的有效性
    /// </summary>
    /// <returns>验证结果元组 (IsValid, ErrorMessage)</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (PollingIntervalMs < 50 || PollingIntervalMs > 1000)
        {
            return (false, "轮询间隔必须在 50-1000 毫秒之间");
        }

        if (DebounceMs < 10 || DebounceMs > 500)
        {
            return (false, "防抖时间必须在 10-500 毫秒之间");
        }

        if (DebounceMs >= PollingIntervalMs)
        {
            return (false, "防抖时间必须小于轮询间隔");
        }

        // 验证 IO 位范围 (0-1023)
        var ioBits = new[] 
        {
            StartButtonInputBit, StopButtonInputBit, EmergencyStopButtonInputBit,
            StartLightOutputBit, StopLightOutputBit, ConnectionLightOutputBit,
            SignalTowerRedOutputBit, SignalTowerYellowOutputBit, SignalTowerGreenOutputBit,
            PreStartWarningOutputBit
        };

        foreach (var bit in ioBits)
        {
            if (bit.HasValue && (bit.Value < 0 || bit.Value > 1023))
            {
                return (false, $"IO位 {bit.Value} 必须在 0-1023 范围内");
            }
        }

        // 验证预警时间范围
        if (PreStartWarningDurationSeconds.HasValue)
        {
            if (PreStartWarningDurationSeconds.Value < 0 || PreStartWarningDurationSeconds.Value > 60)
            {
                return (false, "运行前预警时间必须在 0-60 秒之间");
            }
        }

        return (true, null);
    }
}
