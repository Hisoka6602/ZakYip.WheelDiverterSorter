using ZakYip.WheelDiverterSorter.Core.Events.Communication;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// EMC资源锁管理器接口
/// 用于协调多个实例对共享EMC硬件资源的访问
/// </summary>
public interface IEmcResourceLockManager : IDisposable
{
    /// <summary>
    /// 当前实例ID
    /// </summary>
    string InstanceId { get; }
    
    /// <summary>
    /// 是否已连接到锁服务
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// EMC锁事件（接收来自其他实例的通知）
    /// </summary>
    event EventHandler<EmcLockEventArgs>? EmcLockEventReceived;
    
    /// <summary>
    /// 连接到锁服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 断开连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DisconnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 请求锁（准备执行重置操作）
    /// </summary>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否获取到锁</returns>
    Task<bool> RequestLockAsync(ushort cardNo, int timeoutMs = 5000, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 释放锁（重置操作完成）
    /// </summary>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> ReleaseLockAsync(ushort cardNo, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送冷重置通知
    /// </summary>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> NotifyColdResetAsync(ushort cardNo, int timeoutMs = 5000, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送热重置通知
    /// </summary>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> NotifyHotResetAsync(ushort cardNo, int timeoutMs = 5000, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送确认消息
    /// </summary>
    /// <param name="eventId">事件ID</param>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> SendAcknowledgeAsync(string eventId, ushort cardNo, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送就绪消息（实例已停止使用EMC）
    /// </summary>
    /// <param name="eventId">事件ID</param>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> SendReadyAsync(string eventId, ushort cardNo, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送重置完成通知
    /// </summary>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> NotifyResetCompleteAsync(ushort cardNo, CancellationToken cancellationToken = default);
}
