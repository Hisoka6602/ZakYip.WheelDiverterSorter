using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 默认 IO 联动执行器实现。
/// 根据 IO 联动点配置，写入相应的数字输出。
/// </summary>
public class DefaultIoLinkageExecutor : IIoLinkageExecutor
{
    private readonly IOutputPort _outputPort;
    private readonly ILogger<DefaultIoLinkageExecutor> _logger;

    public DefaultIoLinkageExecutor(
        IOutputPort outputPort,
        ILogger<DefaultIoLinkageExecutor> logger)
    {
        _outputPort = outputPort ?? throw new ArgumentNullException(nameof(outputPort));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<OperationResult> ExecuteAsync(
        IReadOnlyList<IoLinkagePoint> linkagePoints,
        CancellationToken cancellationToken = default)
    {
        if (linkagePoints == null || linkagePoints.Count == 0)
        {
            _logger.LogDebug("没有需要执行的 IO 联动点");
            return OperationResult.Success();
        }

        _logger.LogInformation("开始执行 {Count} 个 IO 联动点写入", linkagePoints.Count);

        var failedWrites = new List<string>();

        foreach (var point in linkagePoints)
        {
            try
            {
                // 根据触发电平类型决定写入值
                // ActiveHigh = 高电平有效 = true
                // ActiveLow = 低电平有效 = false
                var value = point.Level == TriggerLevel.ActiveHigh;

                var success = await _outputPort.WriteAsync(point.BitNumber, value);

                if (success)
                {
                    _logger.LogDebug(
                        "成功写入 IO 联动点：BitNumber={BitNumber}, Level={Level}, Value={Value}",
                        point.BitNumber,
                        point.Level,
                        value);
                }
                else
                {
                    var errorMsg = $"IO {point.BitNumber} 写入失败";
                    _logger.LogError(errorMsg);
                    failedWrites.Add(errorMsg);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"IO {point.BitNumber} 写入异常: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                failedWrites.Add(errorMsg);
            }
        }

        if (failedWrites.Count > 0)
        {
            var errorMessage = $"部分 IO 联动点写入失败: {string.Join(", ", failedWrites)}";
            _logger.LogError(errorMessage);
            return OperationResult.Failure(errorMessage);
        }

        _logger.LogInformation("所有 IO 联动点写入成功");
        return OperationResult.Success();
    }
}
