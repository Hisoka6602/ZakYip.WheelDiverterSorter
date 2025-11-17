using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 系统运行状态服务接口。
/// 负责管理系统运行状态转换和按钮事件处理。
/// </summary>
public interface ISystemRunStateService
{
    /// <summary>
    /// 获取当前系统运行状态
    /// </summary>
    SystemOperatingState Current { get; }

    /// <summary>
    /// 尝试处理启动按钮事件
    /// </summary>
    /// <returns>操作结果</returns>
    OperationResult TryHandleStart();

    /// <summary>
    /// 尝试处理停止按钮事件
    /// </summary>
    /// <returns>操作结果</returns>
    OperationResult TryHandleStop();

    /// <summary>
    /// 尝试处理急停按钮事件
    /// </summary>
    /// <returns>操作结果</returns>
    OperationResult TryHandleEmergencyStop();

    /// <summary>
    /// 尝试处理急停复位（急停解除）事件
    /// </summary>
    /// <returns>操作结果</returns>
    OperationResult TryHandleEmergencyReset();

    /// <summary>
    /// 验证当前状态是否允许创建包裹
    /// </summary>
    /// <returns>验证结果</returns>
    OperationResult ValidateParcelCreation();
}
