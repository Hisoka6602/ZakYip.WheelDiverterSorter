using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.Commands;
using ZakYip.WheelDiverterSorter.Host.Models;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 分拣改口API控制器
/// </summary>
[ApiController]
[Route("api/diverts")]
[Produces("application/json")]
public class DivertsController : ControllerBase
{
    private readonly ChangeParcelChuteCommandHandler _changeParcelChuteHandler;
    private readonly ILogger<DivertsController> _logger;

    public DivertsController(
        ChangeParcelChuteCommandHandler changeParcelChuteHandler,
        ILogger<DivertsController> logger)
    {
        _changeParcelChuteHandler = changeParcelChuteHandler ?? throw new ArgumentNullException(nameof(changeParcelChuteHandler));
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
    [HttpPost("change-chute")]
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

            var result = await _changeParcelChuteHandler.HandleAsync(command, cancellationToken);

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
