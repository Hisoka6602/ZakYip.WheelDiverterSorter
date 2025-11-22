using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 具有分布式锁协调能力的EMC控制器
/// 在执行重置操作前通知其他实例，确保多实例安全
/// </summary>
public class CoordinatedEmcController : IEmcController
{
    private readonly ILogger<CoordinatedEmcController> _logger;
    private readonly IEmcController _emcController;
    private readonly IEmcResourceLockManager? _lockManager;
    private readonly IEmcResourceLock? _resourceLock;
    private readonly bool _lockEnabled;
    private readonly LockType _lockType;

    /// <inheritdoc/>
    public ushort CardNo => _emcController.CardNo;

    private enum LockType
    {
        None,
        TcpBased,    // 使用 IEmcResourceLockManager (旧实现)
        NamedMutex   // 使用 IEmcResourceLock (新实现)
    }

    /// <summary>
    /// 构造函数（不使用分布式锁）
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="emcController">底层EMC控制器</param>
    public CoordinatedEmcController(
        ILogger<CoordinatedEmcController> logger,
        IEmcController emcController)
    {
        _logger = logger;
        _emcController = emcController;
        _lockManager = null;
        _resourceLock = null;
        _lockEnabled = false;
        _lockType = LockType.None;
    }

    /// <summary>
    /// 构造函数（使用TCP分布式锁管理器 - 旧实现）
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="emcController">底层EMC控制器</param>
    /// <param name="lockManager">TCP分布式锁管理器</param>
    public CoordinatedEmcController(
        ILogger<CoordinatedEmcController> logger,
        IEmcController emcController,
        IEmcResourceLockManager lockManager)
    {
        _logger = logger;
        _emcController = emcController;
        _lockManager = lockManager;
        _resourceLock = null;
        _lockEnabled = true;
        _lockType = LockType.TcpBased;

        // 订阅锁事件
        if (_lockManager != null)
        {
            _lockManager.EmcLockEventReceived += OnEmcLockEventReceived;
        }
    }

    /// <summary>
    /// 构造函数（使用命名互斥锁 - 新实现，推荐）
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="emcController">底层EMC控制器</param>
    /// <param name="resourceLock">EMC资源锁（命名互斥锁）</param>
    public CoordinatedEmcController(
        ILogger<CoordinatedEmcController> logger,
        IEmcController emcController,
        IEmcResourceLock resourceLock)
    {
        _logger = logger;
        _emcController = emcController;
        _lockManager = null;
        _resourceLock = resourceLock ?? throw new ArgumentNullException(nameof(resourceLock));
        _lockEnabled = true;
        _lockType = LockType.NamedMutex;
    }

    /// <inheritdoc/>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 初始化底层EMC控制器
        var result = await _emcController.InitializeAsync(cancellationToken);

