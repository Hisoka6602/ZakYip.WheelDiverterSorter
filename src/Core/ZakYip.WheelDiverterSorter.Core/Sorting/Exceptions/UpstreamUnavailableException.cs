namespace ZakYip.WheelDiverterSorter.Core.Sorting.Exceptions;

/// <summary>
/// 上游服务不可用异常
/// </summary>
/// <remarks>
/// 表示无法连接到上游服务或通信失败
/// </remarks>
public class UpstreamUnavailableException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public UpstreamUnavailableException()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">错误消息</param>
    public UpstreamUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public UpstreamUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
