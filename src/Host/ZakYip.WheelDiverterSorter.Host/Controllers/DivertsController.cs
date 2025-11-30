using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Simulation;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Application.Services.Debug;
using ZakYip.WheelDiverterSorter.Host.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 分拣改口API控制器
/// </summary>
[ApiController]
[Route("api/diverts")]
[Produces("application/json")]
public class DivertsController : ControllerBase
{
    private readonly IChangeParcelChuteService _changeParcelChuteService;
    private readonly ILogger<DivertsController> _logger;

    public DivertsController(
        IChangeParcelChuteService changeParcelChuteService,
        ILogger<DivertsController> logger)
    {
        _changeParcelChuteService = changeParcelChuteService ?? throw new ArgumentNullException(nameof(changeParcelChuteService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 请求更改包裹的目标格口（改口）
    /// </summary>
    /// <param name="request">改口请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>改口处理结果</returns>
    /// <response code="200">改口请求已处理（可能接受、忽略或拒绝）</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     POST /api/diverts/change-chute
    ///     {
    ///         "parcelId": 1001,
    ///         "requestedChuteId": 5,
    ///         "requestedAt": "2025-11-17T10:30:00Z"
    ///     }
    /// 
    /// 改口功能用于在包裹分拣过程中动态修改目标格口。
    /// 改口请求会根据包裹当前状态决定是否接受：
    /// - Accepted：改口成功，路径已重新规划
    /// - IgnoredAlreadyCompleted：包裹已完成分拣，改口被忽略
    /// - IgnoredExceptionRouted：包裹已进入异常格口，改口被忽略
    /// - RejectedInvalidState：包裹状态不允许改口
    /// - RejectedTooLate：改口请求太晚，包裹已无法改变路径
    /// </remarks>
    [HttpPost("change-chute")]
    [SwaggerOperation(
        Summary = "请求更改包裹的目标格口（改口）",
        Description = "在包裹分拣过程中动态修改目标格口，系统会根据包裹当前位置和状态决定是否接受改口请求",
        OperationId = "ChangeParcelChute",
        Tags = new[] { "分拣改口" }
    )]
    [SwaggerResponse(200, "改口请求已处理", typeof(ChuteChangeResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ChuteChangeResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<ChuteChangeResponse>> ChangeChute(
        [FromBody] ChuteChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request body is required" });
            }

            if (request.ParcelId <= 0)
            {
                return BadRequest(new { message = "Invalid ParcelId" });
            }

            if (request.RequestedChuteId <= 0)
            {
                return BadRequest(new { message = "Invalid RequestedChuteId" });
            }

            var command = new ChangeParcelChuteCommand
            {
                ParcelId = request.ParcelId,
                RequestedChuteId = request.RequestedChuteId,
                RequestedAt = request.RequestedAt
            };

            var result = await _changeParcelChuteService.ChangeParcelChuteAsync(command, cancellationToken);

            var response = new ChuteChangeResponse
            {
                IsSuccess = result.IsSuccess,
                ParcelId = result.ParcelId,
                OriginalChuteId = result.OriginalChuteId,
                RequestedChuteId = result.RequestedChuteId,
                EffectiveChuteId = result.EffectiveChuteId,
                Outcome = result.Outcome?.ToString(),
                Message = result.Message,
                ProcessedAt = result.ProcessedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chute change request for parcel {ParcelId}", request?.ParcelId);
            return StatusCode(500, new { message = "Internal server error processing chute change request" });
        }
    }
}
