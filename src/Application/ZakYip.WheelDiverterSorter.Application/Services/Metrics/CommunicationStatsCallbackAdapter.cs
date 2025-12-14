using ZakYip.WheelDiverterSorter.Communication.Abstractions;

namespace ZakYip.WheelDiverterSorter.Application.Services.Metrics;

/// <summary>
/// 通信统计回调适配器
/// Communication statistics callback adapter
/// </summary>
/// <remarks>
/// 适配器模式：将Communication层的回调接口适配到Application层的统计服务。
/// 保持Communication层不依赖Application层，实现分层解耦。
/// </remarks>
public sealed class CommunicationStatsCallbackAdapter : IMessageStatsCallback
{
    private readonly ICommunicationStatsService _statsService;

    public CommunicationStatsCallbackAdapter(ICommunicationStatsService statsService)
    {
        _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
    }

    public void IncrementSent()
    {
        _statsService.IncrementSent();
    }

    public void IncrementReceived()
    {
        _statsService.IncrementReceived();
    }
}
