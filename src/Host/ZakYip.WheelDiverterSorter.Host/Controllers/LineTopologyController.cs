using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 线体拓扑配置管理API控制器
/// </summary>
/// <remarks>
/// 提供线体拓扑配置的管理接口，包括线体段、摆轮节点、格口映射等
/// 用于配置整条分拣线的物理拓扑结构，支持超时/丢失计算
/// 
/// **拓扑组成规则**：
/// 一个最简的摆轮分拣拓扑由以下元素组成：
/// - 创建包裹感应IO -> 线体段 -> 摆轮 -> 格口（摆轮方向=格口）
/// 
/// **线体段规则**：
/// - 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）
/// - 最后一段线体的终点IO Id应该是0（表示已到达末端）
/// - 线体段的起点IO和终点IO必须引用已配置的感应IO
/// </remarks>
[ApiController]
[Route("api/config/line-topology")]
[Produces("application/json")]
public class LineTopologyController : ControllerBase
{
    private readonly ILineTopologyRepository _topologyRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<LineTopologyController> _logger;

    public LineTopologyController(
        ILineTopologyRepository topologyRepository,
        ISensorConfigurationRepository sensorRepository,
        ISystemClock clock,
        ILogger<LineTopologyController> logger)
    {
        _topologyRepository = topologyRepository;
        _sensorRepository = sensorRepository;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 获取线体拓扑配置
    /// </summary>
    /// <returns>线体拓扑配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取线体拓扑配置",
        Description = "返回完整的线体拓扑配置，包括线体段、摆轮节点、格口映射等信息",
        OperationId = "GetLineTopology",
        Tags = new[] { "线体拓扑配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<LineTopologyResponse>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<LineTopologyResponse>> GetLineTopology()
    {
        try
        {
            var config = _topologyRepository.Get();
            var response = MapToResponse(config);
            return Ok(ApiResponse<LineTopologyResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取线体拓扑配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取线体拓扑配置失败 - Failed to get line topology configuration"));
        }
    }

    /// <summary>
    /// 更新线体拓扑配置
    /// </summary>
    /// <param name="request">线体拓扑配置请求</param>
    /// <returns>更新后的线体拓扑配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     PUT /api/config/line-topology
    ///     {
    ///         "topologyName": "标准线体拓扑",
    ///         "description": "包含3个摆轮和10个格口的标准配置",
    ///         "wheelNodes": [
    ///             {
    ///                 "nodeId": "WHEEL-1",
    ///                 "nodeName": "第一摆轮",
    ///                 "positionIndex": 1,
    ///                 "frontIoId": 2,
    ///                 "hasLeftChute": true,
    ///                 "hasRightChute": true
    ///             }
    ///         ],
    ///         "chutes": [
    ///             {
    ///                 "chuteId": "CHUTE-001",
    ///                 "chuteName": "A区01号口",
    ///                 "isExceptionChute": false,
    ///                 "boundNodeId": "WHEEL-1",
    ///                 "boundDirection": "Left",
    ///                 "lockIoId": 3,
    ///                 "dropOffsetMm": 500.0,
    ///                 "isEnabled": true
    ///             }
    ///         ],
    ///         "lineSegments": [
    ///             {
    ///                 "segmentId": "SEG-1",
    ///                 "segmentName": "入口到第一摆轮段",
    ///                 "startIoId": 1,
    ///                 "endIoId": 2,
    ///                 "lengthMm": 5000.0,
    ///                 "speedMmPerSec": 1000.0,
    ///                 "description": "从创建包裹IO到第一摆轮前IO"
    ///             }
    ///         ],
    ///         "defaultLineSpeedMmps": 1000.0
    ///     }
    ///
    /// 配置说明：
    /// - 线体段（LineSegments）通过起点IO和终点IO定义，必须引用已配置的感应IO
    /// - 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）
    /// - 最后一段线体的终点IO Id为0（表示末端）
    /// - 摆轮节点（WheelNodes）按物理位置顺序排列，positionIndex从1开始
    /// - 格口（Chutes）必须绑定到某个摆轮节点的某个方向
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新线体拓扑配置",
        Description = "更新完整的线体拓扑配置，配置立即生效。线体段的起点IO和终点IO必须引用已配置的感应IO。",
        OperationId = "UpdateLineTopology",
        Tags = new[] { "线体拓扑配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<LineTopologyResponse>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<LineTopologyResponse>> UpdateLineTopology([FromBody] LineTopologyRequest request)
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

            // 验证摆轮节点不能为空
            if (request.WheelNodes.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("至少需要配置一个摆轮节点 - At least one wheel node is required"));
            }

            // 验证格口不能为空
            if (request.Chutes.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("至少需要配置一个格口 - At least one chute is required"));
            }

            // 验证格口绑定的节点必须存在
            var nodeIds = request.WheelNodes.Select(n => n.NodeId).ToHashSet();
            foreach (var chute in request.Chutes)
            {
                if (!nodeIds.Contains(chute.BoundNodeId))
                {
                    return BadRequest(ApiResponse<object>.BadRequest($"格口 {chute.ChuteId} 绑定的节点 {chute.BoundNodeId} 不存在"));
                }
            }

            // 获取已配置的感应IO列表用于验证
            var sensorConfig = _sensorRepository.Get();
            var configuredSensorIds = sensorConfig.Sensors?.Select(s => s.SensorId).ToHashSet() ?? new HashSet<long>();

            // 验证线体段的IO引用
            if (request.LineSegments != null && request.LineSegments.Count > 0)
            {
                var validationResult = ValidateLineSegments(request.LineSegments, configuredSensorIds, sensorConfig);
                if (!validationResult.IsValid)
                {
                    return BadRequest(ApiResponse<object>.BadRequest(validationResult.ErrorMessage!));
                }
            }

            // 验证摆轮节点的FrontIoId引用
            foreach (var node in request.WheelNodes)
            {
                if (node.FrontIoId.HasValue && node.FrontIoId.Value != 0)
                {
                    if (!configuredSensorIds.Contains(node.FrontIoId.Value))
                    {
                        return BadRequest(ApiResponse<object>.BadRequest(
                            $"摆轮节点 {node.NodeId} 的摆轮前感应IO (FrontIoId={node.FrontIoId}) 未配置，请先在感应IO配置中添加"));
                    }

                    // 验证FrontIoId必须是WheelFront类型
                    var sensor = sensorConfig.Sensors?.FirstOrDefault(s => s.SensorId == node.FrontIoId.Value);
                    if (sensor != null && sensor.IoType != SensorIoType.WheelFront)
                    {
                        return BadRequest(ApiResponse<object>.BadRequest(
                            $"摆轮节点 {node.NodeId} 的摆轮前感应IO (FrontIoId={node.FrontIoId}) 类型必须是 WheelFront，当前类型为 {sensor.IoType}"));
                    }
                }
            }

            // 验证格口的LockIoId引用
            foreach (var chute in request.Chutes)
            {
                if (chute.LockIoId.HasValue && chute.LockIoId.Value != 0)
                {
                    if (!configuredSensorIds.Contains(chute.LockIoId.Value))
                    {
                        return BadRequest(ApiResponse<object>.BadRequest(
                            $"格口 {chute.ChuteId} 的锁格感应IO (LockIoId={chute.LockIoId}) 未配置，请先在感应IO配置中添加"));
                    }

                    // 验证LockIoId必须是ChuteLock类型
                    var sensor = sensorConfig.Sensors?.FirstOrDefault(s => s.SensorId == chute.LockIoId.Value);
                    if (sensor != null && sensor.IoType != SensorIoType.ChuteLock)
                    {
                        return BadRequest(ApiResponse<object>.BadRequest(
                            $"格口 {chute.ChuteId} 的锁格感应IO (LockIoId={chute.LockIoId}) 类型必须是 ChuteLock，当前类型为 {sensor.IoType}"));
                    }
                }
            }

            var config = MapToConfig(request);
            _topologyRepository.Update(config);

            _logger.LogInformation(
                "线体拓扑配置已更新: TopologyName={TopologyName}, WheelNodes={WheelCount}, Chutes={ChuteCount}, Segments={SegmentCount}",
                config.TopologyName,
                config.WheelNodes.Count,
                config.Chutes.Count,
                config.LineSegments.Count);

            var response = MapToResponse(config);
            return Ok(ApiResponse<LineTopologyResponse>.Ok(response, "线体拓扑配置已更新 - Line topology configuration updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新线体拓扑配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("更新线体拓扑配置失败 - Failed to update line topology configuration"));
        }
    }

    /// <summary>
    /// 验证线体段配置
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateLineSegments(
        List<LineSegmentRequest> segments, 
        HashSet<long> configuredSensorIds,
        SensorConfiguration sensorConfig)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];

            // 验证起点IO（EndIoId为0表示末端，不需要验证）
            if (segment.StartIoId != 0)
            {
                if (!configuredSensorIds.Contains(segment.StartIoId))
                {
                    return (false, $"线体段 {segment.SegmentId} 的起点IO (StartIoId={segment.StartIoId}) 未配置，请先在感应IO配置中添加");
                }
            }

            // 验证终点IO（0表示末端，允许）
            if (segment.EndIoId != 0)
            {
                if (!configuredSensorIds.Contains(segment.EndIoId))
                {
                    return (false, $"线体段 {segment.SegmentId} 的终点IO (EndIoId={segment.EndIoId}) 未配置，请先在感应IO配置中添加");
                }
            }

            // 验证第一段线体的起点IO必须是创建包裹感应IO
            if (i == 0)
            {
                var startSensor = sensorConfig.Sensors?.FirstOrDefault(s => s.SensorId == segment.StartIoId);
                if (startSensor == null)
                {
                    return (false, $"第一段线体 {segment.SegmentId} 的起点IO (StartIoId={segment.StartIoId}) 未配置");
                }
                if (startSensor.IoType != SensorIoType.ParcelCreation)
                {
                    return (false, $"第一段线体 {segment.SegmentId} 的起点IO必须是创建包裹感应IO (ParcelCreation)，当前类型为 {startSensor.IoType}");
                }
            }

            // 验证最后一段线体的终点IO应该是0
            if (i == segments.Count - 1 && segment.EndIoId != 0)
            {
                _logger.LogWarning(
                    "线体段配置提示: 最后一段线体 {SegmentId} 的终点IO不是0，建议设为0表示末端",
                    segment.SegmentId);
            }
        }