        // 如果启用了TCP分布式锁，连接到锁服务
        if (_lockEnabled && _lockType == LockType.TcpBased && _lockManager != null && result)
        {
            var connected = await _lockManager.ConnectAsync(cancellationToken);
            if (!connected)
            {
                _logger.LogWarning("连接到EMC锁服务失败，将以单实例模式运行");
            }
            else
            {
                _logger.LogInformation("已连接到EMC锁服务，实例ID: {InstanceId}", _lockManager.InstanceId);
            }
        }
        else if (_lockEnabled && _lockType == LockType.NamedMutex && _resourceLock != null && result)
        {
            _logger.LogInformation("使用命名互斥锁进行分布式锁协调，资源: {LockIdentifier}", _resourceLock.LockIdentifier);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> ColdResetAsync(CancellationToken cancellationToken = default)
    {
        if (!_lockEnabled)
        {
            // 直接执行重置
            _logger.LogWarning("未启用分布式锁，直接执行冷重置");
            return await _emcController.ColdResetAsync(cancellationToken);
        }

        // 根据锁类型选择不同的重置策略
        return _lockType switch
        {
            LockType.TcpBased => await ColdResetWithTcpLockAsync(cancellationToken),
            LockType.NamedMutex => await ColdResetWithNamedMutexAsync(cancellationToken),
            _ => await _emcController.ColdResetAsync(cancellationToken)
        };
    }

    /// <inheritdoc/>
    public async Task<bool> HotResetAsync(CancellationToken cancellationToken = default)
    {
        if (!_lockEnabled)
        {
            // 直接执行重置
            _logger.LogWarning("未启用分布式锁，直接执行热重置");
            return await _emcController.HotResetAsync(cancellationToken);
        }

        // 根据锁类型选择不同的重置策略
        return _lockType switch
        {
            LockType.TcpBased => await HotResetWithTcpLockAsync(cancellationToken),
            LockType.NamedMutex => await HotResetWithNamedMutexAsync(cancellationToken),
            _ => await _emcController.HotResetAsync(cancellationToken)
        };
    }

    /// <inheritdoc/>
    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        return _emcController.StopAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
    {
        return _emcController.ResumeAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public bool IsAvailable()
    {
        return _emcController.IsAvailable();
    }

    /// <summary>
    /// 使用TCP分布式锁协调执行冷重置
    /// </summary>
    private async Task<bool> ColdResetWithTcpLockAsync(CancellationToken cancellationToken)
    {
        if (_lockManager == null || !_lockManager.IsConnected)
        {
            _logger.LogWarning("TCP锁管理器未连接，直接执行冷重置");
            return await _emcController.ColdResetAsync(cancellationToken);
        }

        try
        {
            _logger.LogInformation("请求EMC锁以执行冷重置，卡号: {CardNo}", CardNo);

            // 1. 请求锁
            var lockAcquired = await _lockManager.RequestLockAsync(CardNo, timeoutMs: 10000, cancellationToken);
            if (!lockAcquired)
            {
                _logger.LogError("获取EMC锁失败，其他实例可能未准备好，取消重置");
                return false;
            }

            try
            {
                // 2. 发送冷重置通知
                _logger.LogInformation("发送冷重置通知，卡号: {CardNo}", CardNo);
                var notified = await _lockManager.NotifyColdResetAsync(CardNo, timeoutMs: 10000, cancellationToken);
                if (!notified)
                {
                    _logger.LogWarning("发送冷重置通知失败或超时，继续执行重置");
                }

                // 3. 等待其他实例准备就绪
                await Task.Delay(1000, cancellationToken);

                // 4. 执行实际的冷重置操作
                _logger.LogInformation("开始执行EMC冷重置，卡号: {CardNo}", CardNo);
                var result = await _emcController.ColdResetAsync(cancellationToken);

                if (!result)
                {
                    _logger.LogError("执行EMC冷重置失败，卡号: {CardNo}", CardNo);
                    return false;
                }

                // 5. 通知重置完成
                _logger.LogInformation("发送重置完成通知，卡号: {CardNo}", CardNo);
                await _lockManager.NotifyResetCompleteAsync(CardNo, cancellationToken);

                return true;
            }
            finally
            {
                // 6. 释放锁
                await _lockManager.ReleaseLockAsync(CardNo, cancellationToken);
                _logger.LogInformation("已释放EMC锁，卡号: {CardNo}", CardNo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行协调冷重置时发生异常");
            return false;
        }
    }

    /// <summary>
    /// 使用命名互斥锁执行冷重置
    /// </summary>
    private async Task<bool> ColdResetWithNamedMutexAsync(CancellationToken cancellationToken)
    {
        if (_resourceLock == null)
        {
            return await _emcController.ColdResetAsync(cancellationToken);
        }

        try
        {
            _logger.LogInformation("尝试获取EMC资源锁以执行冷重置，卡号: {CardNo}", CardNo);

            // 1. 获取锁（30秒超时）
            var lockAcquired = await _resourceLock.TryAcquireAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (!lockAcquired)
            {
                _logger.LogError("获取EMC资源锁失败（超时），其他实例可能正在使用，取消重置");
                return false;
            }

            try
            {
                // 2. 执行实际的冷重置操作
                _logger.LogInformation("开始执行EMC冷重置，卡号: {CardNo}", CardNo);
                var result = await _emcController.ColdResetAsync(cancellationToken);

                if (!result)
                {
                    _logger.LogError("执行EMC冷重置失败，卡号: {CardNo}", CardNo);
                    return false;
                }

                _logger.LogInformation("EMC冷重置完成，卡号: {CardNo}", CardNo);
                return true;
            }
            finally
            {
                // 3. 释放锁
                _resourceLock.Release();
                _logger.LogInformation("已释放EMC资源锁，卡号: {CardNo}", CardNo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "使用命名互斥锁执行冷重置时发生异常");
            return false;
        }
    }

    /// <summary>
    /// 使用TCP分布式锁协调执行热重置
    /// </summary>
    private async Task<bool> HotResetWithTcpLockAsync(CancellationToken cancellationToken)
    {
        if (_lockManager == null || !_lockManager.IsConnected)
        {
            _logger.LogWarning("TCP锁管理器未连接，直接执行热重置");
            return await _emcController.HotResetAsync(cancellationToken);
        }

        try
        {
            _logger.LogInformation("请求EMC锁以执行热重置，卡号: {CardNo}", CardNo);

            // 1. 请求锁
            var lockAcquired = await _lockManager.RequestLockAsync(CardNo, timeoutMs: 10000, cancellationToken);
            if (!lockAcquired)
            {
                _logger.LogError("获取EMC锁失败，其他实例可能未准备好，取消重置");
                return false;
            }

            try
            {
                // 2. 发送热重置通知
                _logger.LogInformation("发送热重置通知，卡号: {CardNo}", CardNo);
                var notified = await _lockManager.NotifyHotResetAsync(CardNo, timeoutMs: 10000, cancellationToken);
                if (!notified)
                {
                    _logger.LogWarning("发送热重置通知失败或超时，继续执行重置");
                }

                // 3. 等待其他实例准备就绪
                await Task.Delay(500, cancellationToken);

                // 4. 执行实际的热重置操作
                _logger.LogInformation("开始执行EMC热重置，卡号: {CardNo}", CardNo);
                var result = await _emcController.HotResetAsync(cancellationToken);

                if (!result)
                {
                    _logger.LogError("执行EMC热重置失败，卡号: {CardNo}", CardNo);
                    return false;
                }

                // 5. 通知重置完成
                _logger.LogInformation("发送重置完成通知，卡号: {CardNo}", CardNo);
                await _lockManager.NotifyResetCompleteAsync(CardNo, cancellationToken);

                return true;
            }
            finally
            {
                // 6. 释放锁
                await _lockManager.ReleaseLockAsync(CardNo, cancellationToken);
                _logger.LogInformation("已释放EMC锁，卡号: {CardNo}", CardNo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行协调热重置时发生异常");
            return false;
        }
    }

    /// <summary>
    /// 使用命名互斥锁执行热重置
    /// </summary>
    private async Task<bool> HotResetWithNamedMutexAsync(CancellationToken cancellationToken)
    {
        if (_resourceLock == null)
        {
            return await _emcController.HotResetAsync(cancellationToken);
        }

        try
        {
            _logger.LogInformation("尝试获取EMC资源锁以执行热重置，卡号: {CardNo}", CardNo);

            // 1. 获取锁（30秒超时）
            var lockAcquired = await _resourceLock.TryAcquireAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (!lockAcquired)
            {
                _logger.LogError("获取EMC资源锁失败（超时），其他实例可能正在使用，取消重置");
                return false;
            }

            try
            {
                // 2. 执行实际的热重置操作
                _logger.LogInformation("开始执行EMC热重置，卡号: {CardNo}", CardNo);
                var result = await _emcController.HotResetAsync(cancellationToken);

                if (!result)
                {
                    _logger.LogError("执行EMC热重置失败，卡号: {CardNo}", CardNo);
                    return false;
                }

                _logger.LogInformation("EMC热重置完成，卡号: {CardNo}", CardNo);
                return true;
            }
            finally
            {
                // 3. 释放锁
                _resourceLock.Release();
                _logger.LogInformation("已释放EMC资源锁，卡号: {CardNo}", CardNo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "使用命名互斥锁执行热重置时发生异常");
            return false;
        }
    }

    /// <summary>
    /// 处理接收到的EMC锁事件
    /// </summary>
    private async void OnEmcLockEventReceived(object? sender, EmcLockEventArgs e)
    {
        var lockEvent = e.LockEvent;

        // 只处理针对当前卡号的事件
        if (lockEvent.CardNo != CardNo)
        {
            return;
        }

        _logger.LogInformation(
            "收到EMC锁事件: {Type}, 来自实例: {InstanceId}, 卡号: {CardNo}, 消息: {Message}",
            lockEvent.NotificationType,
            lockEvent.InstanceId,
            lockEvent.CardNo,
            lockEvent.Message);

        try
        {
            switch (lockEvent.NotificationType)
            {
                case EmcLockNotificationType.ColdReset:
                case EmcLockNotificationType.HotReset:
                    // 停止使用EMC硬件
                    _logger.LogWarning("收到重置通知，暂停使用EMC，卡号: {CardNo}", CardNo);
                    await StopAsync();

                    // 发送就绪消息
                    if (_lockManager != null)
                    {
                        await _lockManager.SendReadyAsync(lockEvent.EventId, CardNo);
                        _logger.LogInformation("已发送就绪消息，等待重置完成");
                    }
                    break;

                case EmcLockNotificationType.ResetComplete:
                    // 重新初始化并恢复使用
                    _logger.LogInformation("收到重置完成通知，恢复使用EMC，卡号: {CardNo}", CardNo);
                    await ResumeAsync();
                    break;

                case EmcLockNotificationType.RequestLock:
                    // 有其他实例请求锁，可能即将执行重置
                    _logger.LogInformation("其他实例请求EMC锁，卡号: {CardNo}", CardNo);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理EMC锁事件时发生异常");
        }
    }
}
