using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 信号塔输出接口。
/// 负责向硬件或仿真发送三色灯/蜂鸣器控制指令。
/// </summary>
public interface ISignalTowerOutput
{
    /// <summary>
    /// 设置单个通道的状态。
    /// </summary>
    /// <param name="state">通道状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetChannelStateAsync(SignalTowerState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量设置多个通道的状态。
    /// </summary>
    /// <param name="states">通道状态集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetChannelStatesAsync(IEnumerable<SignalTowerState> states, CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭所有通道。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task TurnOffAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前所有通道的状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有通道状态的字典</returns>
    Task<IDictionary<SignalTowerChannel, SignalTowerState>> GetAllChannelStatesAsync(CancellationToken cancellationToken = default);
}
