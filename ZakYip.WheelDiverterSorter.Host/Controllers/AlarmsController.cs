using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 告警管理API
/// Alarm Management API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AlarmsController : ControllerBase
{
    private readonly AlarmService _alarmService;
    private readonly ILogger<AlarmsController> _logger;

    public AlarmsController(
        AlarmService alarmService,
        ILogger<AlarmsController> logger)
    {
        _alarmService = alarmService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有活跃告警
    /// Get all active alarms
    /// </summary>
    /// <returns>活跃告警列表 / List of active alarms</returns>
    [HttpGet]
    public ActionResult<IEnumerable<AlarmEvent>> GetActiveAlarms()
    {
        var alarms = _alarmService.GetActiveAlarms();
        return Ok(alarms);
    }

    /// <summary>
    /// 获取当前分拣失败率
    /// Get current sorting failure rate
    /// </summary>
    /// <returns>分拣失败率 / Sorting failure rate</returns>
    [HttpGet("sorting-failure-rate")]
    public ActionResult<object> GetSortingFailureRate()
    {
        var failureRate = _alarmService.GetSortingFailureRate();
        return Ok(new
        {
            failureRate,
            percentage = $"{failureRate * 100:F2}%"
        });
    }

    /// <summary>
    /// 确认告警
    /// Acknowledge an alarm
    /// </summary>
    /// <param name="alarmType">告警类型 / Alarm type</param>
    /// <returns>操作结果 / Operation result</returns>
    [HttpPost("acknowledge")]
    public ActionResult AcknowledgeAlarm([FromQuery] AlarmType alarmType)
    {
        var alarms = _alarmService.GetActiveAlarms();
        var alarm = alarms.FirstOrDefault(a => a.Type == alarmType);

        if (alarm == null)
        {
            return NotFound(new { message = $"未找到类型为 {alarmType} 的活跃告警 / No active alarm found for type {alarmType}" });
        }

        _alarmService.AcknowledgeAlarm(alarm);
        _logger.LogInformation(
            "告警已确认 / Alarm acknowledged: Type={Type}, Level={Level}",
            alarm.Type,
            alarm.Level);

        return Ok(new { message = "告警已确认 / Alarm acknowledged", alarm });
    }

    /// <summary>
    /// 重置分拣统计计数器
    /// Reset sorting statistics counters
    /// </summary>
    /// <returns>操作结果 / Operation result</returns>
    [HttpPost("reset-statistics")]
    public ActionResult ResetStatistics()
    {
        _alarmService.ResetSortingStatistics();
        _logger.LogInformation("分拣统计计数器已重置 / Sorting statistics reset");
        return Ok(new { message = "统计计数器已重置 / Statistics reset" });
    }
}