        return (true, null);
    }

    private LineTopologyResponse MapToResponse(LineTopologyConfig config)
    {
        return new LineTopologyResponse
        {
            TopologyId = config.TopologyId,
            TopologyName = config.TopologyName,
            Description = config.Description,
            WheelNodes = config.WheelNodes.Select(n => new WheelNodeRequest
            {
                NodeId = n.NodeId,
                NodeName = n.NodeName,
                PositionIndex = n.PositionIndex,
                FrontIoId = n.FrontIoId,
                HasLeftChute = n.HasLeftChute,
                HasRightChute = n.HasRightChute,
                Remarks = n.Remarks
            }).ToList(),
            Chutes = config.Chutes.Select(c => new ChuteConfigRequest
            {
                ChuteId = c.ChuteId,
                ChuteName = c.ChuteName,
                IsExceptionChute = c.IsExceptionChute,
                BoundNodeId = c.BoundNodeId,
                BoundDirection = c.BoundDirection,
                DropOffsetMm = c.DropOffsetMm,
                IsEnabled = c.IsEnabled,
                Remarks = c.Remarks
            }).ToList(),
            LineSegments = config.LineSegments.Select(s => new LineSegmentRequest
            {
                SegmentId = s.SegmentId,
                SegmentName = s.SegmentName,
                StartIoId = s.StartIoId,
                EndIoId = s.EndIoId,
                LengthMm = s.LengthMm,
                SpeedMmPerSec = s.SpeedMmPerSec,
                Description = s.Description
            }).ToList(),
            DefaultLineSpeedMmps = config.DefaultLineSpeedMmps,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    private LineTopologyConfig MapToConfig(LineTopologyRequest request)
    {
        var now = _clock.LocalNow;
        
        return new LineTopologyConfig
        {
            TopologyId = LiteDbLineTopologyRepository.DefaultTopologyId,
            TopologyName = request.TopologyName,
            Description = request.Description,
            WheelNodes = request.WheelNodes.Select(n => new WheelNodeConfig
            {
                NodeId = n.NodeId,
                NodeName = n.NodeName,
                PositionIndex = n.PositionIndex,
                FrontIoId = n.FrontIoId,
                HasLeftChute = n.HasLeftChute,
                HasRightChute = n.HasRightChute,
                Remarks = n.Remarks
            }).ToList(),
            Chutes = request.Chutes.Select(c => new ChuteConfig
            {
                ChuteId = c.ChuteId,
                ChuteName = c.ChuteName,
                IsExceptionChute = c.IsExceptionChute,
                BoundNodeId = c.BoundNodeId,
                BoundDirection = c.BoundDirection,
                DropOffsetMm = c.DropOffsetMm,
                IsEnabled = c.IsEnabled,
                Remarks = c.Remarks
            }).ToList(),
            LineSegments = request.LineSegments?.Select(s => new LineSegmentConfig
            {
                SegmentId = s.SegmentId,
                SegmentName = s.SegmentName,
                StartIoId = s.StartIoId,
                EndIoId = s.EndIoId,
                LengthMm = s.LengthMm,
                SpeedMmPerSec = s.SpeedMmPerSec,
                Description = s.Description
            }).ToList() ?? new List<LineSegmentConfig>(),
            DefaultLineSpeedMmps = request.DefaultLineSpeedMmps,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 模拟包裹从指定格口落格的整个生命周期
    /// </summary>
    /// <param name="chuteId">目标格口ID</param>
    /// <param name="toleranceTimeMs">容差时间（毫秒），用于包裹摩擦力补偿，默认为0</param>
    /// <returns>包裹生命周期模拟结果</returns>
    /// <response code="200">模拟成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="404">未找到指定格口或无法生成路径</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 根据当前线体拓扑配置，模拟包裹从创建到落格的整个生命周期。
    /// 
    /// **计算逻辑**：
    /// - 理论通过时间 = Σ(线体段长度 / 线体段速度) * 1000
    /// - 实际通过时间 = 理论通过时间 + 容差时间
    /// 
    /// **容差时间验证**：
    /// - 容差时间应小于放包间隔时间（normalReleaseIntervalMs，默认300ms）的一半
    /// - 即：容差时间 &lt; normalReleaseIntervalMs / 2
    /// - 这样可以确保相邻包裹的超时检测窗口不会重叠
    /// 
    /// **示例请求**：
    /// ```
    /// GET /api/config/line-topology/simulate-lifecycle?chuteId=CHUTE-001&amp;toleranceTimeMs=100
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "success": true,
    ///   "code": "Ok",
    ///   "message": "模拟成功",
    ///   "data": {
    ///     "chuteId": "CHUTE-001",
    ///     "chuteName": "A区01号口",
    ///     "totalTheoreticalTimeMs": 5000.0,
    ///     "toleranceTimeMs": 100,
    ///     "totalActualTimeMs": 5100.0,
    ///     "normalReleaseIntervalMs": 300,
    ///     "isToleranceValid": true,
    ///     "toleranceValidationMessage": "容差时间配置合理",
    ///     "nodeTimings": [
    ///       {
    ///         "nodeType": "LineSegment",
    ///         "nodeId": "1",
    ///         "nodeName": "入口到第一摆轮段",
    ///         "distanceMm": 5000.0,
    ///         "speedMmPerSec": 1000.0,
    ///         "theoreticalTimeMs": 5000.0
    ///       }
    ///     ]
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpGet("simulate-lifecycle")]
    [SwaggerOperation(
        Summary = "模拟包裹生命周期",
        Description = "根据当前配置模拟包裹从创建到指定格口落格的整个生命周期，包括总耗时和各节点耗时",
        OperationId = "SimulateParcelLifecycle",
        Tags = new[] { "线体拓扑配置" }
    )]
    [SwaggerResponse(200, "模拟成功", typeof(ApiResponse<ParcelLifecycleSimulationResponse>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(404, "未找到指定格口", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [ProducesResponseType(typeof(ApiResponse<ParcelLifecycleSimulationResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public ActionResult<ApiResponse<ParcelLifecycleSimulationResponse>> SimulateParcelLifecycle(
        [FromQuery, SwaggerParameter("目标格口ID", Required = true)] string chuteId,
        [FromQuery, SwaggerParameter("容差时间（毫秒），用于包裹摩擦力补偿，默认为0")] int toleranceTimeMs = 0)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(chuteId))
            {
                return BadRequest(ApiResponse<object>.BadRequest("格口ID不能为空 - Chute ID cannot be empty"));
            }

            if (toleranceTimeMs < 0 || toleranceTimeMs > 60000)
            {
                return BadRequest(ApiResponse<object>.BadRequest("容差时间必须在0-60000毫秒之间 - Tolerance time must be between 0-60000ms"));
            }

            var topology = _topologyRepository.Get();
            var chute = topology.FindChuteById(chuteId);

            if (chute == null)
            {
                return NotFound(ApiResponse<object>.NotFound($"未找到格口 {chuteId} - Chute {chuteId} not found"));
            }

            if (topology.LineSegments.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("未配置线体段 - No line segments configured"));
            }

            // 计算各节点耗时
            var nodeTimings = new List<NodeTimingInfo>();
            double totalTheoreticalTimeMs = 0;

            foreach (var segment in topology.LineSegments)
            {
                var transitTimeMs = segment.CalculateTransitTimeMs();
                totalTheoreticalTimeMs += transitTimeMs;

                nodeTimings.Add(new NodeTimingInfo
                {
                    NodeType = "LineSegment",
                    NodeId = segment.SegmentId.ToString(),
                    NodeName = segment.SegmentName ?? $"线体段-{segment.SegmentId}",
                    DistanceMm = segment.LengthMm,
                    SpeedMmPerSec = segment.SpeedMmPerSec,
                    TheoreticalTimeMs = transitTimeMs
                });
            }

            // 如果格口有落格偏移，也计算其时间
            if (chute.DropOffsetMm > 0)
            {
                var dropSpeed = (double)topology.DefaultLineSpeedMmps;
                var dropTimeMs = (chute.DropOffsetMm / dropSpeed) * 1000.0;
                totalTheoreticalTimeMs += dropTimeMs;

                nodeTimings.Add(new NodeTimingInfo
                {
                    NodeType = "DropOffset",
                    NodeId = chuteId,
                    NodeName = $"落格偏移-{chute.ChuteName}",
                    DistanceMm = chute.DropOffsetMm,
                    SpeedMmPerSec = dropSpeed,
                    TheoreticalTimeMs = dropTimeMs
                });
            }

            // 获取当前放包间隔配置（默认300ms）
            const int DefaultNormalReleaseIntervalMs = 300;

            // 验证容差时间是否合理
            // 容差时间应小于放包间隔时间的一半，以确保相邻包裹的超时检测窗口不会重叠
            var maxAllowedToleranceMs = DefaultNormalReleaseIntervalMs / 2.0;
            var isToleranceValid = toleranceTimeMs < maxAllowedToleranceMs;
            var toleranceValidationMessage = isToleranceValid
                ? "容差时间配置合理 - Tolerance time is valid"
                : $"容差时间过大，可能导致相邻包裹超时窗口重叠。建议小于 {maxAllowedToleranceMs:F0}ms - Tolerance time is too large, may cause overlapping timeout windows. Recommended < {maxAllowedToleranceMs:F0}ms";

            var response = new ParcelLifecycleSimulationResponse
            {
                ChuteId = chuteId,
                ChuteName = chute.ChuteName,
                TotalTheoreticalTimeMs = totalTheoreticalTimeMs,
                ToleranceTimeMs = toleranceTimeMs,
                TotalActualTimeMs = totalTheoreticalTimeMs + toleranceTimeMs,
                NormalReleaseIntervalMs = DefaultNormalReleaseIntervalMs,
                IsToleranceValid = isToleranceValid,
                ToleranceValidationMessage = toleranceValidationMessage,
                NodeTimings = nodeTimings
            };

            return Ok(ApiResponse<ParcelLifecycleSimulationResponse>.Ok(response, "模拟成功 - Simulation successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模拟包裹生命周期失败: ChuteId={ChuteId}", chuteId);
            return StatusCode(500, ApiResponse<object>.ServerError("模拟包裹生命周期失败 - Failed to simulate parcel lifecycle"));
        }
    }
}

/// <summary>
/// 包裹生命周期模拟响应
/// </summary>
public record ParcelLifecycleSimulationResponse
{
    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required string ChuteId { get; init; }

    /// <summary>
    /// 格口名称
    /// </summary>
    public string? ChuteName { get; init; }

    /// <summary>
    /// 理论总通过时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 计算公式：Σ(线体段长度 / 线体段速度) * 1000
    /// </remarks>
    public required double TotalTheoreticalTimeMs { get; init; }

    /// <summary>
    /// 容差时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 用于补偿包裹摩擦力等因素造成的时间误差
    /// </remarks>
    public required int ToleranceTimeMs { get; init; }

    /// <summary>
    /// 实际总通过时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 计算公式：理论总通过时间 + 容差时间
    /// </remarks>
    public required double TotalActualTimeMs { get; init; }

    /// <summary>
    /// 正常放包间隔时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 默认值为300ms，表示至少间隔300ms才能创建一个包裹
    /// </remarks>
    public required int NormalReleaseIntervalMs { get; init; }

    /// <summary>
    /// 容差时间是否合理
    /// </summary>
    /// <remarks>
    /// 容差时间应小于放包间隔时间的一半（即 &lt; normalReleaseIntervalMs / 2），
    /// 以确保相邻包裹的超时检测窗口不会重叠
    /// </remarks>
    public required bool IsToleranceValid { get; init; }

    /// <summary>
    /// 容差时间验证消息
    /// </summary>
    public required string ToleranceValidationMessage { get; init; }

    /// <summary>
    /// 各节点耗时详情
    /// </summary>
    public required List<NodeTimingInfo> NodeTimings { get; init; }
}

/// <summary>
/// 节点耗时信息
/// </summary>
public record NodeTimingInfo
{
    /// <summary>
    /// 节点类型（LineSegment - 线体段, DropOffset - 落格偏移）
    /// </summary>
    public required string NodeType { get; init; }

    /// <summary>
    /// 节点ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点名称
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// 距离（毫米）
    /// </summary>
    public required double DistanceMm { get; init; }

    /// <summary>
    /// 速度（毫米/秒）
    /// </summary>
    public required double SpeedMmPerSec { get; init; }

    /// <summary>
    /// 理论通过时间（毫秒）
    /// </summary>
    public required double TheoreticalTimeMs { get; init; }
}
