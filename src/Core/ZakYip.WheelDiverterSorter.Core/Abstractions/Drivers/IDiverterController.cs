namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

/// <summary>
/// 摆轮控制器接口
/// </summary>
/// <remarks>
/// 本接口属于 Core 层，定义摆轮角度控制的抽象契约。
/// 用于控制单个摆轮的角度，由 Drivers 层实现。
/// </remarks>
public interface IDiverterController
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    string DiverterId { get; }

    /// <summary>
    /// 设置摆轮角度
    /// </summary>
    /// <param name="angle">目标角度（0, 30, 45, 90度等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功设置</returns>
    Task<bool> SetAngleAsync(int angle, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前摆轮角度
    /// </summary>
    /// <returns>当前角度</returns>
    Task<int> GetCurrentAngleAsync();

    /// <summary>
    /// 复位摆轮到初始位置（通常是0度）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功复位</returns>
    Task<bool> ResetAsync(CancellationToken cancellationToken = default);
}
