using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Communication.Infrastructure;

/// <summary>
/// 指数退避重试策略
/// </summary>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly int _initialDelayMs;
    private readonly ILogger _logger;

    public ExponentialBackoffRetryPolicy(UpstreamConnectionOptions options, ILogger logger)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _maxRetries = options.RetryCount;
        _initialDelayMs = options.RetryDelayMs;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        int attempt = 0;
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                attempt++;
                var delay = _initialDelayMs * (int)Math.Pow(2, attempt - 1);
                
                _logger.LogWarning(
                    "Operation failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms. Error: {Error}",
                    attempt, _maxRetries, delay, ex.Message);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, cancellationToken);
    }
}
