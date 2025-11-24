using LiteDB;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// IO 联动配置完整模型
/// </summary>
/// <remarks>
/// 定义在不同系统状态下要联动的 IO 端口组的配置。
/// 支持持久化存储和热更新。
/// </remarks>
public sealed record class IoLinkageConfiguration
{
    /// <summary>
    /// 配置ID（LiteDB自动生成）
    /// </summary>
    [BsonId]
    public int Id { get; init; }

    /// <summary>
    /// 配置名称（固定为"io_linkage"）
    /// </summary>
    public string ConfigName { get; init; } = "io_linkage";

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// 是否启用 IO 联动功能
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 运行中状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 例如：运行中时将某些 IO 设置为低电平以启动设备
    /// </remarks>
    public List<IoLinkagePoint> RunningStateIos { get; init; } = new();

    /// <summary>
    /// 停止/复位状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 例如：停止/复位时将某些 IO 设置为高电平以关闭设备
    /// </remarks>
    public List<IoLinkagePoint> StoppedStateIos { get; init; } = new();

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 获取默认 IO 联动配置
    /// </summary>
    /// <returns>默认配置实例</returns>
    public static IoLinkageConfiguration GetDefault()
    {
        return new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = true,
            RunningStateIos = new List<IoLinkagePoint>(),
            StoppedStateIos = new List<IoLinkagePoint>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 验证配置的有效性
    /// </summary>
    /// <returns>验证结果元组 (IsValid, ErrorMessage)</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        // 验证运行状态 IO 点
        foreach (var ioPoint in RunningStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"运行状态 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"运行状态 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

        // 验证停止状态 IO 点
        foreach (var ioPoint in StoppedStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"停止状态 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"停止状态 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

        // 检查重复的 IO 点
        var runningBits = RunningStateIos.Select(io => io.BitNumber).ToList();
        var duplicateRunningBits = runningBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateRunningBits.Any())
        {
            return (false, $"运行状态 IO 点存在重复: {string.Join(", ", duplicateRunningBits)}");
        }

        var stoppedBits = StoppedStateIos.Select(io => io.BitNumber).ToList();
        var duplicateStoppedBits = stoppedBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateStoppedBits.Any())
        {
            return (false, $"停止状态 IO 点存在重复: {string.Join(", ", duplicateStoppedBits)}");
        }

        return (true, null);
    }
}
