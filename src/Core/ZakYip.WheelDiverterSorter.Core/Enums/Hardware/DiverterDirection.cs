using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 表示摆轮转向方向的枚举（用于硬件驱动和底层控制）
/// </summary>
/// <remarks>
/// <para>在直线拓扑结构中，摆轮只有转向方向，不存在具体的转向角度。</para>
/// <para>每个摆轮可以将包裹分流到左侧格口、右侧格口，或者让包裹直行通过。</para>
/// <para>⚠️ 注意：此枚举与 <see cref="DiverterSide"/> 语义相同但用途不同：</para>
/// <list type="bullet">
/// <item><description><b>DiverterDirection</b>：用于硬件驱动层和底层控制（摆轮指令、路径执行）</description></item>
/// <item><description><b>DiverterSide</b>：用于拓扑模型和高层配置（格口映射、拓扑定义）</description></item>
/// </list>
/// <para>两个枚举保持独立是为了分离拓扑模型与硬件实现的关注点。</para>
/// <para>相关技术债：TD-063 - 待评估是否可以合并为单一枚举。</para>
/// </remarks>
public enum DiverterDirection
{
    /// <summary>
    /// 直行通过
    /// </summary>
    [Description("直行")]
    Straight = 0,

    /// <summary>
    /// 转向左侧格口
    /// </summary>
    [Description("左")]
    Left = 1,

    /// <summary>
    /// 转向右侧格口
    /// </summary>
    [Description("右")]
    Right = 2
}
