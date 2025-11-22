using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Host.Models;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 仿真测试控制器（已废弃，请使用 /api/simulation）
/// </summary>
/// <remarks>
/// **⚠️ 此控制器已废弃，将在未来版本中移除**
/// 
/// 所有仿真测试功能已迁移至 SimulationController (/api/simulation)。
/// 
/// **迁移指南**：
/// - 原端点: POST /api/simulation/test/sort
/// - 新端点: POST /api/simulation/sort
/// - 请求和响应模型保持不变
/// 
/// **重要提示**：
/// - 本控制器保留仅为向后兼容
/// - 所有端点将返回 410 Gone 状态码并提示迁移
/// - 建议尽快迁移到新端点
/// </remarks>
[ApiController]
[Route("api/simulation/test")]
[Produces("application/json")]
[Obsolete("此控制器已废弃，请使用 /api/simulation 下的端点")]
public class SimulationTestController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SimulationTestController> _logger;

    public SimulationTestController(
        IWebHostEnvironment environment,
        ILogger<SimulationTestController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// 手动触发包裹分拣（已废弃，请使用 POST /api/simulation/sort）
    /// </summary>
    /// <param name="request">分拣请求，包含包裹ID和目标格口ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>端点已废弃的通知</returns>
    /// <response code="410">端点已废弃</response>
    /// <remarks>
    /// **⚠️ 此端点已废弃**
    /// 
    /// 请迁移至: **POST /api/simulation/sort**
    /// 
    /// **迁移说明**：
    /// - 新端点地址: /api/simulation/sort
    /// - 请求模型保持不变
    /// - 响应模型保持不变
    /// - 功能完全一致
    /// 
    /// 该端点将在下一主要版本中移除。
    /// </remarks>
    [HttpPost("sort")]
    [SwaggerOperation(
        Summary = "手动触发包裹分拣（已废弃）",
        Description = "此端点已废弃，请使用 POST /api/simulation/sort",
        OperationId = "TriggerDebugSort_Obsolete",
        Tags = new[] { "仿真测试（已废弃）" }
    )]
    [SwaggerResponse(410, "端点已废弃")]
    [ProducesResponseType(typeof(object), 410)]
    [Obsolete("请使用 POST /api/simulation/sort")]
    public Task<IActionResult> TriggerDebugSort(
        [FromBody] DebugSortRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "使用了已废弃的端点 POST /api/simulation/test/sort，请迁移至 POST /api/simulation/sort");

        return Task.FromResult<IActionResult>(StatusCode(410, new
        {
            message = "此端点已废弃",
            deprecatedEndpoint = "POST /api/simulation/test/sort",
            newEndpoint = "POST /api/simulation/sort",
            hint = "请更新您的API调用，使用新端点。请求和响应模型保持不变。",
            willBeRemovedIn = "v2.0.0"
        }));
    }
}
