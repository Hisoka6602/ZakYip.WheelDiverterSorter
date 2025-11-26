using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 线体段配置管理API控制器
/// Line segment configuration management API controller
/// </summary>
/// <remarks>
/// 提供线体段配置的查询和管理功能。
/// 
/// **线体段说明**：
/// 线体段定义了两个感应IO之间的物理线体，包含长度和速度信息。
/// 
/// **拓扑组成规则**：
/// 一个最简的摆轮分拣拓扑由以下元素组成：
/// - 创建包裹感应IO -> 线体段 -> 摆轮 -> 格口（摆轮方向=格口）
/// 
/// **多段线体规则**：
/// - 第一段线体的起点IO必须是创建包裹的IO（ParcelCreation类型）
/// - 最后一段线体的终点IO Id应该是0（表示已到达末端）
/// - 中间线体段的起点/终点IO通常是摆轮前感应IO（WheelFront类型）
/// 
/// **计算用途**：
/// - 根据线体长度和速度计算包裹从上一个IO到下一个IO的理论时间
/// - 用于超时检测和丢包判断逻辑
/// - 计算公式：时间(ms) = (长度mm / 速度mm/s) * 1000
/// </remarks>
[ApiController]
[Route("api/config/line-segments")]
[Produces("application/json")]
public class LineSegmentController : ControllerBase
{
    private readonly ILineTopologyRepository _topologyRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<LineSegmentController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="topologyRepository">线体拓扑配置仓储</param>
    /// <param name="sensorRepository">感应IO配置仓储</param>
    /// <param name="clock">系统时钟</param>
    /// <param name="logger">日志记录器</param>
    public LineSegmentController(
        ILineTopologyRepository topologyRepository,
        ISensorConfigurationRepository sensorRepository,
        ISystemClock clock,
        ILogger<LineSegmentController> logger)
    {
        _topologyRepository = topologyRepository;
        _sensorRepository = sensorRepository;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有线体段配置
    /// Get all line segment configurations
    /// </summary>
    /// <returns>线体段配置列表 / List of line segment configurations</returns>
    /// <response code="200">成功返回线体段配置列表 / Successfully returned line segment configuration list</response>
    /// <response code="500">服务器内部错误 / Internal server error</response>
    /// <remarks>
    /// 返回当前配置的所有线体段信息，包括：
    /// Returns all configured line segment information, including:
    /// - 线体段ID和名称 / Line segment ID and name
    /// - 起点IO和终点IO的引用 / Start and end IO references
    /// - 线体长度（毫米）和速度（毫米/秒） / Length (mm) and speed (mm/s)
    /// - 理论通过时间（毫秒） / Theoretical transit time (ms)
    /// 
    /// **示例响应 / Example response**：
    /// ```json
    /// {
    ///   "success": true,
    ///   "code": "Ok",
    ///   "message": "操作成功",
    ///   "data": [
    ///     {
    ///       "segmentId": 1,
    ///       "segmentName": "入口到第一摆轮段",
    ///       "startIoId": 1,
    ///       "endIoId": 2,
    ///       "lengthMm": 5000.0,
    ///       "speedMmPerSec": 1000.0,
    ///       "transitTimeMs": 5000.0,
    ///       "description": "从创建包裹IO到第一摆轮前IO"
    ///     }
    ///   ]
    /// }
    /// ```
    /// </remarks>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取所有线体段配置",
        Description = "返回当前配置的所有线体段信息，包括线体ID、名称、起终点IO、长度、速度和理论通过时间",
        OperationId = "GetAllLineSegments",
        Tags = new[] { "线体段配置" }
    )]
    [SwaggerResponse(200, "成功返回线体段配置列表", typeof(ApiResponse<List<LineSegmentResponse>>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<List<LineSegmentResponse>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<List<LineSegmentResponse>>> GetAllLineSegments()
    {
        try
        {
            var topology = _topologyRepository.Get();
            var segments = topology.LineSegments.Select(MapToResponse).ToList();
            return Ok(ApiResponse<List<LineSegmentResponse>>.Ok(segments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取线体段配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取线体段配置失败 - Failed to get line segment configuration"));
        }
    }

    /// <summary>
    /// 根据ID获取线体段配置
    /// Get line segment configuration by ID
    /// </summary>
    /// <param name="segmentId">线体段ID / Line segment ID</param>
    /// <returns>线体段配置 / Line segment configuration</returns>
    /// <response code="200">成功返回线体段配置 / Successfully returned line segment configuration</response>
    /// <response code="404">未找到指定的线体段 / Line segment not found</response>
    /// <response code="500">服务器内部错误 / Internal server error</response>
    /// <remarks>
    /// 根据线体段ID获取单个线体段的详细配置信息。
    /// Get detailed configuration of a single line segment by its ID.
    /// 
    /// **参数说明 / Parameter description**：
    /// - segmentId: 线体段唯一标识符（long类型） / Unique identifier of the line segment (long type)
    /// 
    /// **示例响应 / Example response**：
    /// ```json
    /// {
    ///   "success": true,
    ///   "code": "Ok",
    ///   "message": "操作成功",
    ///   "data": {
    ///     "segmentId": 1,
    ///     "segmentName": "入口到第一摆轮段",
    ///     "startIoId": 1,
    ///     "endIoId": 2,
    ///     "lengthMm": 5000.0,
    ///     "speedMmPerSec": 1000.0,
    ///     "transitTimeMs": 5000.0,
    ///     "description": "从创建包裹IO到第一摆轮前IO"
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpGet("{segmentId:long}")]
    [SwaggerOperation(
        Summary = "根据ID获取线体段配置",
        Description = "根据线体段ID获取单个线体段的详细配置信息",
        OperationId = "GetLineSegmentById",
        Tags = new[] { "线体段配置" }
    )]
    [SwaggerResponse(200, "成功返回线体段配置", typeof(ApiResponse<LineSegmentResponse>))]
    [SwaggerResponse(404, "未找到指定的线体段", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<LineSegmentResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<LineSegmentResponse>> GetLineSegmentById(
        [FromRoute, SwaggerParameter("线体段唯一标识符（long类型）", Required = true)] long segmentId)
    {
        try
        {
            var topology = _topologyRepository.Get();
            var segment = topology.LineSegments.FirstOrDefault(s => s.SegmentId == segmentId);
            
            if (segment == null)
            {
                return NotFound(ApiResponse<object>.NotFound($"未找到线体段 {segmentId} - Line segment {segmentId} not found"));
            }

            return Ok(ApiResponse<LineSegmentResponse>.Ok(MapToResponse(segment)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取线体段配置失败: {SegmentId}", segmentId);
            return StatusCode(500, ApiResponse<object>.ServerError("获取线体段配置失败 - Failed to get line segment configuration"));
        }
    }

    /// <summary>
    /// 添加或更新线体段配置
    /// Add or update line segment configuration
    /// </summary>
    /// <param name="request">线体段配置请求 / Line segment configuration request</param>
    /// <returns>添加或更新后的线体段配置 / Updated line segment configuration</returns>
    /// <response code="200">添加或更新成功 / Add or update successful</response>
    /// <response code="400">请求参数无效 / Invalid request parameters</response>
    /// <response code="500">服务器内部错误 / Internal server error</response>
    /// <remarks>
    /// 添加新的线体段或更新现有线体段配置。删除操作也通过此接口完成（不传入要删除的线体段即可）。
    /// Add a new line segment or update an existing one. Delete operation is also handled through this interface.
    /// 
    /// **线体段配置规则 / Line segment configuration rules**：
    /// - 起点IO必须引用已配置的感应IO / Start IO must reference configured sensor IO
    /// - 终点IO为0表示末端，否则必须引用已配置的感应IO / End IO 0 means end, otherwise must reference configured sensor IO
    /// - 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型） / First segment's start IO must be ParcelCreation type
    /// - 线体长度必须大于0（范围：0.1-100000毫米） / Length must be between 0.1-100000 mm
    /// - 线体速度必须大于0（范围：1-10000毫米/秒） / Speed must be between 1-10000 mm/s
    /// 
    /// **理论通过时间计算 / Transit time calculation**：
    /// ```
    /// 通过时间(ms) = (线体长度mm / 线体速度mm/s) * 1000
    /// Transit time(ms) = (length mm / speed mm/s) * 1000
    /// ```
    /// 
    /// **示例请求 / Example request**：
    /// ```json
    /// {
    ///   "segmentId": 1,
    ///   "segmentName": "入口到第一摆轮段",
    ///   "startIoId": 1,
    ///   "endIoId": 2,
    ///   "lengthMm": 5000.0,
    ///   "speedMmPerSec": 1000.0,
    ///   "description": "从创建包裹IO到第一摆轮前IO"
    /// }
    /// ```
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "添加或更新线体段配置",
        Description = "添加新的线体段或更新现有线体段配置。起点IO和终点IO必须引用已配置的感应IO（终点IO为0表示末端）。删除操作通过更新整体线体拓扑配置完成。",
        OperationId = "UpsertLineSegment",
        Tags = new[] { "线体段配置" }
    )]
    [SwaggerResponse(200, "添加或更新成功", typeof(ApiResponse<LineSegmentResponse>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<LineSegmentResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<LineSegmentResponse>> UpsertLineSegment(
        [FromBody, SwaggerRequestBody("线体段配置请求", Required = true)] LineSegmentRequest request)
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

            // 验证IO引用
            var validationResult = ValidateIoReferences(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(ApiResponse<object>.BadRequest(validationResult.ErrorMessage!));
            }

            // 获取当前拓扑配置
            var topology = _topologyRepository.Get();
            var segments = topology.LineSegments.ToList();

            // 查找是否已存在该线体段
            var existingIndex = segments.FindIndex(s => s.SegmentId == request.SegmentId);
            var newSegment = MapToConfig(request);

            if (existingIndex >= 0)
            {
                // 更新现有线体段
                segments[existingIndex] = newSegment;
                _logger.LogInformation("线体段配置已更新: SegmentId={SegmentId}", request.SegmentId);
            }
            else
            {
                // 添加新线体段
                segments.Add(newSegment);
                _logger.LogInformation("线体段配置已添加: SegmentId={SegmentId}", request.SegmentId);
            }

            // 更新拓扑配置
            var updatedTopology = topology with
            {
                LineSegments = segments,
                UpdatedAt = _clock.LocalNow
            };
            _topologyRepository.Update(updatedTopology);

            return Ok(ApiResponse<LineSegmentResponse>.Ok(
                MapToResponse(newSegment),
                existingIndex >= 0 ? "线体段配置已更新 - Line segment updated" : "线体段配置已添加 - Line segment added"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加或更新线体段配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("添加或更新线体段配置失败 - Failed to upsert line segment configuration"));
        }
    }

    /// <summary>
    /// 计算两个IO之间的理论通过时间
    /// Calculate theoretical transit time between two IOs
    /// </summary>
    /// <param name="startIoId">起点IO ID / Start IO ID</param>
    /// <param name="endIoId">终点IO ID (0表示到末端) / End IO ID (0 means end of line)</param>
    /// <returns>计算结果 / Calculation result</returns>
    /// <response code="200">计算成功 / Calculation successful</response>
    /// <response code="400">IO路径不存在 / IO path not found</response>
    /// <response code="500">服务器内部错误 / Internal server error</response>
    /// <remarks>
    /// 计算从起点IO到终点IO之间所有线体段的理论通过时间总和。
    /// Calculate total theoretical transit time for all line segments between start and end IOs.
    /// 
    /// **参数说明 / Parameter description**：
    /// - startIoId: 起点感应IO的ID / Start sensor IO ID
    /// - endIoId: 终点感应IO的ID，设为0表示计算到末端 / End sensor IO ID, 0 means calculate to end
    /// 
    /// **计算公式 / Calculation formula**：
    /// ```
    /// 总通过时间 = Σ(线体段长度 / 线体段速度) * 1000
    /// Total transit time = Σ(segment length / segment speed) * 1000
    /// ```
    /// 
    /// **示例请求 / Example request**：
    /// ```
    /// GET /api/config/line-segments/transit-time?startIoId=1&amp;endIoId=2
    /// ```
    /// 
    /// **示例响应 / Example response**：
    /// ```json
    /// {
    ///   "success": true,
    ///   "code": "Ok",
    ///   "message": "操作成功",
    ///   "data": {
    ///     "startIoId": 1,
    ///     "endIoId": 2,
    ///     "totalDistanceMm": 5000.0,
    ///     "totalTransitTimeMs": 5000.0,
    ///     "segmentCount": 1
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpGet("transit-time")]
    [SwaggerOperation(
        Summary = "计算两个IO之间的理论通过时间",
        Description = "计算从起点IO到终点IO之间所有线体段的理论通过时间总和。用于超时检测和丢包判断。",
        OperationId = "CalculateTransitTime",
        Tags = new[] { "线体段配置" }
    )]
    [SwaggerResponse(200, "计算成功", typeof(ApiResponse<TransitTimeResponse>))]
    [SwaggerResponse(400, "IO路径不存在", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<TransitTimeResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<TransitTimeResponse>> CalculateTransitTime(
        [FromQuery, SwaggerParameter("起点感应IO的ID", Required = true)] long startIoId,
        [FromQuery, SwaggerParameter("终点感应IO的ID (0表示到末端)", Required = true)] long endIoId)
    {
        try
        {
            var topology = _topologyRepository.Get();
            var path = topology.GetPathBetweenIos(startIoId, endIoId);

            if (path == null || path.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest(
                    $"找不到从IO {startIoId} 到 IO {endIoId} 的路径 - No path found from IO {startIoId} to IO {endIoId}"));
            }

            var totalDistance = path.Sum(s => s.LengthMm);
            var totalTime = path.Sum(s => s.CalculateTransitTimeMs());

            var response = new TransitTimeResponse
            {
                StartIoId = startIoId,
                EndIoId = endIoId,
                TotalDistanceMm = totalDistance,
                TotalTransitTimeMs = totalTime,
                SegmentCount = path.Count
            };

            return Ok(ApiResponse<TransitTimeResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算通过时间失败: StartIoId={StartIoId}, EndIoId={EndIoId}", startIoId, endIoId);
            return StatusCode(500, ApiResponse<object>.ServerError("计算通过时间失败 - Failed to calculate transit time"));
        }
    }

    /// <summary>
    /// 验证IO引用
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateIoReferences(LineSegmentRequest request)
    {
        var sensorConfig = _sensorRepository.Get();
        var configuredSensorIds = sensorConfig.Sensors?.Select(s => s.SensorId).ToHashSet() ?? new HashSet<long>();

        // 验证起点IO
        if (request.StartIoId != 0)
        {
            if (!configuredSensorIds.Contains(request.StartIoId))
            {
                return (false, $"起点IO (StartIoId={request.StartIoId}) 未配置，请先在感应IO配置中添加 - Start IO not configured");
            }
        }

        // 验证终点IO（0表示末端，允许）
        if (request.EndIoId != 0)
        {
            if (!configuredSensorIds.Contains(request.EndIoId))
            {
                return (false, $"终点IO (EndIoId={request.EndIoId}) 未配置，请先在感应IO配置中添加 - End IO not configured");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 将请求映射为配置对象
    /// </summary>
    private static LineSegmentConfig MapToConfig(LineSegmentRequest request)
    {
        return new LineSegmentConfig
        {
            SegmentId = request.SegmentId,
            SegmentName = request.SegmentName,
            StartIoId = request.StartIoId,
            EndIoId = request.EndIoId,
            LengthMm = request.LengthMm,
            SpeedMmPerSec = request.SpeedMmPerSec,
            ToleranceTimeMs = request.ToleranceTimeMs,
            Description = request.Description
        };
    }

    /// <summary>
    /// 将配置对象映射为响应对象
    /// </summary>
    private static LineSegmentResponse MapToResponse(LineSegmentConfig config)
    {
        return new LineSegmentResponse
        {
            SegmentId = config.SegmentId,
            SegmentName = config.SegmentName,
            StartIoId = config.StartIoId,
            EndIoId = config.EndIoId,
            LengthMm = config.LengthMm,
            SpeedMmPerSec = config.SpeedMmPerSec,
            ToleranceTimeMs = config.ToleranceTimeMs,
            TransitTimeMs = config.CalculateTransitTimeMs(),
            ActualTransitTimeMs = config.CalculateActualTransitTimeMs(),
            IsEndSegment = config.IsEndSegment,
            Description = config.Description
        };
    }
}

/// <summary>
/// 线体段配置响应
/// Line segment configuration response
/// </summary>
/// <remarks>
/// 包含线体段的所有配置信息和计算的理论通过时间
/// Contains all line segment configuration and calculated theoretical transit time
/// </remarks>
public record LineSegmentResponse
{
    /// <summary>
    /// 线体段唯一标识符
    /// Unique identifier of the line segment
    /// </summary>
    /// <example>1</example>
    public required long SegmentId { get; init; }

    /// <summary>
    /// 线体段显示名称
    /// Display name of the line segment
    /// </summary>
    /// <example>入口到第一摆轮段</example>
    public string? SegmentName { get; init; }

    /// <summary>
    /// 起点感应IO的ID（引用感应IO配置中的SensorId）
    /// Start IO ID (references SensorId in sensor configuration)
    /// </summary>
    /// <remarks>
    /// 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）
    /// First segment's start IO must be ParcelCreation type
    /// </remarks>
    /// <example>1</example>
    public required long StartIoId { get; init; }

    /// <summary>
    /// 终点感应IO的ID（引用感应IO配置中的SensorId）
    /// End IO ID (references SensorId in sensor configuration)
    /// </summary>
    /// <remarks>
    /// 值为0表示末端线体段
    /// Value 0 means end segment
    /// </remarks>
    /// <example>2</example>
    public required long EndIoId { get; init; }

    /// <summary>
    /// 线体段物理长度（单位：毫米）
    /// Physical length of the line segment (in millimeters)
    /// </summary>
    /// <example>5000.0</example>
    public required double LengthMm { get; init; }

    /// <summary>
    /// 线体运行速度（单位：毫米/秒）
    /// Line speed (in millimeters per second)
    /// </summary>
    /// <example>1000.0</example>
    public required double SpeedMmPerSec { get; init; }

    /// <summary>
    /// 容差时间（单位：毫秒）
    /// Tolerance time (in milliseconds) considering parcel friction
    /// </summary>
    /// <remarks>
    /// 考虑包裹摩擦力等因素的额外时间容差。
    /// </remarks>
    /// <example>200.0</example>
    public required double ToleranceTimeMs { get; init; }

    /// <summary>
    /// 理论通过时间（单位：毫秒，不含容差）
    /// Theoretical transit time (in milliseconds, without tolerance)
    /// </summary>
    /// <remarks>
    /// 计算公式：(LengthMm / SpeedMmPerSec) * 1000
    /// Formula: (LengthMm / SpeedMmPerSec) * 1000
    /// </remarks>
    /// <example>5000.0</example>
    public required double TransitTimeMs { get; init; }

    /// <summary>
    /// 实际预期通过时间（单位：毫秒，含容差）
    /// Actual expected transit time (in milliseconds, with tolerance)
    /// </summary>
    /// <remarks>
    /// 计算公式：TransitTimeMs + ToleranceTimeMs
    /// Formula: TransitTimeMs + ToleranceTimeMs
    /// </remarks>
    /// <example>5200.0</example>
    public required double ActualTransitTimeMs { get; init; }

    /// <summary>
    /// 是否为末端线体段（终点IO ID为0）
    /// Whether this is an end segment (EndIoId is 0)
    /// </summary>
    /// <example>false</example>
    public required bool IsEndSegment { get; init; }

    /// <summary>
    /// 线体段描述（可选）
    /// Description of the line segment (optional)
    /// </summary>
    /// <example>从创建包裹IO到第一摆轮前IO</example>
    public string? Description { get; init; }
}

/// <summary>
/// 通过时间计算响应
/// Transit time calculation response
/// </summary>
/// <remarks>
/// 包含从起点IO到终点IO的路径信息和计算的总通过时间
/// </remarks>
public record TransitTimeResponse
{
    /// <summary>
    /// 起点感应IO的ID
    /// Start IO ID
    /// </summary>
    /// <example>1</example>
    public required long StartIoId { get; init; }

    /// <summary>
    /// 终点感应IO的ID（0表示末端）
    /// End IO ID (0 means end of line)
    /// </summary>
    /// <example>2</example>
    public required long EndIoId { get; init; }

    /// <summary>
    /// 路径总距离（单位：毫米）
    /// Total distance of the path (in millimeters)
    /// </summary>
    /// <example>5000.0</example>
    public required double TotalDistanceMm { get; init; }

    /// <summary>
    /// 理论总通过时间（单位：毫秒）
    /// Total theoretical transit time (in milliseconds)
    /// </summary>
    /// <example>5000.0</example>
    public required double TotalTransitTimeMs { get; init; }

    /// <summary>
    /// 路径上的线体段数量
    /// Number of line segments in the path
    /// </summary>
    /// <example>1</example>
    public required int SegmentCount { get; init; }
}
