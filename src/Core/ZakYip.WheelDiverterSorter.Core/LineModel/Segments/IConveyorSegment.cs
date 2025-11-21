using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Segments;

/// <summary>
/// 中段皮带段接口。
/// 定义单个中段皮带段的核心行为，不依赖具体 IO 实现。
/// </summary>
public interface IConveyorSegment
{
    /// <summary>
    /// 皮带段标识
    /// </summary>
    ConveyorSegmentId SegmentId { get; }

    /// <summary>
    /// 当前运行状态
    /// </summary>
    ConveyorSegmentState State { get; }

    /// <summary>
    /// 启动皮带段
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    ValueTask<ConveyorOperationResult> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止皮带段
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    ValueTask<ConveyorOperationResult> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前故障信息（如果有）
    /// </summary>
    /// <returns>故障信息，无故障则返回 null</returns>
    string? GetFaultInfo();
}
