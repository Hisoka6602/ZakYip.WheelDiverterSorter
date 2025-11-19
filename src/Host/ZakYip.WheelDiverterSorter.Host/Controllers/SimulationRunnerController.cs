using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Simulation.Results;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 仿真运行控制 API 控制器
/// </summary>
/// <remarks>
/// 提供启动和监控仿真运行的功能
/// </remarks>
[ApiController]
[Route("api/sim")]
[Produces("application/json")]
public class SimulationRunnerController : ControllerBase
{
    private readonly ILogger<SimulationRunnerController> _logger;
    private static SimulationStatus? _currentStatus;
    private static readonly object _statusLock = new();

    public SimulationRunnerController(ILogger<SimulationRunnerController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取当前仿真状态
    /// </summary>
    /// <returns>仿真状态信息</returns>
    /// <response code="200">成功返回状态</response>
    /// <remarks>
    /// 查询当前仿真运行状态，包括完成状态、包裹数量、错误信息等。
    /// </remarks>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SimulationStatus), 200)]
    public IActionResult GetStatus()
    {
        lock (_statusLock)
        {
            if (_currentStatus == null)
            {
                return Ok(new SimulationStatus
                {
                    IsRunning = false,
                    IsCompleted = false,
                    TotalParcels = 0,
                    CompletedParcels = 0,
                    Message = "未开始仿真"
                });
            }

            return Ok(_currentStatus);
        }
    }

    /// <summary>
    /// 更新仿真状态（内部使用）
    /// </summary>
    public static void UpdateStatus(SimulationStatus status)
    {
        lock (_statusLock)
        {
            _currentStatus = status;
        }
    }

    /// <summary>
    /// 重置仿真状态
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult Reset()
    {
        lock (_statusLock)
        {
            _currentStatus = null;
            _logger.LogInformation("仿真状态已重置");
            return Ok(new { message = "仿真状态已重置" });
        }
    }
}

/// <summary>
/// 仿真状态模型
/// </summary>
public class SimulationStatus
{
    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 总包裹数
    /// </summary>
    public int TotalParcels { get; set; }

    /// <summary>
    /// 已完成包裹数
    /// </summary>
    public int CompletedParcels { get; set; }

    /// <summary>
    /// 状态消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>
    /// 仿真结果摘要
    /// </summary>
    public SimulationResultSummary? Summary { get; set; }
}

/// <summary>
/// 仿真结果摘要
/// </summary>
public class SimulationResultSummary
{
    /// <summary>
    /// 正常完成的包裹数
    /// </summary>
    public int NormalCount { get; set; }

    /// <summary>
    /// 异常包裹数
    /// </summary>
    public int ExceptionCount { get; set; }

    /// <summary>
    /// 错分包裹数
    /// </summary>
    public int MisSortCount { get; set; }

    /// <summary>
    /// 最大并发包裹数
    /// </summary>
    public int MaxConcurrentParcels { get; set; }
}
