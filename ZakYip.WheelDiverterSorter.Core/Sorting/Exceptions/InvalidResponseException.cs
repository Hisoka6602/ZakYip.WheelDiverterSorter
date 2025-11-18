namespace ZakYip.Sorting.Core.Exceptions;

/// <summary>
/// 上游响应无效异常
/// </summary>
/// <remarks>
/// 表示上游服务返回了无法解析或不符合预期的响应
/// </remarks>
public class InvalidResponseException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public InvalidResponseException()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">错误消息</param>
    public InvalidResponseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public InvalidResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
