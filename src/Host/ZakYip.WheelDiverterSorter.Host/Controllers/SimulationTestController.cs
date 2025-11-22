using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Services;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 仿真测试控制器
/// </summary>
/// <remarks>
/// 提供仿真和测试环境下的调试功能，包括手动触发分拣等。
/// 
/// **重要警告**：
/// - 本控制器中的所有端点仅供仿真和测试环境使用
/// - **生产环境中禁止调用**，否则会干扰真实分拣数据
/// - 生产环境下调用这些端点将返回错误
/// 
/// **使用场景**：
/// - 开发和调试阶段手动触发分拣
/// - 集成测试验证分拣逻辑
/// - 仿真场景模拟包裹处理
/// </remarks>
[ApiController]
[Route("api/simulation/test")]
[Produces("application/json")]
public class SimulationTestController : ControllerBase
{
    private readonly DebugSortService _debugService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SimulationTestController> _logger;

    public SimulationTestController(
        DebugSortService debugService,
        IWebHostEnvironment environment,
        ILogger<SimulationTestController> logger)
    {
        _debugService = debugService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// 手动触发包裹分拣（仅供测试/仿真环境）
    /// </summary>
    /// <param name="request">分拣请求，包含包裹ID和目标格口ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分拣执行结果</returns>
    /// <response code="200">分拣执行成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="403">生产环境禁止调用</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **功能说明**：
    /// 
    /// 接收包裹ID和目标格口ID，生成摆轮切换路径并执行分拣操作。
    /// 
    /// **环境限制**：
    /// - **仅在测试和仿真环境中可用**
    /// - 在生产环境（Production）中调用将返回 403 错误
    /// - 生产环境下应通过扫码或供包台触发分拣，而非此接口
    /// 
    /// **示例请求**：
    /// ```json
    /// {
    ///   "parcelId": "PKG-20231201-001",
    ///   "targetChuteId": 5
    /// }
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "parcelId": "PKG-20231201-001",
    ///   "targetChuteId": 5,
    ///   "isSuccess": true,
    ///   "message": "分拣执行成功",
    ///   "executionTimeMs": 1250.5,
    ///   "pathSegments": [
    ///     {
    ///       "diverterId": 1,
    ///       "direction": "Left",
    ///       "sequenceNumber": 1
    ///     }
    ///   ]
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 包裹ID必须唯一，避免与真实包裹冲突
    /// - 目标格口ID必须在系统路由配置中存在
    /// - 此接口会实际触发摆轮动作（在硬件环境下）
    /// </remarks>
    [HttpPost("sort")]
    [SwaggerOperation(
        Summary = "手动触发包裹分拣（仅供测试/仿真环境）",
        Description = "接收包裹ID和目标格口ID，生成并执行摆轮分拣路径。**仅在测试/仿真环境可用，生产环境禁止调用**",
        OperationId = "TriggerDebugSort",
        Tags = new[] { "仿真测试" }
    )]
    [SwaggerResponse(200, "分拣执行成功", typeof(DebugSortResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(403, "生产环境禁止调用")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(DebugSortResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> TriggerDebugSort(
        [FromBody] DebugSortRequest request,
        CancellationToken cancellationToken)
    {
        // 检查环境：生产环境禁止调用
        if (_environment.IsProduction())
        {
            _logger.LogWarning(
                "生产环境下尝试调用仿真测试接口 /api/simulation/test/sort，已拒绝。ParcelId: {ParcelId}",
                request.ParcelId);
            
            return StatusCode(403, new
            {
                message = "生产环境下禁止调用仿真测试接口",
                errorCode = "FORBIDDEN_IN_PRODUCTION",
                hint = "此接口仅供开发、测试和仿真环境使用。生产环境下应通过扫码或供包台触发分拣。"
            });
        }

        // 参数验证
        if (string.IsNullOrWhiteSpace(request.ParcelId))
        {
            return BadRequest(new { message = "包裹ID不能为空" });
        }

        if (request.TargetChuteId <= 0)
        {
            return BadRequest(new { message = "目标格口ID必须大于0" });
        }

        try
        {
            _logger.LogInformation(
                "仿真测试：手动触发分拣，ParcelId: {ParcelId}, TargetChuteId: {TargetChuteId}",
                request.ParcelId,
                request.TargetChuteId);

            var response = await _debugService.ExecuteDebugSortAsync(
                request.ParcelId,
                request.TargetChuteId,
                cancellationToken);

            if (response.IsSuccess)
            {
                _logger.LogInformation(
                    "仿真测试：分拣执行成功，ParcelId: {ParcelId}",
                    request.ParcelId);
            }
            else
            {
                _logger.LogWarning(
                    "仿真测试：分拣执行失败，ParcelId: {ParcelId}, Message: {Message}",
                    request.ParcelId,
                    response.Message);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仿真测试：分拣执行异常，ParcelId: {ParcelId}", request.ParcelId);
            return StatusCode(500, new
            {
                message = "分拣执行失败",
                error = ex.Message
            });
        }
    }
}
