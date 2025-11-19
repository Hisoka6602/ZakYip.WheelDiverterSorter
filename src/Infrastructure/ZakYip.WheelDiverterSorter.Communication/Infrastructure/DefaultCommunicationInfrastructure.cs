using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 通信基础设施默认实现
/// </summary>
public class DefaultCommunicationInfrastructure : ICommunicationInfrastructure
{
    public DefaultCommunicationInfrastructure(
        RuleEngineConnectionOptions options,
        ILogger logger)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        Logger = new CommunicationLoggerAdapter(logger);
        RetryPolicy = new ExponentialBackoffRetryPolicy(options, Logger);
        CircuitBreaker = new SimpleCircuitBreaker(options, Logger);
        Serializer = new JsonMessageSerializer();
    }

    public IRetryPolicy RetryPolicy { get; }
    public ICircuitBreaker CircuitBreaker { get; }
    public IMessageSerializer Serializer { get; }
    public ICommunicationLogger Logger { get; }
}
