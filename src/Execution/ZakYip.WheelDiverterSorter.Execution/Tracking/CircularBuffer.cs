namespace ZakYip.WheelDiverterSorter.Execution.Tracking;

/// <summary>
/// 循环缓冲区（固定大小，自动覆盖旧数据）
/// </summary>
/// <typeparam name="T">缓冲区元素类型</typeparam>
/// <remarks>
/// 线程安全：所有公共方法都通过 lock 保护，确保多线程并发访问安全
/// </remarks>
public sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new object();
    private int _index = 0;
    private int _count = 0;
    
    /// <summary>
    /// 获取当前缓冲区中的元素数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }
    
    /// <summary>
    /// 初始化循环缓冲区
    /// </summary>
    /// <param name="capacity">缓冲区容量</param>
    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero");
            
        _buffer = new T[capacity];
    }
    
    /// <summary>
    /// 添加元素到缓冲区
    /// </summary>
    /// <param name="item">要添加的元素</param>
    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_index] = item;
            _index = (_index + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }
    }
    
    /// <summary>
    /// 将缓冲区内容转换为数组（按时间顺序，最旧到最新）
    /// </summary>
    /// <returns>包含缓冲区所有元素的数组</returns>
    /// <remarks>
    /// 优化：避免 LINQ Take().ToArray() 的额外分配，直接使用 Array.Copy
    /// </remarks>
    public T[] ToArray()
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                return Array.Empty<T>();
            }
            
            var result = new T[_count];
            
            if (_count < _buffer.Length)
            {
                // 缓冲区未满，直接复制前 _count 个元素
                Array.Copy(_buffer, 0, result, 0, _count);
            }
            else
            {
                // 缓冲区已满，按正确顺序返回（最旧到最新）
                Array.Copy(_buffer, _index, result, 0, _buffer.Length - _index);
                Array.Copy(_buffer, 0, result, _buffer.Length - _index, _index);
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// 清空缓冲区
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _index = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }
}
