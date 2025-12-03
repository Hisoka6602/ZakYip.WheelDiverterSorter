using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 通信基础设施默认实现
/// </summary>
public class DefaultCommunicationInfrastructure : ICommunicationInfrastructure
{
    public DefaultCommunicationInfrastructure(
        UpstreamConnectionOptions options,
        ILogger logger,
        ISystemClock systemClock)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        if (systemClock == null)
        {
            throw new ArgumentNullException(nameof(systemClock));
        }

        Logger = logger;
        RetryPolicy = new ExponentialBackoffRetryPolicy(options, Logger);
        CircuitBreaker = new SimpleCircuitBreaker(options, Logger, systemClock);
        Serializer = new JsonMessageSerializer();
    }

    public IRetryPolicy RetryPolicy { get; }
    public ICircuitBreaker CircuitBreaker { get; }
    public IMessageSerializer Serializer { get; }
    public ILogger Logger { get; }
}
