using System.Buffers;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 缓冲区池管理器，用于减少内存分配和GC压力
/// </summary>
/// <remarks>
/// 使用 ArrayPool 和 MemoryPool 管理缓冲区：
/// - 小缓冲区 (≤ 4KB): ArrayPool (栈分配或池化)
/// - 大缓冲区 (> 4KB): MemoryPool (托管堆池化)
/// 
/// 注意：此类使用共享池（ArrayPool.Shared 和 MemoryPool.Shared），
/// 因此不需要实现 IDisposable，共享池由运行时管理生命周期。
/// </remarks>
public sealed class BufferPoolManager
{
    private static readonly Lazy<BufferPoolManager> _instance = new(() => new BufferPoolManager());
    
    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static BufferPoolManager Shared => _instance.Value;

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

    private BufferPoolManager()
    {
        // 私有构造函数，强制使用单例
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
        if (minimumSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSize), "缓冲区大小必须大于0");
        }

        return MemoryPool<byte>.Shared.Rent(minimumSize);
    }
}
