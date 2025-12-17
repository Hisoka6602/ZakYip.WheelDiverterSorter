using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

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
    /// 配置ID（由持久化层自动生成）
    /// </summary>
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
    /// 就绪状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 例如：系统就绪时将某些 IO 设置为特定电平以指示设备已准备好
    /// </remarks>
    public List<IoLinkagePoint> ReadyStateIos { get; init; } = new();

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
    /// 急停状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 例如：急停时将某些 IO 设置为特定电平以紧急停止设备
    /// </remarks>
    public List<IoLinkagePoint> EmergencyStopStateIos { get; init; } = new();

    /// <summary>
    /// 上游连接异常状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 例如：上游连接异常时将某些 IO 设置为特定电平以告警
    /// </remarks>
    public List<IoLinkagePoint> UpstreamConnectionExceptionStateIos { get; init; } = new();

    /// <summary>
    /// 摆轮异常状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 例如：摆轮异常时将某些 IO 设置为特定电平以告警
    /// </remarks>
    public List<IoLinkagePoint> DiverterExceptionStateIos { get; init; } = new();

    /// <summary>
    /// 运行前预警结束后联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 当系统按下启动按钮后，先进行运行前预警（preStartWarning），
    /// 等待 durationSeconds 秒后，预警结束，此时触发这些 IO 点。
    /// 用于在预警结束后通知外部设备可以开始工作。
    /// </remarks>
    public List<IoLinkagePoint> PostPreStartWarningStateIos { get; init; } = new();

    /// <summary>
    /// 摆轮断联/异常状态时联动的 IO 点列表
    /// </summary>
    /// <remarks>
    /// 当摆轮首次连接成功后，如果摆轮断联或发生异常，将触发这些 IO 点。
    /// 用于通知外部设备摆轮出现连接问题或异常情况。
    /// 注意：只有在摆轮首次连接成功后才会触发此联动。
    /// </remarks>
    public List<IoLinkagePoint> WheelDiverterDisconnectedStateIos { get; init; } = new();

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 获取默认 IO 联动配置
    /// </summary>
    /// <param name="systemClock">系统时钟（可选，测试时可为 null 使用固定时间）</param>
    /// <returns>默认配置实例</returns>
    public static IoLinkageConfiguration GetDefault(ISystemClock? systemClock = null)
    {
        var now = systemClock?.LocalNow ?? ConfigurationDefaults.DefaultTimestamp;
        return new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = true,
            ReadyStateIos = new List<IoLinkagePoint>(),
            RunningStateIos = new List<IoLinkagePoint>(),
            StoppedStateIos = new List<IoLinkagePoint>(),
            EmergencyStopStateIos = new List<IoLinkagePoint>(),
            UpstreamConnectionExceptionStateIos = new List<IoLinkagePoint>(),
            DiverterExceptionStateIos = new List<IoLinkagePoint>(),
            PostPreStartWarningStateIos = new List<IoLinkagePoint>(),
            WheelDiverterDisconnectedStateIos = new List<IoLinkagePoint>(),
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
        // 验证就绪状态 IO 点
        foreach (var ioPoint in ReadyStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"就绪状态 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"就绪状态 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

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

        // 验证急停状态 IO 点
        foreach (var ioPoint in EmergencyStopStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"急停状态 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"急停状态 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

        // 验证上游连接异常状态 IO 点
        foreach (var ioPoint in UpstreamConnectionExceptionStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"上游连接异常状态 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"上游连接异常状态 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

        // 验证摆轮异常状态 IO 点
        foreach (var ioPoint in DiverterExceptionStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"摆轮异常状态 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"摆轮异常状态 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

        // 检查重复的 IO 点
        var readyBits = ReadyStateIos.Select(io => io.BitNumber).ToList();
        var duplicateReadyBits = readyBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateReadyBits.Any())
        {
            return (false, $"就绪状态 IO 点存在重复: {string.Join(", ", duplicateReadyBits)}");
        }

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

        var emergencyStopBits = EmergencyStopStateIos.Select(io => io.BitNumber).ToList();
        var duplicateEmergencyStopBits = emergencyStopBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateEmergencyStopBits.Any())
        {
            return (false, $"急停状态 IO 点存在重复: {string.Join(", ", duplicateEmergencyStopBits)}");
        }

        var upstreamExceptionBits = UpstreamConnectionExceptionStateIos.Select(io => io.BitNumber).ToList();
        var duplicateUpstreamExceptionBits = upstreamExceptionBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateUpstreamExceptionBits.Any())
        {
            return (false, $"上游连接异常状态 IO 点存在重复: {string.Join(", ", duplicateUpstreamExceptionBits)}");
        }

        var diverterExceptionBits = DiverterExceptionStateIos.Select(io => io.BitNumber).ToList();
        var duplicateDiverterExceptionBits = diverterExceptionBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateDiverterExceptionBits.Any())
        {
            return (false, $"摆轮异常状态 IO 点存在重复: {string.Join(", ", duplicateDiverterExceptionBits)}");
        }

        // 验证运行前预警结束后 IO 点
        foreach (var ioPoint in PostPreStartWarningStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"运行前预警结束后 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"运行前预警结束后 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

        var postPreStartWarningBits = PostPreStartWarningStateIos.Select(io => io.BitNumber).ToList();
        var duplicatePostPreStartWarningBits = postPreStartWarningBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicatePostPreStartWarningBits.Any())
        {
            return (false, $"运行前预警结束后 IO 点存在重复: {string.Join(", ", duplicatePostPreStartWarningBits)}");
        }

        // 验证摆轮断联/异常 IO 点
        foreach (var ioPoint in WheelDiverterDisconnectedStateIos)
        {
            if (ioPoint.BitNumber < 0 || ioPoint.BitNumber > 1023)
            {
                return (false, $"摆轮断联/异常 IO 点 {ioPoint.BitNumber} 必须在 0-1023 范围内");
            }

            if (!Enum.IsDefined(typeof(TriggerLevel), ioPoint.Level))
            {
                return (false, $"摆轮断联/异常 IO 点 {ioPoint.BitNumber} 的电平配置无效");
            }
        }

        var wheelDiverterDisconnectedBits = WheelDiverterDisconnectedStateIos.Select(io => io.BitNumber).ToList();
        var duplicateWheelDiverterDisconnectedBits = wheelDiverterDisconnectedBits.GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateWheelDiverterDisconnectedBits.Any())
        {
            return (false, $"摆轮断联/异常 IO 点存在重复: {string.Join(", ", duplicateWheelDiverterDisconnectedBits)}");
        }

        return (true, null);
    }
}
