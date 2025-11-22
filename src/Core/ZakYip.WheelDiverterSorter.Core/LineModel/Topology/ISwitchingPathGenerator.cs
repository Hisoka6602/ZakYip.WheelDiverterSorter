namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology;

/// <summary>
/// 摆轮路径生成器接口，负责根据目标格口生成摆轮指令序列
/// </summary>
public interface ISwitchingPathGenerator
{
    /// <summary>
    /// 根据目标格口生成摆轮路径
    /// </summary>
    /// <param name="targetChuteId">目标格口标识（数字ID）</param>
    /// <returns>
    /// 生成的摆轮路径，如果目标格口无法映射到任意摆轮组合则返回null。
    /// 当返回null时，包裹将走异常口处理流程。
    /// </returns>
    SwitchingPath? GeneratePath(long targetChuteId);
}
