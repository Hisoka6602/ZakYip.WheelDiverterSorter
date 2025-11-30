using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Events.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Chute;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Services;

/// <summary>
/// 告警接收器接口 / Alert Sink Interface
/// 负责接收和处理告警事件，支持多种实现（日志、企业微信、钉钉、邮件等）
/// Responsible for receiving and processing alert events, supports multiple implementations (log, WeChat Work, DingTalk, email, etc.)
/// </summary>
public interface IAlertSink
{
    /// <summary>
    /// 写入告警事件 / Write an alert event
    /// </summary>
    /// <param name="alertEvent">告警事件数据 / Alert event data</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>异步任务 / Async task</returns>
    Task WriteAlertAsync(AlertRaisedEventArgs alertEvent, CancellationToken cancellationToken = default);
}
