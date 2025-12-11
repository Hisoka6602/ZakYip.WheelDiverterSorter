using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 表示分拣方向的枚举（用于拓扑模型和配置）
/// </summary>
/// <remarks>
/// <para>⚠️ 注意：此枚举与 <see cref="DiverterDirection"/> 语义相同但用途不同：</para>
/// <list type="bullet">
/// <item><description><b>DiverterSide</b>：用于拓扑模型和高层配置（格口映射、拓扑定义）</description></item>
/// <item><description><b>DiverterDirection</b>：用于硬件驱动层和底层控制（摆轮指令、路径执行）</description></item>
/// </list>
/// <para>两个枚举保持独立是为了分离拓扑模型与硬件实现的关注点。</para>
/// <para>相关技术债：TD-063 - 待评估是否可以合并为单一枚举。</para>
/// </remarks>
public enum DiverterSide
{
    /// <summary>
    /// 直行
    /// </summary>
    [Description("直行")]
    Straight = 0,

    /// <summary>
    /// 左转
    /// </summary>
    [Description("左转")]
    Left = 1,

    /// <summary>
    /// 右转
    /// </summary>
    [Description("右转")]
    Right = 2
}
