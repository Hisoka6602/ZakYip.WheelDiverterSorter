namespace ZakYip.WheelDiverterSorter.Core.Hardware.Devices;

/// <summary>
/// EMC控制器操作接口
/// </summary>
/// <remarks>
/// 本接口属于 HAL（硬件抽象层），定义 EMC 硬件控制的抽象契约。
/// 提供对EMC硬件的基本操作，包括重置功能，由 Drivers 层实现。
/// </remarks>
public interface IEmcController
{
    /// <summary>
    /// EMC卡号
    /// </summary>
    ushort CardNo { get; }
    
    /// <summary>
    /// 初始化EMC
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行冷重置（硬件重启）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> ColdResetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行热重置（软件重置）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> HotResetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止使用EMC（在其他实例重置时调用）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 恢复使用EMC（其他实例重置完成后调用）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> ResumeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查EMC是否可用
    /// </summary>
    /// <returns>是否可用</returns>
    bool IsAvailable();
}
