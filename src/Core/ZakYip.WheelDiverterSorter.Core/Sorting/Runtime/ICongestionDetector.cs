using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Runtime;

/// <summary>
/// 拥堵检测器：基于实时指标推断当前拥堵等级。
/// </summary>
public interface ICongestionDetector
{
    /// <summary>
    /// 计算当前拥堵等级。
    /// </summary>
    /// <param name="snapshot">当前指标快照。</param>
    /// <returns>拥堵等级。</returns>
    CongestionLevel Detect(in CongestionSnapshot snapshot);
}
