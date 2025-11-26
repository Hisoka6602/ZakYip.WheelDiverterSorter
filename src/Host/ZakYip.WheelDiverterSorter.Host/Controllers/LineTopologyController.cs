using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums;
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
    private readonly IRouteConfigurationRepository _routeRepository;
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<LineTopologyController> _logger;

    public LineTopologyController(
        ILineTopologyRepository topologyRepository,
        ISensorConfigurationRepository sensorRepository,
        IRouteConfigurationRepository routeRepository,
        ISystemConfigurationRepository systemConfigRepository,
        ISystemClock clock,
        ILogger<LineTopologyController> logger)
    {
        _topologyRepository = topologyRepository;
        _sensorRepository = sensorRepository;
        _routeRepository = routeRepository;
        _systemConfigRepository = systemConfigRepository;
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
    /// <param name="chuteId">目标格口ID（数字ID，如：1, 2, 3）</param>
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
    /// - 实际通过时间 = 理论总通过时间 + 容差时间
    /// 
    /// **模拟步骤类型**：
    /// - **ParcelCreation**: 包裹创建 - 感应IO触发，创建包裹实体，请求上游路由
    /// - **WaitOnConveyor**: 等待线体运行 - 包裹在线体上运行的时间
    /// - **DiverterCommand**: 摆轮指令 - 发送摆轮转向指令
    /// - **PassDiverter**: 通过摆轮 - 包裹通过摆轮，执行转向
    /// - **DropToChute**: 落格 - 包裹落入目标格口
    /// 
    /// **容差时间验证**：
    /// - 容差时间应小于放包间隔时间（normalReleaseIntervalMs，默认300ms）的一半
    /// - 即：容差时间 &lt; normalReleaseIntervalMs / 2
    /// - 这样可以确保相邻包裹的超时检测窗口不会重叠
    /// 
    /// **示例请求**：
    /// ```
    /// GET /api/config/line-topology/simulate-lifecycle?chuteId=1&amp;toleranceTimeMs=100
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "success": true,
    ///   "code": "Ok",
    ///   "message": "模拟成功",
    ///   "data": {
    ///     "chuteId": 1,
    ///     "chuteName": "A区01号口",
    ///     "totalTheoreticalTimeMs": 5000.0,
    ///     "toleranceTimeMs": 100,
    ///     "totalActualTimeMs": 5100.0,
    ///     "normalReleaseIntervalMs": 300,
    ///     "isToleranceValid": true,
    ///     "toleranceValidationMessage": "容差时间配置合理",
    ///     "simulationSteps": [
    ///       {
    ///         "stepNumber": 1,
    ///         "stepType": "ParcelCreation",
    ///         "action": "感应IO触发，创建包裹实体，分配ParcelId，请求上游路由",
    ///         "durationMs": 0,
    ///         "cumulativeTimeMs": 0,
    ///         "details": { "event": "SensorTriggered", "description": "..." }
    ///       },
    ///       {
    ///         "stepNumber": 2,
    ///         "stepType": "WaitOnConveyor",
    ///         "action": "等待线体运行 5000ms (段1，长度5000mm，速度1000mm/s)",
    ///         "durationMs": 5000.0,
    ///         "cumulativeTimeMs": 5000.0,
    ///         "details": { "segmentSequence": 1, "lengthMm": 5000.0, "speedMmPerSec": 1000.0 }
    ///       },
    ///       {
    ///         "stepNumber": 3,
    ///         "stepType": "DiverterCommand",
    ///         "action": "发送摆轮指令: 摆轮#1 右转 (Right)",
    ///         "durationMs": 0,
    ///         "cumulativeTimeMs": 5000.0,
    ///         "details": { "diverterId": 1, "direction": "Right", "directionChinese": "右转" }
    ///       },
    ///       {
    ///         "stepNumber": 4,
    ///         "stepType": "PassDiverter",
    ///         "action": "包裹通过摆轮#1，执行右转",
    ///         "durationMs": 0,
    ///         "cumulativeTimeMs": 5000.0,
    ///         "details": { "diverterId": 1, "direction": "Right", "description": "..." }
    ///       },
    ///       {
    ///         "stepNumber": 5,
    ///         "stepType": "DropToChute",
    ///         "action": "包裹落入格口 A区01号口 (ChuteId=1)",
    ///         "durationMs": 0,
    ///         "cumulativeTimeMs": 5000.0,
    ///         "details": { "chuteId": 1, "chuteName": "A区01号口", "description": "..." }
    ///       }
    ///     ]
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpGet("simulate-lifecycle")]
    [SwaggerOperation(
        Summary = "模拟包裹生命周期",
        Description = "根据当前配置模拟包裹从创建到指定格口落格的整个生命周期，包括详细的各步骤信息（等待线体运行时间、摆轮指令等）",
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
        [FromQuery, SwaggerParameter("目标格口ID（数字ID，如1、2、3）", Required = true)] long chuteId,
        [FromQuery, SwaggerParameter("容差时间（毫秒），用于包裹摩擦力补偿，默认为0")] int toleranceTimeMs = 0)
    {
        try
        {
            if (chuteId <= 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("格口ID必须大于0 - Chute ID must be greater than 0"));
            }

            if (toleranceTimeMs < 0 || toleranceTimeMs > 60000)
            {
                return BadRequest(ApiResponse<object>.BadRequest("容差时间必须在0-60000毫秒之间 - Tolerance time must be between 0-60000ms"));
            }

            var topology = _topologyRepository.Get();
            
            // 获取路由配置
            var routeConfig = _routeRepository.GetByChuteId(chuteId);
            if (routeConfig == null)
            {
                return NotFound(ApiResponse<object>.NotFound($"未找到格口 {chuteId} 的路由配置 - Route configuration for chute {chuteId} not found"));
            }

            if (topology.LineSegments.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("未配置线体段 - No line segments configured"));
            }

            // 获取系统配置中的放包间隔时间
            var systemConfig = _systemConfigRepository.Get();
            var normalReleaseIntervalMs = systemConfig?.ThrottleNormalIntervalMs ?? 300;

            // 构建详细的模拟步骤
            var simulationSteps = new List<SimulationStep>();
            int stepNumber = 0;
            double cumulativeTimeMs = 0;

            // 步骤0：包裹创建（感应IO触发，创建包裹实体）
            stepNumber++;
            simulationSteps.Add(new SimulationStep
            {
                StepNumber = stepNumber,
                StepType = "ParcelCreation",
                Action = "感应IO触发，创建包裹实体，分配ParcelId，请求上游路由",
                DurationMs = 0,
                CumulativeTimeMs = 0,
                Details = new Dictionary<string, object>
                {
                    { "event", "SensorTriggered" },
                    { "description", "包裹到达创建包裹感应IO，系统创建Parcel实体并向RuleEngine请求路由" }
                }
            });

            // 按顺序处理每个摆轮配置
            var sortedDiverters = routeConfig.DiverterConfigurations
                .OrderBy(d => d.SequenceNumber)
                .ToList();

            foreach (var diverterConfig in sortedDiverters)
            {
                // 步骤N.1：等待线体运行（使用摆轮配置中的段长度和速度）
                var segmentTimeMs = (diverterConfig.SegmentLengthMm / diverterConfig.SegmentSpeedMmPerSecond) * 1000.0;
                cumulativeTimeMs += segmentTimeMs;
                stepNumber++;

                simulationSteps.Add(new SimulationStep
                {
                    StepNumber = stepNumber,
                    StepType = "WaitOnConveyor",
                    Action = $"等待线体运行 {segmentTimeMs:F0}ms (段{diverterConfig.SequenceNumber}，长度{diverterConfig.SegmentLengthMm}mm，速度{diverterConfig.SegmentSpeedMmPerSecond}mm/s)",
                    DurationMs = segmentTimeMs,
                    CumulativeTimeMs = cumulativeTimeMs,
                    Details = new Dictionary<string, object>
                    {
                        { "segmentSequence", diverterConfig.SequenceNumber },
                        { "lengthMm", diverterConfig.SegmentLengthMm },
                        { "speedMmPerSec", diverterConfig.SegmentSpeedMmPerSecond },
                        { "toleranceTimeMs", diverterConfig.SegmentToleranceTimeMs }
                    }
                });

                // 步骤N.2：发送摆轮指令（提前发送，确保摆轮在包裹到达前就位）
                stepNumber++;
                var directionName = diverterConfig.TargetDirection.ToString();
                var directionChinese = diverterConfig.TargetDirection switch
                {
                    DiverterDirection.Left => "左转",
                    DiverterDirection.Right => "右转",
                    DiverterDirection.Straight => "直行",
                    _ => directionName
                };

                simulationSteps.Add(new SimulationStep
                {
                    StepNumber = stepNumber,
                    StepType = "DiverterCommand",
                    Action = $"发送摆轮指令: 摆轮#{diverterConfig.DiverterId} {directionChinese} ({directionName})",
                    DurationMs = 0,
                    CumulativeTimeMs = cumulativeTimeMs,
                    Details = new Dictionary<string, object>
                    {
                        { "diverterId", diverterConfig.DiverterId },
                        { "direction", directionName },
                        { "directionChinese", directionChinese },
                        { "description", $"包裹即将通过摆轮#{diverterConfig.DiverterId}，发送转向指令使摆轮{directionChinese}" }
                    }
                });

                // 步骤N.3：包裹通过摆轮（转向执行）
                stepNumber++;
                simulationSteps.Add(new SimulationStep
                {
                    StepNumber = stepNumber,
                    StepType = "PassDiverter",
                    Action = $"包裹通过摆轮#{diverterConfig.DiverterId}，执行{directionChinese}",
                    DurationMs = 0,
                    CumulativeTimeMs = cumulativeTimeMs,
                    Details = new Dictionary<string, object>
                    {
                        { "diverterId", diverterConfig.DiverterId },
                        { "direction", directionName },
                        { "description", "包裹通过摆轮，摆轮转向完成，包裹被导向目标方向" }
                    }
                });
            }

            // 最后一步：落格（包裹进入格口）
            stepNumber++;
            simulationSteps.Add(new SimulationStep
            {
                StepNumber = stepNumber,
                StepType = "DropToChute",
                Action = $"包裹落入格口 {routeConfig.ChuteName ?? $"Chute-{chuteId}"} (ChuteId={chuteId})",
                DurationMs = 0,
                CumulativeTimeMs = cumulativeTimeMs,
                Details = new Dictionary<string, object>
                {
                    { "chuteId", chuteId },
                    { "chuteName", routeConfig.ChuteName ?? $"Chute-{chuteId}" },
                    { "description", "包裹成功落入目标格口，生命周期结束" }
                }
            });

            // 验证容差时间是否合理
            var maxAllowedToleranceMs = normalReleaseIntervalMs / 2.0;
            var isToleranceValid = toleranceTimeMs < maxAllowedToleranceMs;
            var toleranceValidationMessage = isToleranceValid
                ? "容差时间配置合理 - Tolerance time is valid"
                : $"容差时间过大，可能导致相邻包裹超时窗口重叠。建议小于 {maxAllowedToleranceMs:F0}ms - Tolerance time is too large, may cause overlapping timeout windows. Recommended < {maxAllowedToleranceMs:F0}ms";

            var response = new ParcelLifecycleSimulationResponse
            {
                ChuteId = chuteId,
                ChuteName = routeConfig.ChuteName,
                TotalTheoreticalTimeMs = cumulativeTimeMs,
                ToleranceTimeMs = toleranceTimeMs,
                TotalActualTimeMs = cumulativeTimeMs + toleranceTimeMs,
                NormalReleaseIntervalMs = normalReleaseIntervalMs,
                IsToleranceValid = isToleranceValid,
                ToleranceValidationMessage = toleranceValidationMessage,
                SimulationSteps = simulationSteps
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
    /// 目标格口ID（数字ID）
    /// </summary>
    public required long ChuteId { get; init; }

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
    /// 详细模拟步骤列表
    /// </summary>
    /// <remarks>
    /// 包含每个步骤的详细信息：等待线体运行时间、摆轮指令、落格等
    /// </remarks>
    public required List<SimulationStep> SimulationSteps { get; init; }
}

/// <summary>
/// 模拟步骤信息
/// </summary>
public record SimulationStep
{
    /// <summary>
    /// 步骤序号（从1开始）
    /// </summary>
    public required int StepNumber { get; init; }

    /// <summary>
    /// 步骤类型
    /// </summary>
    /// <remarks>
    /// 可能的值：
    /// - WaitOnConveyor: 等待线体运行
    /// - DiverterCommand: 发送摆轮指令
    /// - DropToChute: 落格到格口
    /// </remarks>
    public required string StepType { get; init; }

    /// <summary>
    /// 动作描述
    /// </summary>
    /// <example>等待线体运行 5000ms (入口到第一摆轮段，长度5000mm，速度1000mm/s)</example>
    public required string Action { get; init; }

    /// <summary>
    /// 本步骤持续时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 对于WaitOnConveyor类型，表示等待时间；
    /// 对于DiverterCommand和DropToChute类型，通常为0
    /// </remarks>
    public required double DurationMs { get; init; }

    /// <summary>
    /// 累计时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 从包裹创建到当前步骤结束的累计时间
    /// </remarks>
    public required double CumulativeTimeMs { get; init; }

    /// <summary>
    /// 步骤详情
    /// </summary>
    /// <remarks>
    /// 包含步骤的具体参数，不同类型的步骤有不同的详情字段：
    /// - WaitOnConveyor: segmentSequence, lengthMm, speedMmPerSec, toleranceTimeMs
    /// - DiverterCommand: diverterId, direction, directionChinese
    /// - DropToChute: chuteId, chuteName
    /// </remarks>
    public required Dictionary<string, object> Details { get; init; }
}
