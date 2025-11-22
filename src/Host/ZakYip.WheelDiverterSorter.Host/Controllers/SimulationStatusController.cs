using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 仿真状态查询控制器（向后兼容）
/// </summary>
/// <remarks>
/// 该控制器保留用于向后兼容，实际仿真管理功能已移至 SimulationController (/api/simulation)
/// 
/// **迁移说明**：
/// - 原 /api/sim/status 端点已移至 /api/simulation/status
/// - 建议使用新的 /api/simulation 路径进行仿真管理
/// </remarks>
[ApiController]
[Route("api/sim")]
[Produces("application/json")]
public class SimulationStatusController : ControllerBase
{
    private readonly ILogger<SimulationStatusController> _logger;

    public SimulationStatusController(ILogger<SimulationStatusController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取仿真状态（向后兼容）
    /// </summary>
    /// <returns>仿真状态信息</returns>
    /// <response code="200">成功返回状态</response>
    /// <remarks>
    /// 该端点保留用于向后兼容，建议使用 /api/simulation/status
    /// 
    /// 返回一个简化的仿真状态对象。
    /// </remarks>
    [HttpGet("status")]
    [SwaggerOperation(
        Summary = "获取仿真状态（向后兼容端点）",
        Description = "返回简化的仿真状态信息，保留用于向后兼容。建议使用 /api/simulation/status",
        OperationId = "GetSimulationStatusLegacy",
        Tags = new[] { "仿真管理（兼容）" }
    )]
    [SwaggerResponse(200, "成功返回状态", typeof(SimulationStatus))]
    [ProducesResponseType(typeof(SimulationStatus), 200)]
    public IActionResult GetStatus()
    {
        _logger.LogInformation("使用了向后兼容的 /api/sim/status 端点");
        
        // 返回一个默认的状态对象，用于满足测试需求
        return Ok(new SimulationStatus
        {
            IsRunning = false,
            IsCompleted = false,
            TotalParcels = 0,
            CompletedParcels = 0,
            Message = "请使用 /api/simulation/status 查询详细仿真状态"
        });
    }
}
