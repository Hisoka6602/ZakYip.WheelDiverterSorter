using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Validation;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 控制面板输入点位配置（控制按钮）。
/// </summary>
public sealed record class CabinetInputPoint
{
    /// <summary>
    /// 急停按键输入位编号列表。
    /// </summary>
    /// <remarks>
    /// 系统可能有多个急停按钮，任意一个处于按下状态时，系统状态都为急停。
    /// 每个位编号必须在 -1 到 1000 之间。使用 -1 表示该位置禁用。空列表表示不启用急停功能。
    /// <para>
    /// <b>迁移说明：</b> 原有 EmergencyStop (int) 字段已废弃，请使用 EmergencyStopButtons 列表。
    /// 对于仅有一个急停按钮的系统，可将原 EmergencyStop 值放入列表中。
    /// 可使用 <see cref="FromLegacyEmergencyStop"/> 方法进行迁移。
    /// </para>
    /// </remarks>
    [ValidateCollectionItems(ConfigurationDefaults.CabinetInput.MinIoBitNumber, 
        ConfigurationDefaults.CabinetInput.MaxIoBitNumber, 
        ErrorMessage = "急停按钮位编号必须在 -1 到 1000 之间")]
    public List<int> EmergencyStopButtons { get; init; } = new();

    /// <summary>停止按键输入位编号，-1 表示禁用。</summary>
    [Range(-1, 1000, ErrorMessage = "输入位编号必须在 -1 到 1000 之间")]
    public int Stop { get; init; } = -1;

    /// <summary>启动按键输入位编号，-1 表示禁用。</summary>
    [Range(-1, 1000, ErrorMessage = "输入位编号必须在 -1 到 1000 之间")]
    public int Start { get; init; } = -1;

    /// <summary>复位按键输入位编号，-1 表示禁用。</summary>
    [Range(-1, 1000, ErrorMessage = "输入位编号必须在 -1 到 1000 之间")]
    public int Reset { get; init; } = -1;

    /// <summary>远程/本地模式切换输入位编号，-1 表示禁用。</summary>
    [Range(-1, 1000, ErrorMessage = "输入位编号必须在 -1 到 1000 之间")]
    public int RemoteLocalMode { get; init; } = -1;

    /// <summary>急停按键触发电平配置：ActiveHigh=高电平触发（常开按键），ActiveLow=低电平触发（常闭按键）。</summary>
    public TriggerLevel EmergencyStopTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>停止按键触发电平配置：ActiveHigh=高电平触发，ActiveLow=低电平触发。</summary>
    public TriggerLevel StopTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>启动按键触发电平配置：ActiveHigh=高电平触发，ActiveLow=低电平触发。</summary>
    public TriggerLevel StartTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>复位按键触发电平配置：ActiveHigh=高电平触发，ActiveLow=低电平触发。</summary>
    public TriggerLevel ResetTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>远程/本地模式触发电平配置：ActiveHigh=高电平触发，ActiveLow=低电平触发。</summary>
    public TriggerLevel RemoteLocalTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>远程/本地模式高电平对应的模式：true=高电平为远程模式，false=高电平为本地模式。</summary>
    public bool RemoteLocalActiveHigh { get; init; } = true;
    
    /// <summary>
    /// 从旧 EmergencyStop 字段迁移到 EmergencyStopButtons 列表。
    /// Migrates from the legacy EmergencyStop field to the EmergencyStopButtons list.
    /// </summary>
    /// <param name="emergencyStopBit">原 EmergencyStop 按键输入位编号。-1 表示禁用。</param>
    /// <returns>包含迁移后急停按钮列表的 CabinetInputPoint 实例。</returns>
    /// <example>
    /// <code>
    /// // 迁移单个急停按钮
    /// var config = CabinetInputPoint.FromLegacyEmergencyStop(5);
    /// // config.EmergencyStopButtons 将包含 [5]
    /// 
    /// // 迁移禁用状态
    /// var configDisabled = CabinetInputPoint.FromLegacyEmergencyStop(-1);
    /// // configDisabled.EmergencyStopButtons 将是空列表 []
    /// </code>
    /// </example>
    public static CabinetInputPoint FromLegacyEmergencyStop(int emergencyStopBit)
    {
        return new CabinetInputPoint
        {
            EmergencyStopButtons = emergencyStopBit >= 0 
                ? new List<int> { emergencyStopBit } 
                : new List<int>()
        };
    }
}
