using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// 面板输入读取器接口。
/// 负责从硬件或仿真中读取面板按钮状态。
/// </summary>
public interface IPanelInputReader
{
    /// <summary>
    /// 读取指定按钮的当前状态。
    /// </summary>
    /// <param name="buttonType">按钮类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按钮状态</returns>
    Task<PanelButtonState> ReadButtonStateAsync(PanelButtonType buttonType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取所有按钮的当前状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有按钮状态的字典</returns>
    Task<IDictionary<PanelButtonType, PanelButtonState>> ReadAllButtonStatesAsync(CancellationToken cancellationToken = default);
}
