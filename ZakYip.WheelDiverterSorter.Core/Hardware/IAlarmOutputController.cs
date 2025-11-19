namespace ZakYip.WheelDiverterSorter.Core.Hardware;

/// <summary>
/// 报警输出控制器接口（三色灯/蜂鸣器等）
/// Alarm Output Controller Interface - Controls signal towers, buzzers, etc.
/// </summary>
/// <remarks>
/// 此接口提供报警设备的控制能力，如三色灯、蜂鸣器。
/// 厂商驱动通过此接口暴露报警输出控制。
/// </remarks>
public interface IAlarmOutputController
{
    /// <summary>
    /// 设置红灯状态
    /// </summary>
    /// <param name="isOn">是否开启</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> SetRedLightAsync(bool isOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置黄灯状态
    /// </summary>
    /// <param name="isOn">是否开启</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> SetYellowLightAsync(bool isOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置绿灯状态
    /// </summary>
    /// <param name="isOn">是否开启</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> SetGreenLightAsync(bool isOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置蜂鸣器状态
    /// </summary>
    /// <param name="isOn">是否开启</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> SetBuzzerAsync(bool isOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置通用数字输出点状态
    /// </summary>
    /// <param name="outputPoint">输出点编号</param>
    /// <param name="isOn">是否开启</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> SetDigitalOutputAsync(int outputPoint, bool isOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 复位所有报警输出到默认状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作是否成功</returns>
    Task<bool> ResetAllAsync(CancellationToken cancellationToken = default);
}
