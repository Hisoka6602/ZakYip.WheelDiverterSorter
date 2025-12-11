namespace ZakYip.WheelDiverterSorter.Core.Enums.System;

/// <summary>
/// SystemState 扩展方法
/// </summary>
/// <remarks>
/// 提供 SystemState 的辅助方法和验证逻辑。
/// PR-FIX-SHADOW-ENUM: 统一系统状态管理，删除 SystemOperatingState 影分身。
/// </remarks>
public static class SystemStateExtensions
{
    /// <summary>
    /// 验证当前状态是否允许创建包裹
    /// </summary>
    /// <param name="state">当前系统状态</param>
    /// <returns>true 表示允许创建包裹</returns>
    /// <remarks>
    /// 只有 Running 状态才允许创建包裹。
    /// 此方法替代了原 ISystemRunStateService.ValidateParcelCreation() 的逻辑。
    /// </remarks>
    public static bool AllowsParcelCreation(this SystemState state)
    {
        return state == SystemState.Running;
    }

    /// <summary>
    /// 获取包裹创建被拒绝时的错误消息
    /// </summary>
    /// <param name="state">当前系统状态</param>
    /// <returns>中文错误消息</returns>
    public static string GetParcelCreationDeniedMessage(this SystemState state)
    {
        return state switch
        {
            SystemState.Booting => "系统当前正在启动，禁止创建包裹。",
            SystemState.Ready => "系统当前未处于运行状态，禁止创建包裹。当前状态: 就绪",
            SystemState.Paused => "系统当前处于暂停状态，禁止创建包裹。",
            SystemState.Faulted => "系统当前处于故障状态，禁止创建包裹。",
            SystemState.EmergencyStop => "系统当前处于急停状态，禁止创建包裹。",
            _ => $"系统当前状态 {state} 不允许创建包裹。"
        };
    }

    /// <summary>
    /// 验证当前状态是否允许启动系统
    /// </summary>
    /// <param name="state">当前系统状态</param>
    /// <returns>true 表示允许启动</returns>
    public static bool AllowsStart(this SystemState state)
    {
        return state is SystemState.Ready or SystemState.Paused;
    }

    /// <summary>
    /// 验证当前状态是否允许停止系统
    /// </summary>
    /// <param name="state">当前系统状态</param>
    /// <returns>true 表示允许停止</returns>
    public static bool AllowsStop(this SystemState state)
    {
        return state is SystemState.Running or SystemState.Paused;
    }
}
