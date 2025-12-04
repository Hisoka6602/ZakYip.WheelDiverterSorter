namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

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
    
    /// <summary>
    /// 面板指示灯配置默认值
    /// Panel indicator configuration defaults
    /// </summary>
    public static class CabinetIndicator
    {
        /// <summary>急停蜂鸣器默认鸣叫时长（秒）</summary>
        public const int DefaultEmergencyStopBuzzerDurationSeconds = 5;
        
        /// <summary>急停蜂鸣器最大鸣叫时长（秒）</summary>
        public const int MaxEmergencyStopBuzzerDurationSeconds = 300;
    }
    
    /// <summary>
    /// 面板输入点位配置默认值
    /// Panel input point configuration defaults
    /// </summary>
    public static class CabinetInput
    {
        /// <summary>IO位编号的最小有效值（-1表示禁用）</summary>
        public const int MinIoBitNumber = -1;
        
        /// <summary>IO位编号的最大有效值</summary>
        public const int MaxIoBitNumber = 1000;
    }
}
