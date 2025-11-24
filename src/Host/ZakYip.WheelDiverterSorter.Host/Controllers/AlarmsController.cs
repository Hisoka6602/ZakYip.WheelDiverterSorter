using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Observability;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 告警管理API控制器
/// </summary>
/// <remarks>
/// 提供系统告警的查询、确认和统计管理功能
/// </remarks>
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
    /// </summary>
    /// <returns>活跃告警列表</returns>
    /// <response code="200">成功返回告警列表</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取所有活跃告警",
        Description = "返回当前系统中所有未确认的活跃告警列表，包括告警类型、级别、触发时间等信息",
        OperationId = "GetActiveAlarms",
        Tags = new[] { "告警管理" }
    )]
    [SwaggerResponse(200, "成功返回告警列表", typeof(IEnumerable<AlarmEvent>))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(IEnumerable<AlarmEvent>), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IEnumerable<AlarmEvent>> GetActiveAlarms()
    {
        var alarms = _alarmService.GetActiveAlarms();
        return Ok(alarms);
    }

    /// <summary>
    /// 获取当前分拣失败率
    /// </summary>
    /// <returns>分拣失败率统计信息</returns>
    /// <response code="200">成功返回失败率</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前时间窗口内的分拣失败率，包括小数值和百分比形式
    /// </remarks>
    [HttpGet("sorting-failure-rate")]
    [SwaggerOperation(
        Summary = "获取当前分拣失败率",
        Description = "返回系统当前的分拣失败率统计，包括失败率小数值和百分比表示",
        OperationId = "GetSortingFailureRate",
        Tags = new[] { "告警管理" }
    )]
    [SwaggerResponse(200, "成功返回失败率", typeof(object))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
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
    /// </summary>
    /// <param name="alarmType">告警类型（必需），支持的告警类型包括：HighFailureRate（高失败率）、UpstreamTimeout（上游超时）等</param>
    /// <returns>操作结果</returns>
    /// <response code="200">成功确认告警</response>
    /// <response code="404">指定类型的活跃告警不存在</response>
    /// <response code="400">告警类型参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求：
    /// 
    ///     POST /api/alarms/acknowledge?alarmType=HighFailureRate
    /// 
    /// 确认告警后，该告警将从活跃告警列表中移除
    /// </remarks>
    [HttpPost("acknowledge")]
    [SwaggerOperation(
        Summary = "确认告警",
        Description = "确认并移除指定类型的活跃告警，用于告警管理流程",
        OperationId = "AcknowledgeAlarm",
        Tags = new[] { "告警管理" }
    )]
    [SwaggerResponse(200, "成功确认告警", typeof(object))]
    [SwaggerResponse(404, "指定类型的活跃告警不存在")]
    [SwaggerResponse(400, "告警类型参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
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
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">成功重置统计计数器</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 重置分拣成功/失败计数器，用于开始新的统计周期或测试场景
    /// </remarks>
    [HttpPost("reset-statistics")]
    [SwaggerOperation(
        Summary = "重置分拣统计计数器",
        Description = "清除当前的分拣统计数据，包括成功和失败计数器，通常用于开始新的统计周期",
        OperationId = "ResetStatistics",
        Tags = new[] { "告警管理" }
    )]
    [SwaggerResponse(200, "成功重置统计计数器", typeof(object))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult ResetStatistics()
    {
        _alarmService.ResetSortingStatistics();
        _logger.LogInformation("分拣统计计数器已重置 / Sorting statistics reset");
        return Ok(new { message = "统计计数器已重置 / Statistics reset" });
    }
}
