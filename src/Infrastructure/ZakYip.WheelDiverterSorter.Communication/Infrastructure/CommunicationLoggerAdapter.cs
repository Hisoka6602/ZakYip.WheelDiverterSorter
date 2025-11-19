using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 通信日志记录器适配器
/// </summary>
public class CommunicationLoggerAdapter : ICommunicationLogger
{
    private readonly ILogger _logger;

    public CommunicationLoggerAdapter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogError(Exception? exception, string message, params object[] args)
    {
        _logger.LogError(exception, message, args);
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }
}
