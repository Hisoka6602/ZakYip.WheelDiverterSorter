using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 输送线段配置管理API控制器
/// </summary>
/// <remarks>
/// <para>提供输送线段配置的管理接口，用于定义摆轮前输送线段的物理参数。</para>
/// <para>每个线段对应拓扑配置中的一个 SegmentId，描述从上一个节点到下一个摆轮的输送带。</para>
/// <para>配置参数包括：</para>
/// <list type="bullet">
/// <item>线段长度（LengthMm）</item>
/// <item>线速（SpeedMmps）</item>
/// <item>时间容差（TimeToleranceMs）</item>
/// </list>
/// <para>系统根据这些参数自动计算：</para>
/// <list type="bullet">
/// <item>理论传输时间 = (长度 / 速度) × 1000</item>
/// <item>超时阈值 = 理论传输时间 + 时间容差</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/config/conveyor-segments")]
[Produces("application/json")]
public class ConveyorSegmentController : ControllerBase
{
    private readonly IConveyorSegmentService _segmentService;
    private readonly ISystemClock _clock;
    private readonly ILogger<ConveyorSegmentController> _logger;

    /// <summary>
    /// 初始化输送线段配置控制器
    /// </summary>
    public ConveyorSegmentController(
        IConveyorSegmentService segmentService,
        ISystemClock clock,
        ILogger<ConveyorSegmentController> logger)
    {
        _segmentService = segmentService;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有输送线段配置
    /// </summary>
    /// <returns>线段配置列表</returns>
    /// <response code="200">成功返回配置列表</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取所有输送线段配置",
        Description = "返回所有已配置的输送线段，包括每个线段的长度、速度、时间容差等参数",
        OperationId = "GetAllConveyorSegments",
        Tags = new[] { "输送线段配置" }
    )]
    [SwaggerResponse(200, "成功返回配置列表", typeof(ApiResponse<IEnumerable<ConveyorSegmentResponse>>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<IEnumerable<ConveyorSegmentResponse>>> GetAllSegments()
    {
        try
        {
            var segments = _segmentService.GetAllSegments();
            var response = segments.Select(MapToResponse);
            return Ok(ApiResponse<IEnumerable<ConveyorSegmentResponse>>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取输送线段配置列表失败");
            return StatusCode(500, ApiResponse<object>.ServerError($"获取输送线段配置列表失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取指定输送线段配置
    /// </summary>
    /// <param name="id">线段ID</param>
    /// <returns>线段配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="404">线段不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("{id}")]
    [SwaggerOperation(
        Summary = "获取指定输送线段配置",
        Description = "根据线段ID获取配置详情，包括自动计算的传输时间和超时阈值",
        OperationId = "GetConveyorSegmentById",
        Tags = new[] { "输送线段配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<ConveyorSegmentResponse>))]
    [SwaggerResponse(404, "线段不存在", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<ConveyorSegmentResponse>> GetSegmentById(long id)
    {
        try
        {
            var segment = _segmentService.GetSegmentById(id);
            if (segment == null)
            {
                return NotFound(ApiResponse<object>.NotFound($"输送线段ID {id} 不存在"));
            }

            var response = MapToResponse(segment);
            return Ok(ApiResponse<ConveyorSegmentResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取输送线段配置失败: SegmentId={SegmentId}", id);
            return StatusCode(500, ApiResponse<object>.ServerError($"获取输送线段配置失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 创建输送线段配置
    /// </summary>
    /// <param name="request">线段配置请求</param>
    /// <returns>创建后的线段配置</returns>
    /// <response code="200">创建成功</response>
    /// <response code="400">请求参数无效或线段ID已存在</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     POST /api/config/conveyor-segments
    ///     {
    ///         "segmentId": 1,
    ///         "segmentName": "入口到摆轮D1",
    ///         "lengthMm": 5000,
    ///         "speedMmps": 1000,
    ///         "timeToleranceMs": 500,
    ///         "enableLossDetection": true,
    ///         "remarks": "入口到第一个摆轮的输送段"
    ///     }
    ///
    /// 响应将包含自动计算的传输时间和超时阈值：
    /// - calculatedTransitTimeMs = 5000ms (lengthMm / speedMmps × 1000)
    /// - calculatedTimeoutThresholdMs = 5500ms (transitTime + timeToleranceMs)
    /// </remarks>
    [HttpPost]
    [SwaggerOperation(
        Summary = "创建输送线段配置",
        Description = "创建新的输送线段配置，系统将自动计算传输时间和超时阈值",
        OperationId = "CreateConveyorSegment",
        Tags = new[] { "输送线段配置" }
    )]
    [SwaggerResponse(200, "创建成功", typeof(ApiResponse<ConveyorSegmentResponse>))]
    [SwaggerResponse(400, "请求参数无效或线段ID已存在", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public async Task<ActionResult<ApiResponse<ConveyorSegmentResponse>>> CreateSegment([FromBody] ConveyorSegmentRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.BadRequest($"请求参数无效: {string.Join(", ", errors)}"));
            }

            var config = MapToConfiguration(request);
            var result = await _segmentService.CreateSegmentAsync(config);

            if (!result.IsSuccess)
            {
                return BadRequest(ApiResponse<object>.BadRequest(result.ErrorMessage!));
            }

            var response = MapToResponse(result.Segment!);
            _logger.LogInformation("输送线段配置已创建: SegmentId={SegmentId}, Name={Name}", 
                response.SegmentId, response.SegmentName);

            return Ok(ApiResponse<ConveyorSegmentResponse>.Ok(response, "输送线段配置已创建 - Conveyor segment configuration created"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建输送线段配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("创建输送线段配置失败 - Failed to create conveyor segment configuration"));
        }
    }

    /// <summary>
    /// 更新输送线段配置
    /// </summary>
    /// <param name="id">线段ID</param>
    /// <param name="request">线段配置请求</param>
    /// <returns>更新后的线段配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效或线段ID不匹配</response>
    /// <response code="404">线段不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPut("{id}")]
    [SwaggerOperation(
        Summary = "更新输送线段配置",
        Description = "更新指定输送线段的配置，系统将重新计算传输时间和超时阈值",
        OperationId = "UpdateConveyorSegment",
        Tags = new[] { "输送线段配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<ConveyorSegmentResponse>))]
    [SwaggerResponse(400, "请求参数无效或线段ID不匹配", typeof(ApiResponse<object>))]
    [SwaggerResponse(404, "线段不存在", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public async Task<ActionResult<ApiResponse<ConveyorSegmentResponse>>> UpdateSegment(long id, [FromBody] ConveyorSegmentRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.BadRequest($"请求参数无效: {string.Join(", ", errors)}"));
            }

            if (id != request.SegmentId)
            {
                return BadRequest(ApiResponse<object>.BadRequest("路径参数ID与请求体中的SegmentId不匹配"));
            }

            var config = MapToConfiguration(request);
            var result = await _segmentService.UpdateSegmentAsync(config);

            if (!result.IsSuccess)
            {
                if (result.ErrorMessage?.Contains("不存在") == true)
                {
                    return NotFound(ApiResponse<object>.NotFound(result.ErrorMessage));
                }
                return BadRequest(ApiResponse<object>.BadRequest(result.ErrorMessage!));
            }

            var response = MapToResponse(result.Segment!);
            _logger.LogInformation("输送线段配置已更新: SegmentId={SegmentId}, Name={Name}", 
                response.SegmentId, response.SegmentName);

            return Ok(ApiResponse<ConveyorSegmentResponse>.Ok(response, "输送线段配置已更新 - Conveyor segment configuration updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新输送线段配置失败: SegmentId={SegmentId}", id);
            return StatusCode(500, ApiResponse<object>.ServerError("更新输送线段配置失败 - Failed to update conveyor segment configuration"));
        }
    }

    /// <summary>
    /// 删除输送线段配置
    /// </summary>
    /// <param name="id">线段ID</param>
    /// <returns>删除结果</returns>
    /// <response code="200">删除成功</response>
    /// <response code="404">线段不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpDelete("{id}")]
    [SwaggerOperation(
        Summary = "删除输送线段配置",
        Description = "删除指定的输送线段配置。注意：删除后，引用此线段的拓扑配置将无法获取线段参数",
        OperationId = "DeleteConveyorSegment",
        Tags = new[] { "输送线段配置" }
    )]
    [SwaggerResponse(200, "删除成功", typeof(ApiResponse<object>))]
    [SwaggerResponse(404, "线段不存在", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSegment(long id)
    {
        try
        {
            var result = await _segmentService.DeleteSegmentAsync(id);

            if (!result.IsSuccess)
            {
                if (result.ErrorMessage?.Contains("不存在") == true)
                {
                    return NotFound(ApiResponse<object>.NotFound(result.ErrorMessage));
                }
                return BadRequest(ApiResponse<object>.BadRequest(result.ErrorMessage!));
            }

            _logger.LogInformation("输送线段配置已删除: SegmentId={SegmentId}", id);
            return Ok(ApiResponse<object>.Ok(new { }, "输送线段配置已删除 - Conveyor segment configuration deleted"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除输送线段配置失败: SegmentId={SegmentId}", id);
            return StatusCode(500, ApiResponse<object>.ServerError("删除输送线段配置失败 - Failed to delete conveyor segment configuration"));
        }
    }

    /// <summary>
    /// 批量创建输送线段配置
    /// </summary>
    /// <param name="request">批量创建请求</param>
    /// <returns>批量操作结果</returns>
    /// <response code="200">批量操作完成</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     POST /api/config/conveyor-segments/batch
    ///     {
    ///         "segments": [
    ///             {
    ///                 "segmentId": 1,
    ///                 "segmentName": "入口到摆轮D1",
    ///                 "lengthMm": 5000,
    ///                 "speedMmps": 1000,
    ///                 "timeToleranceMs": 500,
    ///                 "enableLossDetection": true
    ///             },
    ///             {
    ///                 "segmentId": 2,
    ///                 "segmentName": "摆轮D1到摆轮D2",
    ///                 "lengthMm": 6000,
    ///                 "speedMmps": 1200,
    ///                 "timeToleranceMs": 600,
    ///                 "enableLossDetection": true
    ///             }
    ///         ]
    ///     }
    ///
    /// 响应将包含成功和失败的数量，以及错误详情
    /// </remarks>
    [HttpPost("batch")]
    [SwaggerOperation(
        Summary = "批量创建输送线段配置",
        Description = "一次性创建多个输送线段配置，返回成功和失败的数量",
        OperationId = "CreateConveyorSegmentsBatch",
        Tags = new[] { "输送线段配置" }
    )]
    [SwaggerResponse(200, "批量操作完成", typeof(ApiResponse<ConveyorSegmentBatchResponse>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public async Task<ActionResult<ApiResponse<ConveyorSegmentBatchResponse>>> CreateSegmentsBatch([FromBody] ConveyorSegmentBatchRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.BadRequest($"请求参数无效: {string.Join(", ", errors)}"));
            }

            var configs = request.Segments.Select(MapToConfiguration);
            var result = await _segmentService.CreateSegmentsBatchAsync(configs);

            var response = new ConveyorSegmentBatchResponse
            {
                SuccessCount = result.SuccessCount,
                FailureCount = result.FailureCount,
                Errors = result.Errors
            };

            _logger.LogInformation("批量创建输送线段配置完成: 成功={Success}, 失败={Failure}", 
                response.SuccessCount, response.FailureCount);

            return Ok(ApiResponse<ConveyorSegmentBatchResponse>.Ok(response, 
                $"批量操作完成: 成功 {response.SuccessCount}, 失败 {response.FailureCount} - Batch operation completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量创建输送线段配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("批量创建输送线段配置失败 - Failed to create conveyor segments in batch"));
        }
    }

    /// <summary>
    /// 获取默认输送线段配置模板
    /// </summary>
    /// <param name="segmentId">线段ID</param>
    /// <returns>默认配置模板</returns>
    /// <response code="200">成功返回默认模板</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回默认配置模板，包含以下默认值：
    /// - lengthMm: 5000 (5米)
    /// - speedMmps: 1000 (1米/秒)
    /// - timeToleranceMs: 500 (500毫秒)
    /// - enableLossDetection: true
    /// </remarks>
    [HttpGet("template/{segmentId}")]
    [SwaggerOperation(
        Summary = "获取默认输送线段配置模板",
        Description = "返回指定线段ID的默认配置模板，可用于初始化配置",
        OperationId = "GetConveyorSegmentTemplate",
        Tags = new[] { "输送线段配置" }
    )]
    [SwaggerResponse(200, "成功返回默认模板", typeof(ApiResponse<ConveyorSegmentResponse>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<ConveyorSegmentResponse>> GetTemplate(long segmentId)
    {
        try
        {
            var template = _segmentService.GetDefaultTemplate(segmentId);
            var response = MapToResponse(template);
            return Ok(ApiResponse<ConveyorSegmentResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取默认模板失败: SegmentId={SegmentId}", segmentId);
            return StatusCode(500, ApiResponse<object>.ServerError($"获取默认模板失败: {ex.Message}"));
        }
    }

    private ConveyorSegmentConfiguration MapToConfiguration(ConveyorSegmentRequest request)
    {
        var now = _clock.LocalNow;
        return new ConveyorSegmentConfiguration
        {
            SegmentId = request.SegmentId,
            SegmentName = request.SegmentName,
            LengthMm = request.LengthMm,
            SpeedMmps = request.SpeedMmps,
            TimeToleranceMs = request.TimeToleranceMs,
            Remarks = request.Remarks,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static ConveyorSegmentResponse MapToResponse(ConveyorSegmentConfiguration config)
    {
        return new ConveyorSegmentResponse
        {
            SegmentId = config.SegmentId,
            SegmentName = config.SegmentName,
            LengthMm = config.LengthMm,
            SpeedMmps = config.SpeedMmps,
            TimeToleranceMs = config.TimeToleranceMs,
            Remarks = config.Remarks,
            CalculatedTransitTimeMs = config.CalculateTransitTimeMs(),
            CalculatedTimeoutThresholdMs = config.CalculateTimeoutThresholdMs(),
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}
