using System.Buffers;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 缓冲区池管理器，用于减少内存分配和GC压力
/// </summary>
/// <remarks>
/// 使用 ArrayPool 和 MemoryPool 管理缓冲区：
/// - 小缓冲区 (≤ 4KB): ArrayPool (栈分配或池化)
/// - 大缓冲区 (> 4KB): MemoryPool (托管堆池化)
/// </remarks>
public sealed class BufferPoolManager : IDisposable
{
    private readonly MemoryPool<byte> _memoryPool;
    private bool _disposed;

    /// <summary>
    /// 大缓冲区阈值（4KB）
    /// </summary>
    public const int LargeBufferThreshold = 4096;

    /// <summary>
    /// 默认小缓冲区大小（1KB）
    /// </summary>
    public const int DefaultSmallBufferSize = 1024;

    /// <summary>
    /// 默认大缓冲区大小（8KB）
    /// </summary>
    public const int DefaultLargeBufferSize = 8192;

    public BufferPoolManager()
    {
        _memoryPool = MemoryPool<byte>.Shared;
    }

    /// <summary>
    /// 租用小缓冲区（≤ 4KB）
    /// </summary>
    /// <param name="minimumSize">最小大小</param>
    /// <returns>租用的缓冲区</returns>
    public byte[] RentSmallBuffer(int minimumSize = DefaultSmallBufferSize)
    {
        if (minimumSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSize), "缓冲区大小必须大于0");
        }

        if (minimumSize > LargeBufferThreshold)
        {
            throw new ArgumentException($"小缓冲区大小不能超过 {LargeBufferThreshold} 字节，使用 RentLargeBuffer 代替", nameof(minimumSize));
        }

        return ArrayPool<byte>.Shared.Rent(minimumSize);
    }

    /// <summary>
    /// 归还小缓冲区
    /// </summary>
    /// <param name="buffer">要归还的缓冲区</param>
    /// <param name="clearBuffer">是否清空缓冲区（安全考虑）</param>
    public void ReturnSmallBuffer(byte[] buffer, bool clearBuffer = true)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        ArrayPool<byte>.Shared.Return(buffer, clearBuffer);
    }

    /// <summary>
    /// 租用大缓冲区（> 4KB）
    /// </summary>
    /// <param name="minimumSize">最小大小</param>
    /// <returns>租用的内存块</returns>
    public IMemoryOwner<byte> RentLargeBuffer(int minimumSize = DefaultLargeBufferSize)
    {
        ThrowIfDisposed();

        if (minimumSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSize), "缓冲区大小必须大于0");
        }

        return _memoryPool.Rent(minimumSize);
    }

    /// <summary>
    /// 使用 stackalloc 分配固定大小的栈缓冲区（仅用于小型临时缓冲区）
    /// </summary>
    /// <param name="size">缓冲区大小（建议 ≤ 512 字节）</param>
    /// <param name="buffer">输出：栈分配的 Span</param>
    /// <returns>是否成功分配</returns>
    /// <remarks>
    /// 注意：此方法返回的 Span 只在当前作用域有效，不能传递到异步方法或存储到字段中
    /// stackalloc 只能在方法内部使用，因此此方法已移除。请在调用方直接使用 stackalloc。
    /// </remarks>
    [Obsolete("不能将 stackalloc 包装在方法中，请在调用方直接使用 stackalloc")]
    public static bool TryStackAllocate(int size, out Span<byte> buffer)
    {
        buffer = default;
        return false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BufferPoolManager));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // MemoryPool.Shared 不需要显式释放
            _disposed = true;
        }
    }
}
