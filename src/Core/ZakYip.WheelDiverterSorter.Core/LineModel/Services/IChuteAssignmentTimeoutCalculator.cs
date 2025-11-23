namespace ZakYip.WheelDiverterSorter.Core.LineModel.Services;

/// <summary>
/// 格口分配超时计算器接口
/// </summary>
/// <remarks>
/// 根据线体拓扑配置（入口到第一个摆轮前决策传感器的距离和速度）
/// 动态计算包裹等待上游格口分配的超时时间
/// </remarks>
public interface IChuteAssignmentTimeoutCalculator
{
    /// <summary>
    /// 计算包裹等待上游格口分配的超时时间（秒）
    /// </summary>
    /// <param name="context">超时计算上下文</param>
    /// <returns>超时时间（秒），如果计算失败则返回降级默认值</returns>
    decimal CalculateTimeoutSeconds(ChuteAssignmentTimeoutContext context);
}

/// <summary>
/// 格口分配超时计算上下文
/// </summary>
/// <param name="LineId">包裹所在线体ID</param>
/// <param name="SafetyFactor">安全系数（0.1-1.0），默认0.9</param>
public readonly record struct ChuteAssignmentTimeoutContext(
    long LineId,
    decimal SafetyFactor = 0.9m
);
