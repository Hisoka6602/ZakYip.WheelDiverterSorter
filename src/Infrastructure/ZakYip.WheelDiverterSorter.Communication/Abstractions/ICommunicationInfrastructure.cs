// This file is maintained for backward compatibility.
// The enum has been moved to ZakYip.WheelDiverterSorter.Core.Enums.Communication namespace.

// Re-export the enum from Core.Enums.Communication for backward compatibility
global using CircuitState = ZakYip.WheelDiverterSorter.Core.Enums.Communication.CircuitState;

namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// 通信基础设施接口
/// </summary>
/// <remarks>
/// 提供统一的重试、熔断、日志、序列化工具入口点。
/// 所有协议实现都应该通过此接口访问基础设施功能。
/// </remarks>
public interface ICommunicationInfrastructure
{
    /// <summary>
    /// 重试策略
    /// </summary>
    IRetryPolicy RetryPolicy { get; }

    /// <summary>
    /// 熔断器
    /// </summary>
    ICircuitBreaker CircuitBreaker { get; }

    /// <summary>
    /// 序列化器
    /// </summary>
    IMessageSerializer Serializer { get; }

    /// <summary>
    /// 日志记录器
    /// </summary>
    ICommunicationLogger Logger { get; }
}

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// 执行带重试的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带重试的操作（无返回值）
    /// </summary>
    /// <param name="operation">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// 熔断器接口
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// 熔断器状态
    /// </summary>
    CircuitState State { get; }

    /// <summary>
    /// 执行受保护的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置熔断器
    /// </summary>
    void Reset();
}

/// <summary>
/// 消息序列化器接口
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// 序列化对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>序列化后的字节数组</returns>
    byte[] Serialize<T>(T obj);

    /// <summary>
    /// 反序列化对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="data">序列化的字节数组</param>
    /// <returns>反序列化后的对象</returns>
    T? Deserialize<T>(byte[] data);

    /// <summary>
    /// 序列化为字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>序列化后的字符串</returns>
    string SerializeToString<T>(T obj);

    /// <summary>
    /// 从字符串反序列化
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="json">JSON字符串</param>
    /// <returns>反序列化后的对象</returns>
    T? DeserializeFromString<T>(string json);
}

/// <summary>
/// 通信日志记录器接口
/// </summary>
public interface ICommunicationLogger
{
    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="args">格式化参数</param>
    void LogInformation(string message, params object[] args);

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="args">格式化参数</param>
    void LogWarning(string message, params object[] args);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="message">日志消息</param>
    /// <param name="args">格式化参数</param>
    void LogError(Exception? exception, string message, params object[] args);

    /// <summary>
    /// 记录调试日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="args">格式化参数</param>
    void LogDebug(string message, params object[] args);
}
