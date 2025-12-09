using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine
{
    /// <summary>
    /// 基于命名互斥锁（Named Mutex）实现的 EMC 资源分布式锁。
    /// <para>
    /// 命名互斥锁在 Windows 系统范围内有效，可跨进程同步。
    /// </para>
    /// </summary>
    public sealed class EmcNamedMutexLock : IEmcResourceLock
    {
        private readonly ILogger<EmcNamedMutexLock> _logger;
        private readonly string _mutexName;
        private Mutex? _mutex;
        private bool _isLockHeld;
        private bool _disposed;

        /// <summary>
        /// 初始化一个新的命名互斥锁实例。
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="resourceName">资源名称（如 "EMC_CardNo_0"）。</param>
        public EmcNamedMutexLock(ILogger<EmcNamedMutexLock> logger, string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentNullException(nameof(resourceName));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 使用全局命名空间前缀，确保跨会话可见
            _mutexName = $"Global\\ZakYip_EMC_{resourceName}";
            LockIdentifier = resourceName;
        }

        /// <inheritdoc />
        public string LockIdentifier { get; }

        /// <inheritdoc />
        public bool IsLockHeld => _isLockHeld && !_disposed;

        /// <inheritdoc />
        public async Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EmcNamedMutexLock));

            if (_isLockHeld)
            {
                _logger.LogWarning("[EMC Lock] 锁已被当前实例持有: {LockIdentifier}", LockIdentifier);
                return true; // 幂等
            }

            try
            {
                // 创建或打开命名互斥锁
                _mutex = new Mutex(false, _mutexName, out _);

                _logger.LogInformation("[EMC Lock] 尝试获取锁: {LockIdentifier}, 超时: {TimeoutMs}ms", 
                    LockIdentifier, timeout.TotalMilliseconds);

                // 使用 Task.Run 避免阻塞异步上下文
                var acquired = await Task.Run(() =>
                {
                    try
                    {
                        return _mutex.WaitOne(timeout);
                    }
                    catch (AbandonedMutexException ex)
                    {
                        // 如果前一个持有者异常退出，互斥锁会被标记为"放弃"
                        // 这种情况下，当前线程仍然获得了锁
                        _logger.LogWarning(ex, "[EMC Lock] 检测到放弃的互斥锁（前一个持有者可能异常退出）: {LockIdentifier}", 
                            LockIdentifier);
                        return true;
                    }
                }, ct);

                if (acquired)
                {
                    _isLockHeld = true;
                    _logger.LogDebug("[EMC Lock] 成功获取锁: {LockIdentifier}", LockIdentifier);
                }
                else
                {
                    _logger.LogWarning("[EMC Lock] 获取锁超时: {LockIdentifier}", LockIdentifier);
                }

                return acquired;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[EMC Lock] 获取锁被取消: {LockIdentifier}", LockIdentifier);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMC Lock] 获取锁失败: {LockIdentifier}", LockIdentifier);
                return false;
            }
        }

        /// <inheritdoc />
        public void Release()
        {
            if (_disposed)
                return;

            if (!_isLockHeld || _mutex == null)
            {
                _logger.LogWarning("[EMC Lock] 尝试释放未持有的锁: {LockIdentifier}", LockIdentifier);
                return;
            }

            try
            {
                _mutex.ReleaseMutex();
                _isLockHeld = false;
                _logger.LogDebug("[EMC Lock] 成功释放锁: {LockIdentifier}", LockIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMC Lock] 释放锁失败: {LockIdentifier}", LockIdentifier);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;

            Release();

            _mutex?.Dispose();
            _mutex = null;
            _disposed = true;

            _logger.LogDebug("[EMC Lock] 锁已释放并销毁: {LockIdentifier}", LockIdentifier);
        }
    }
}
