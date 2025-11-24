namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 配置默认值常量
/// Configuration default value constants
/// </summary>
internal static class ConfigurationDefaults
{
    /// <summary>
    /// 默认时间戳，用于测试场景中 ISystemClock 为 null 时的回退值
    /// Default timestamp for fallback when ISystemClock is null in test scenarios
    /// </summary>
    public static readonly DateTime DefaultTimestamp = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
}
