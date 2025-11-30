using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Simulation;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 格口路径拓扑配置管理API控制器
/// </summary>
/// <remarks>
/// <para>提供格口路径拓扑配置的管理接口。</para>
/// <para>本配置通过引用其他配置中已定义的ID来组织路径关系：</para>
/// <list type="bullet">
/// <item>IO配置 - 引用 SensorConfiguration 中的 SensorId</item>
/// <item>线体段 - 通过 DiverterPathNode.SegmentId 关联</item>
/// <item>摆轮配置 - 引用 WheelDiverterConfiguration 中的 DiverterId</item>
/// </list>
/// 
/// <para><b>拓扑结构示例：</b></para>
/// <code>
///       格口B     格口D     格口F
///         ↑         ↑         ↑
/// 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(默认异常口)
///   ↓     ↓         ↓         ↓
/// 传感器  格口A      格口C     格口E
/// </code>
/// </remarks>
[ApiController]
[Route("api/config/chute-path-topology")]
[Produces("application/json")]
public class ChutePathTopologyController : ControllerBase
{
    /// <summary>
    /// 模拟使用的默认线速（毫米/秒）= 1 m/s
    /// </summary>
    private const decimal SimulationDefaultLineSpeedMmps = 1000m;
    
    /// <summary>
    /// 模拟使用的默认线体段长度（毫米）= 5 m
    /// </summary>
    private const double SimulationDefaultSegmentLengthMm = 5000;
    
    /// <summary>
    /// 拓扑图中每列的宽度（字符数）
    /// </summary>
    private const int DiagramColumnWidth = 12;
    
    /// <summary>
    /// 拓扑图中箭头符号的额外占用宽度（" →" 占用3字符）
    /// </summary>
    private const int DiagramArrowPadding = 3;
    
    /// <summary>
    /// 空的格口ID只读列表（用于避免重复分配）
    /// </summary>
    private static readonly IReadOnlyList<long> EmptyChuteIds = Array.Empty<long>();

    private readonly IChutePathTopologyService _topologyService;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChutePathTopologyController> _logger;

    /// <summary>
    /// 初始化格口路径拓扑配置控制器
    /// </summary>
    public ChutePathTopologyController(
        IChutePathTopologyService topologyService,
        ISystemClock clock,
        ILogger<ChutePathTopologyController> logger)
    {
        _topologyService = topologyService;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// 获取格口路径拓扑配置
    /// </summary>
    /// <returns>格口路径拓扑配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取格口路径拓扑配置",
        Description = "返回完整的格口路径拓扑配置，包括入口传感器、摆轮路径节点、异常格口等信息",
        OperationId = "GetChutePathTopology",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<ChutePathTopologyResponse>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<ChutePathTopologyResponse>> GetChutePathTopology()
    {
        try
        {
            var config = _topologyService.GetTopology();
            var response = MapToResponse(config);
            return Ok(ApiResponse<ChutePathTopologyResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取格口路径拓扑配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取格口路径拓扑配置失败 - Failed to get chute path topology configuration"));
        }
    }

    /// <summary>
    /// 获取格口路径拓扑图（ASCII文本格式）
    /// </summary>
    /// <returns>ASCII格式的拓扑图</returns>
    /// <response code="200">成功返回拓扑图</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// <para>返回当前配置的拓扑结构的ASCII文本图表，便于可视化理解配置。</para>
    /// <para>示例输出：</para>
    /// <code>
    ///       格口B     格口D     格口F
    ///         ↑         ↑         ↑
    /// 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(异常口999)
    ///   ↓     ↓         ↓         ↓
    /// 传感器  格口A      格口C     格口E
    /// </code>
    /// </remarks>
    [HttpGet("diagram")]
    [SwaggerOperation(
        Summary = "获取格口路径拓扑图",
        Description = "返回ASCII格式的拓扑图，便于可视化查看当前配置的拓扑结构",
        OperationId = "GetChutePathTopologyDiagram",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "成功返回拓扑图", typeof(ApiResponse<TopologyDiagramResponse>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<TopologyDiagramResponse>> GetTopologyDiagram()
    {
        try
        {
            var config = _topologyService.GetTopology();
            var diagram = GenerateTopologyDiagram(config);
            
            var response = new TopologyDiagramResponse
            {
                TopologyName = config.TopologyName,
                Description = config.Description,
                Diagram = diagram,
                DiverterCount = config.DiverterNodes.Count,
                TotalChuteCount = config.TotalChuteCount,
                EntrySensorId = config.EntrySensorId,
                ExceptionChuteId = config.ExceptionChuteId
            };
            
            return Ok(ApiResponse<TopologyDiagramResponse>.Ok(response, "拓扑图生成成功 - Topology diagram generated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成格口路径拓扑图失败");
            return StatusCode(500, ApiResponse<object>.ServerError("生成格口路径拓扑图失败 - Failed to generate chute path topology diagram"));
        }
    }

    /// <summary>
    /// 更新格口路径拓扑配置
    /// </summary>
    /// <param name="request">格口路径拓扑配置请求</param>
    /// <returns>更新后的格口路径拓扑配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     PUT /api/config/chute-path-topology
    ///     {
    ///         "topologyName": "标准格口路径拓扑",
    ///         "description": "3摆轮6格口的标准配置",
    ///         "entrySensorId": 1,
    ///         "diverterNodes": [
    ///             {
    ///                 "diverterId": 1,
    ///                 "diverterName": "摆轮D1",
    ///                 "positionIndex": 1,
    ///                 "segmentId": 1,
    ///                 "frontSensorId": 2,
    ///                 "leftChuteIds": [2],
    ///                 "rightChuteIds": [1]
    ///             },
    ///             {
    ///                 "diverterId": 2,
    ///                 "diverterName": "摆轮D2",
    ///                 "positionIndex": 2,
    ///                 "segmentId": 2,
    ///                 "frontSensorId": 3,
    ///                 "leftChuteIds": [4],
    ///                 "rightChuteIds": [3]
    ///             },
    ///             {
    ///                 "diverterId": 3,
    ///                 "diverterName": "摆轮D3",
    ///                 "positionIndex": 3,
    ///                 "segmentId": 3,
    ///                 "frontSensorId": 4,
    ///                 "leftChuteIds": [6],
    ///                 "rightChuteIds": [5]
    ///             }
    ///         ],
    ///         "exceptionChuteId": 999
    ///     }
    ///
    /// 配置说明：
    /// - entrySensorId 必须引用一个已配置的 ParcelCreation 类型的感应IO
    /// - diverterNodes 中的 segmentId 必须引用已配置的线体段（线体速度在线体段配置中定义）
    /// - diverterNodes 中的 frontSensorId（可选）必须引用 WheelFront 类型的感应IO
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新格口路径拓扑配置",
        Description = "更新完整的格口路径拓扑配置，配置立即生效。所有引用的ID必须在对应的配置中已存在。",
        OperationId = "UpdateChutePathTopology",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<ChutePathTopologyResponse>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<ChutePathTopologyResponse>> UpdateChutePathTopology([FromBody] ChutePathTopologyRequest request)
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

            // 将请求转换为配置模型的节点列表用于验证
            var diverterNodes = MapToDiverterNodes(request.DiverterNodes);

            // 使用 Application 服务进行验证
            var (isValid, errorMessage) = _topologyService.ValidateTopologyRequest(
                request.EntrySensorId,
                diverterNodes,
                request.ExceptionChuteId);

            if (!isValid)
            {
                return BadRequest(ApiResponse<object>.BadRequest(errorMessage!));
            }

            var config = MapToConfig(request);
            _topologyService.UpdateTopology(config);

            _logger.LogInformation(
                "格口路径拓扑配置已更新: TopologyName={TopologyName}, DiverterNodes={NodeCount}, EntrySensorId={EntrySensorId}, ExceptionChuteId={ExceptionChuteId}",
                config.TopologyName,
                config.DiverterNodes.Count,
                config.EntrySensorId,
                config.ExceptionChuteId);

            var response = MapToResponse(config);
            return Ok(ApiResponse<ChutePathTopologyResponse>.Ok(response, "格口路径拓扑配置已更新 - Chute path topology configuration updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新格口路径拓扑配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("更新格口路径拓扑配置失败 - Failed to update chute path topology configuration"));
        }
    }

    /// <summary>
    /// 模拟包裹分拣路径测试
    /// </summary>
    /// <param name="request">模拟测试请求参数</param>
    /// <returns>模拟的包裹分拣全过程详情</returns>
    /// <response code="200">模拟测试成功</response>
    /// <response code="400">请求参数无效、格口不存在或拓扑配置不完整</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// <para>此端点用于测试拓扑配置是否正确，模拟包裹从入口到指定格口的完整分拣过程。</para>
    /// 
    /// <para><b>重要</b>：必须先完成拓扑配置才能进行模拟测试。线体速度和长度参数从已配置的拓扑中读取。</para>
    /// 
    /// <para>可配置的模拟参数：</para>
    /// <list type="bullet">
    /// <item>targetChuteId: 目标格口ID</item>
    /// <item>simulateTimeout: 是否模拟超时场景</item>
    /// <item>simulateParcelLoss: 是否模拟丢包场景</item>
    /// <item>parcelLossAtDiverterIndex: 在第几个摆轮处丢包（从1开始）</item>
    /// </list>
    /// 
    /// <para>返回结果包含：</para>
    /// <list type="bullet">
    /// <item>包裹基本信息</item>
    /// <item>完整的路径节点列表及每个节点的详细信息</item>
    /// <item>每个节点的耗时统计</item>
    /// <item>最终分拣结果</item>
    /// </list>
    /// </remarks>
    [HttpPost("simulate")]
    [SwaggerOperation(
        Summary = "模拟包裹分拣路径测试",
        Description = "模拟包裹从入口到指定格口的完整分拣过程，用于验证拓扑配置是否正确。必须先完成拓扑配置（至少配置一个摆轮节点）才能进行模拟。",
        OperationId = "SimulateChutePathTopology",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "模拟测试成功", typeof(ApiResponse<TopologySimulationResult>))]
    [SwaggerResponse(400, "请求参数无效、格口不存在或拓扑配置不完整", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<TopologySimulationResult>> SimulateParcelPath([FromBody] TopologySimulationRequest request)
    {
        try
        {
            var config = _topologyService.GetTopology();
            
            // 验证拓扑配置是否完整
            if (config.DiverterNodes.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest(
                    "拓扑配置不完整，至少需要配置一个摆轮节点才能进行模拟测试 - Topology configuration is incomplete. At least one diverter node must be configured before simulation."));
            }
            
            // Validate target chute exists in topology
            var targetNode = config.FindNodeByChuteId(request.TargetChuteId);
            if (targetNode == null && request.TargetChuteId != config.ExceptionChuteId)
            {
                return BadRequest(ApiResponse<object>.BadRequest(
                    $"格口 {request.TargetChuteId} 不存在于拓扑配置中 - Chute {request.TargetChuteId} does not exist in topology configuration"));
            }

            // Get path to target chute
            var pathNodes = config.GetPathToChute(request.TargetChuteId);
            var isExceptionChute = request.TargetChuteId == config.ExceptionChuteId;
            
            if (pathNodes == null && !isExceptionChute)
            {
                return BadRequest(ApiResponse<object>.BadRequest(
                    $"无法找到到达格口 {request.TargetChuteId} 的路径 - Cannot find path to chute {request.TargetChuteId}"));
            }

            // If targeting exception chute, path goes through all nodes
            if (isExceptionChute || pathNodes == null)
            {
                pathNodes = config.DiverterNodes.OrderBy(n => n.PositionIndex).ToList();
            }

            // Get sorting direction for target chute
            var sortingDirection = config.GetChuteDirection(request.TargetChuteId);

            // Start simulation
            var simulationStartTime = _clock.LocalNow;
            var parcelId = $"SIM-{simulationStartTime:yyyyMMddHHmmss}-{request.TargetChuteId}";
            
            var result = new TopologySimulationResult
            {
                ParcelId = parcelId,
                TargetChuteId = request.TargetChuteId,
                IsExceptionChute = isExceptionChute,
                SimulationStartTime = simulationStartTime,
                SimulateTimeout = request.SimulateTimeout,
                SimulateParcelLoss = request.SimulateParcelLoss,
                Steps = new List<SimulationStep>()
            };

            var currentTime = simulationStartTime;
            var totalDistanceMm = 0.0;
            var isParcelLost = false;
            var isTimeout = false;

            // Step 1: Parcel Creation at Entry Sensor
            var creationStep = new SimulationStep
            {
                StepNumber = 1,
                StepType = SimulationStepType.ParcelCreation,
                Description = $"包裹在入口传感器(ID:{config.EntrySensorId})处创建 - Parcel created at entry sensor",
                NodeId = null,
                NodeName = $"入口传感器 (Sensor ID: {config.EntrySensorId})",
                StartTime = currentTime,
                EndTime = currentTime,
                DurationMs = 0,
                CumulativeTimeMs = 0,
                Status = StepStatus.Success,
                Details = new Dictionary<string, object>
                {
                    ["parcelId"] = parcelId,
                    ["entrySensorId"] = config.EntrySensorId
                }
            };
            result.Steps.Add(creationStep);

            // Step 2: Request routing from upstream
            currentTime = currentTime.AddMilliseconds(request.RoutingRequestDelayMs);
            var routingStep = new SimulationStep
            {
                StepNumber = 2,
                StepType = SimulationStepType.RoutingRequest,
                Description = $"向上游请求路由，目标格口: {request.TargetChuteId} - Requesting routing from upstream",
                NodeId = null,
                NodeName = "上游路由服务 (Upstream Routing Service)",
                StartTime = creationStep.EndTime,
                EndTime = currentTime,
                DurationMs = request.RoutingRequestDelayMs,
                CumulativeTimeMs = request.RoutingRequestDelayMs,
                Status = StepStatus.Success,
                Details = new Dictionary<string, object>
                {
                    ["targetChuteId"] = request.TargetChuteId,
                    ["routingDirection"] = sortingDirection?.ToString() ?? "Straight"
                }
            };
            result.Steps.Add(routingStep);

            long cumulativeTimeMs = request.RoutingRequestDelayMs;
            var stepNumber = 3;

            // Process each diverter node
            foreach (var node in pathNodes)
            {
                // Use class-level default values for simulation
                var segmentLengthMm = SimulationDefaultSegmentLengthMm;
                var lineSpeedMmps = SimulationDefaultLineSpeedMmps;
                var transitTimeMs = (segmentLengthMm / (double)lineSpeedMmps) * 1000;
                
                // Add tolerance for variation
                if (request.SimulateTimeout && node.DiverterId == pathNodes.Last().DiverterId)
                {
                    transitTimeMs += request.TimeoutExtraDelayMs;
                }

                totalDistanceMm += segmentLengthMm;
                var transitStartTime = currentTime;
                currentTime = currentTime.AddMilliseconds(transitTimeMs);

                // Transit step
                var transitStep = new SimulationStep
                {
                    StepNumber = stepNumber++,
                    StepType = SimulationStepType.Transit,
                    Description = $"包裹从上一节点运输到摆轮 {node.DiverterName ?? $"D{node.DiverterId}"} - Parcel transit to diverter",
                    NodeId = node.DiverterId,
                    NodeName = node.DiverterName ?? $"摆轮 D{node.DiverterId}",
                    StartTime = transitStartTime,
                    EndTime = currentTime,
                    DurationMs = (long)transitTimeMs,
                    CumulativeTimeMs = cumulativeTimeMs + (long)transitTimeMs,
                    Status = StepStatus.Success,
                    Details = new Dictionary<string, object>
                    {
                        ["segmentId"] = node.SegmentId,
                        ["segmentLengthMm"] = segmentLengthMm,
                        ["speedMmps"] = lineSpeedMmps
                    }
                };
                
                cumulativeTimeMs += (long)transitTimeMs;

                // Check for simulated parcel loss
                if (request.SimulateParcelLoss && node.PositionIndex == request.ParcelLossAtDiverterIndex)
                {
                    transitStep.Status = StepStatus.Failed;
                    transitStep.Description = $"[模拟丢包] 包裹在摆轮 {node.DiverterName ?? $"D{node.DiverterId}"} 处丢失 - [Simulated] Parcel lost at diverter";
                    transitStep.Details["parcelLost"] = true;
                    isParcelLost = true;
                    result.Steps.Add(transitStep);
                    break;
                }

                // Check for timeout
                if (request.SimulateTimeout && node.DiverterId == pathNodes.Last().DiverterId)
                {
                    transitStep.Status = StepStatus.Timeout;
                    transitStep.Description = $"[模拟超时] 包裹到达摆轮 {node.DiverterName ?? $"D{node.DiverterId}"} 超时 - [Simulated] Parcel arrival timeout";
                    transitStep.Details["timeout"] = true;
                    isTimeout = true;
                }

                result.Steps.Add(transitStep);

                // Front sensor detection (FrontSensorId is now required)
                var sensorDetectionStep = new SimulationStep
                {
                    StepNumber = stepNumber++,
                    StepType = SimulationStepType.SensorDetection,
                    Description = $"摆轮前感应器(ID:{node.FrontSensorId})检测到包裹 - Front sensor detected parcel",
                    NodeId = node.DiverterId,
                    NodeName = $"感应器 (Sensor ID: {node.FrontSensorId})",
                    StartTime = currentTime,
                    EndTime = currentTime.AddMilliseconds(request.SensorDetectionDelayMs),
                    DurationMs = request.SensorDetectionDelayMs,
                    CumulativeTimeMs = cumulativeTimeMs + request.SensorDetectionDelayMs,
                    Status = isTimeout ? StepStatus.Timeout : StepStatus.Success,
                    Details = new Dictionary<string, object>
                    {
                        ["sensorId"] = node.FrontSensorId
                    }
                };
                currentTime = sensorDetectionStep.EndTime;
                cumulativeTimeMs += request.SensorDetectionDelayMs;
                result.Steps.Add(sensorDetectionStep);

                // Diverter action (determine direction based on target chute)
                var isTargetDiverter = node.LeftChuteIds.Contains(request.TargetChuteId) || 
                                       node.RightChuteIds.Contains(request.TargetChuteId);
                
                string diverterAction;
                DiverterDirection actionDirection;
                
                if (isTargetDiverter)
                {
                    if (node.LeftChuteIds.Contains(request.TargetChuteId))
                    {
                        diverterAction = "左转分拣 - Turn Left";
                        actionDirection = DiverterDirection.Left;
                    }
                    else
                    {
                        diverterAction = "右转分拣 - Turn Right";
                        actionDirection = DiverterDirection.Right;
                    }
                }
                else
                {
                    diverterAction = "直行通过 - Pass Straight";
                    actionDirection = DiverterDirection.Straight;
                }

                var diverterStep = new SimulationStep
                {
                    StepNumber = stepNumber++,
                    StepType = SimulationStepType.DiverterAction,
                    Description = $"摆轮 {node.DiverterName ?? $"D{node.DiverterId}"} 执行 {diverterAction}",
                    NodeId = node.DiverterId,
                    NodeName = node.DiverterName ?? $"摆轮 D{node.DiverterId}",
                    StartTime = currentTime,
                    EndTime = currentTime.AddMilliseconds(request.DiverterActionDelayMs),
                    DurationMs = request.DiverterActionDelayMs,
                    CumulativeTimeMs = cumulativeTimeMs + request.DiverterActionDelayMs,
                    Status = StepStatus.Success,
                    Details = new Dictionary<string, object>
                    {
                        ["diverterId"] = node.DiverterId,
                        ["action"] = diverterAction,
                        ["direction"] = actionDirection.ToString(),
                        ["isTargetDiverter"] = isTargetDiverter,
                        ["leftChuteIds"] = node.LeftChuteIds,
                        ["rightChuteIds"] = node.RightChuteIds
                    }
                };
                currentTime = diverterStep.EndTime;
                cumulativeTimeMs += request.DiverterActionDelayMs;
                result.Steps.Add(diverterStep);

                // If this is the target diverter, parcel leaves the main line
                if (isTargetDiverter)
                {
                    break;
                }
            }

            // Final step: Parcel arrives at chute (if not lost)
            if (!isParcelLost)
            {
                var actualChuteId = isTimeout ? config.ExceptionChuteId : request.TargetChuteId;
                var finalStep = new SimulationStep
                {
                    StepNumber = stepNumber,
                    StepType = SimulationStepType.ChuteArrival,
                    Description = isTimeout 
                        ? $"包裹超时，路由到异常格口 {actualChuteId} - Parcel timeout, routed to exception chute"
                        : $"包裹成功落入格口 {actualChuteId} - Parcel successfully sorted to chute",
                    NodeId = actualChuteId,
                    NodeName = $"格口 {actualChuteId}",
                    StartTime = currentTime,
                    EndTime = currentTime,
                    DurationMs = 0,
                    CumulativeTimeMs = cumulativeTimeMs,
                    Status = isTimeout ? StepStatus.RoutedToException : StepStatus.Success,
                    Details = new Dictionary<string, object>
                    {
                        ["chuteId"] = actualChuteId,
                        ["isExceptionChute"] = isTimeout || isExceptionChute
                    }
                };
                result.Steps.Add(finalStep);
                result.ActualChuteId = actualChuteId;
            }

            // Set final result
            result.SimulationEndTime = currentTime;
            result.TotalDurationMs = cumulativeTimeMs;
            result.TotalDistanceMm = totalDistanceMm;
            result.IsSuccess = !isParcelLost && !isTimeout;
            result.IsParcelLost = isParcelLost;
            result.IsTimeout = isTimeout;
            result.DiverterCount = pathNodes.Count;
            result.Summary = isParcelLost 
                ? $"包裹在第 {request.ParcelLossAtDiverterIndex} 个摆轮处丢失 - Parcel lost at diverter {request.ParcelLossAtDiverterIndex}"
                : isTimeout 
                    ? $"包裹超时，已路由到异常格口 {config.ExceptionChuteId} - Parcel timeout, routed to exception chute"
                    : $"包裹成功分拣到格口 {request.TargetChuteId}，耗时 {cumulativeTimeMs}ms - Parcel successfully sorted";

            _logger.LogInformation(
                "拓扑模拟测试完成: ParcelId={ParcelId}, TargetChute={TargetChute}, Success={Success}, Duration={Duration}ms",
                parcelId, request.TargetChuteId, result.IsSuccess, cumulativeTimeMs);

            return Ok(ApiResponse<TopologySimulationResult>.Ok(result, "模拟测试完成 - Simulation completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拓扑模拟测试失败");
            return StatusCode(500, ApiResponse<object>.ServerError("拓扑模拟测试失败 - Topology simulation failed"));
        }
    }

    /// <summary>
    /// 导出格口路径拓扑配置为JSON格式
    /// </summary>
    /// <returns>JSON格式的配置文件</returns>
    /// <response code="200">成功导出配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("export/json")]
    [SwaggerOperation(
        Summary = "导出格口路径拓扑配置为JSON格式",
        Description = "导出当前格口路径拓扑配置为JSON文件，可用于备份或迁移",
        OperationId = "ExportChutePathTopologyJson",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "成功导出配置")]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [Produces("application/json")]
    public ActionResult ExportAsJson()
    {
        try
        {
            var config = _topologyService.GetTopology();
            var response = MapToResponse(config);
            
            var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            var fileName = $"chute-path-topology-{_clock.LocalNow:yyyyMMdd-HHmmss}.json";
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出格口路径拓扑配置为JSON失败");
            return StatusCode(500, ApiResponse<object>.ServerError("导出格口路径拓扑配置失败 - Failed to export chute path topology configuration"));
        }
    }

    /// <summary>
    /// 导出格口路径拓扑配置为CSV格式
    /// </summary>
    /// <returns>CSV格式的配置文件</returns>
    /// <response code="200">成功导出配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// CSV格式说明：
    /// - 第一行为摆轮节点的列头
    /// - 每行代表一个摆轮节点
    /// - LeftChuteIds和RightChuteIds使用分号分隔多个值
    /// </remarks>
    [HttpGet("export/csv")]
    [SwaggerOperation(
        Summary = "导出格口路径拓扑配置为CSV格式",
        Description = "导出当前格口路径拓扑配置为CSV文件，便于在Excel等工具中查看和编辑",
        OperationId = "ExportChutePathTopologyCsv",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "成功导出配置")]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [Produces("text/csv")]
    public ActionResult ExportAsCsv()
    {
        try
        {
            var config = _topologyService.GetTopology();
            
            var sb = new System.Text.StringBuilder();
            
            // 添加配置元数据注释
            sb.AppendLine($"# TopologyName: {config.TopologyName}");
            sb.AppendLine($"# Description: {config.Description ?? ""}");
            sb.AppendLine($"# EntrySensorId: {config.EntrySensorId}");
            sb.AppendLine($"# ExceptionChuteId: {config.ExceptionChuteId}");
            sb.AppendLine();
            
            // CSV列头
            sb.AppendLine("DiverterId,DiverterName,PositionIndex,SegmentId,FrontSensorId,LeftChuteIds,RightChuteIds,Remarks");
            
            // 数据行
            foreach (var node in config.DiverterNodes)
            {
                var leftChutes = string.Join(";", node.LeftChuteIds);
                var rightChutes = string.Join(";", node.RightChuteIds);
                var name = EscapeCsvField(node.DiverterName ?? "");
                var remarks = EscapeCsvField(node.Remarks ?? "");
                
                sb.AppendLine($"{node.DiverterId},{name},{node.PositionIndex},{node.SegmentId},{node.FrontSensorId},{leftChutes},{rightChutes},{remarks}");
            }

            var fileName = $"chute-path-topology-{_clock.LocalNow:yyyyMMdd-HHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出格口路径拓扑配置为CSV失败");
            return StatusCode(500, ApiResponse<object>.ServerError("导出格口路径拓扑配置为CSV失败 - Failed to export chute path topology configuration as CSV"));
        }
    }

    /// <summary>
    /// 从JSON文件导入格口路径拓扑配置
    /// </summary>
    /// <param name="file">JSON配置文件</param>
    /// <returns>导入后的格口路径拓扑配置</returns>
    /// <response code="200">导入成功</response>
    /// <response code="400">文件格式无效</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("import/json")]
    [SwaggerOperation(
        Summary = "从JSON文件导入格口路径拓扑配置",
        Description = "从JSON文件导入格口路径拓扑配置，将覆盖当前配置",
        OperationId = "ImportChutePathTopologyJson",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "导入成功", typeof(ApiResponse<ChutePathTopologyResponse>))]
    [SwaggerResponse(400, "文件格式无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ChutePathTopologyResponse>>> ImportFromJson(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("请选择要导入的JSON文件 - Please select a JSON file to import"));
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.BadRequest("文件必须是JSON格式 - File must be in JSON format"));
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var json = await reader.ReadToEndAsync();

            var request = System.Text.Json.JsonSerializer.Deserialize<ChutePathTopologyRequest>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null)
            {
                return BadRequest(ApiResponse<object>.BadRequest("JSON文件内容无效 - Invalid JSON file content"));
            }

            // 重用更新逻辑进行验证和保存
            return await Task.FromResult(UpdateChutePathTopology(request));
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "导入的JSON文件格式无效");
            return BadRequest(ApiResponse<object>.BadRequest($"JSON文件格式无效: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从JSON导入格口路径拓扑配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("导入格口路径拓扑配置失败 - Failed to import chute path topology configuration"));
        }
    }

    /// <summary>
    /// 从CSV文件导入格口路径拓扑配置
    /// </summary>
    /// <param name="file">CSV配置文件</param>
    /// <param name="topologyName">拓扑配置名称</param>
    /// <param name="entrySensorId">入口传感器ID</param>
    /// <param name="exceptionChuteId">异常格口ID</param>
    /// <param name="description">拓扑描述（可选）</param>
    /// <returns>导入后的格口路径拓扑配置</returns>
    /// <response code="200">导入成功</response>
    /// <response code="400">文件格式无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// CSV格式要求：
    /// - 第一行为列头（可以有#开头的注释行）
    /// - 列: DiverterId,DiverterName,PositionIndex,SegmentId,FrontSensorId,LeftChuteIds,RightChuteIds,Remarks
    /// - LeftChuteIds和RightChuteIds使用分号分隔多个值
    /// </remarks>
    [HttpPost("import/csv")]
    [SwaggerOperation(
        Summary = "从CSV文件导入格口路径拓扑配置",
        Description = "从CSV文件导入摆轮节点配置，需要提供拓扑元数据参数",
        OperationId = "ImportChutePathTopologyCsv",
        Tags = new[] { "格口路径拓扑配置" }
    )]
    [SwaggerResponse(200, "导入成功", typeof(ApiResponse<ChutePathTopologyResponse>))]
    [SwaggerResponse(400, "文件格式无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ChutePathTopologyResponse>>> ImportFromCsv(
        IFormFile file,
        [FromQuery] string topologyName,
        [FromQuery] long entrySensorId,
        [FromQuery] long exceptionChuteId,
        [FromQuery] string? description = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("请选择要导入的CSV文件 - Please select a CSV file to import"));
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.BadRequest("文件必须是CSV格式 - File must be in CSV format"));
            }

            if (string.IsNullOrWhiteSpace(topologyName))
            {
                return BadRequest(ApiResponse<object>.BadRequest("拓扑配置名称不能为空 - Topology name is required"));
            }

            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var diverterNodes = new List<DiverterPathNodeRequest>();
            var headerFound = false;

            // CSV column indices
            const int ColDiverterId = 0;
            const int ColDiverterName = 1;
            const int ColPositionIndex = 2;
            const int ColSegmentId = 3;
            const int ColFrontSensorId = 4;
            const int ColLeftChuteIds = 5;
            const int ColRightChuteIds = 6;
            const int ColRemarks = 7;
            const int MinRequiredColumns = 7;

            var lineNumber = 0;
            foreach (var line in lines)
            {
                lineNumber++;
                var trimmedLine = line.Trim();
                
                // Skip comment lines and empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
                {
                    continue;
                }

                // Skip header line (look for DiverterId column header)
                if (!headerFound && trimmedLine.Contains("DiverterId", StringComparison.OrdinalIgnoreCase))
                {
                    headerFound = true;
                    continue;
                }

                var fields = ParseCsvLine(trimmedLine);
                if (fields.Length < MinRequiredColumns)
                {
                    _logger.LogWarning("Skipping line {LineNumber} in CSV: insufficient columns ({ActualCount} < {RequiredCount})",
                        lineNumber, fields.Length, MinRequiredColumns);
                    continue;
                }

                // Parse with explicit error handling for each field
                if (!long.TryParse(fields[ColDiverterId].Trim(), out var diverterId))
                {
                    throw new FormatException($"Line {lineNumber}: Invalid DiverterId value '{fields[ColDiverterId]}'");
                }
                if (!int.TryParse(fields[ColPositionIndex].Trim(), out var positionIndex))
                {
                    throw new FormatException($"Line {lineNumber}: Invalid PositionIndex value '{fields[ColPositionIndex]}'");
                }
                if (!long.TryParse(fields[ColSegmentId].Trim(), out var segmentId))
                {
                    throw new FormatException($"Line {lineNumber}: Invalid SegmentId value '{fields[ColSegmentId]}'");
                }

                // FrontSensorId is now required
                if (string.IsNullOrWhiteSpace(fields[ColFrontSensorId]))
                {
                    throw new FormatException($"Line {lineNumber}: FrontSensorId is required but not provided");
                }
                if (!long.TryParse(fields[ColFrontSensorId].Trim(), out var frontSensorId))
                {
                    throw new FormatException($"Line {lineNumber}: Invalid FrontSensorId value '{fields[ColFrontSensorId]}'");
                }

                var node = new DiverterPathNodeRequest
                {
                    DiverterId = diverterId,
                    DiverterName = fields[ColDiverterName].Trim(),
                    PositionIndex = positionIndex,
                    SegmentId = segmentId,
                    FrontSensorId = frontSensorId,
                    LeftChuteIds = ParseChuteIds(fields[ColLeftChuteIds], lineNumber, "LeftChuteIds"),
                    RightChuteIds = ParseChuteIds(fields[ColRightChuteIds], lineNumber, "RightChuteIds"),
                    Remarks = fields.Length > ColRemarks ? fields[ColRemarks].Trim() : null
                };

                diverterNodes.Add(node);
            }

            if (diverterNodes.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("CSV文件中没有有效的摆轮节点数据 - No valid diverter node data found in CSV file"));
            }

            var request = new ChutePathTopologyRequest
            {
                TopologyName = topologyName,
                Description = description,
                EntrySensorId = entrySensorId,
                DiverterNodes = diverterNodes,
                ExceptionChuteId = exceptionChuteId
            };

            // 重用更新逻辑进行验证和保存
            return UpdateChutePathTopology(request);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "导入的CSV文件数据格式无效");
            return BadRequest(ApiResponse<object>.BadRequest($"CSV文件数据格式无效: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从CSV导入格口路径拓扑配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("导入格口路径拓扑配置失败 - Failed to import chute path topology configuration from CSV"));
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        
        return result.ToArray();
    }

    private static List<long>? ParseChuteIds(string field, int lineNumber, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        var ids = field.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<long>();
        
        foreach (var idStr in ids)
        {
            if (!long.TryParse(idStr.Trim(), out var id))
            {
                throw new FormatException($"Line {lineNumber}: Invalid chute ID '{idStr}' in {fieldName}");
            }
            result.Add(id);
        }
        
        return result.Count > 0 ? result : null;
    }

    private ChutePathTopologyResponse MapToResponse(ChutePathTopologyConfig config)
    {
        return new ChutePathTopologyResponse
        {
            TopologyId = config.TopologyId,
            TopologyName = config.TopologyName,
            Description = config.Description,
            EntrySensorId = config.EntrySensorId,
            DiverterNodes = config.DiverterNodes.Select(n => new DiverterPathNodeRequest
            {
                DiverterId = n.DiverterId,
                DiverterName = n.DiverterName,
                PositionIndex = n.PositionIndex,
                SegmentId = n.SegmentId,
                FrontSensorId = n.FrontSensorId,
                LeftChuteIds = n.LeftChuteIds.ToList(),
                RightChuteIds = n.RightChuteIds.ToList(),
                Remarks = n.Remarks
            }).ToList(),
            ExceptionChuteId = config.ExceptionChuteId,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    private ChutePathTopologyConfig MapToConfig(ChutePathTopologyRequest request)
    {
        var now = _clock.LocalNow;
        
        return new ChutePathTopologyConfig
        {
            TopologyId = LiteDbChutePathTopologyRepository.DefaultTopologyId,
            TopologyName = request.TopologyName,
            Description = request.Description,
            EntrySensorId = request.EntrySensorId,
            DiverterNodes = MapToDiverterNodes(request.DiverterNodes),
            ExceptionChuteId = request.ExceptionChuteId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 将请求中的摆轮节点列表转换为配置模型的摆轮节点列表
    /// </summary>
    private static List<DiverterPathNode> MapToDiverterNodes(IEnumerable<DiverterPathNodeRequest> requestNodes)
    {
        return requestNodes.Select(n => new DiverterPathNode
        {
            DiverterId = n.DiverterId,
            DiverterName = n.DiverterName,
            PositionIndex = n.PositionIndex,
            SegmentId = n.SegmentId,
            FrontSensorId = n.FrontSensorId,
            LeftChuteIds = n.LeftChuteIds?.AsReadOnly() ?? EmptyChuteIds,
            RightChuteIds = n.RightChuteIds?.AsReadOnly() ?? EmptyChuteIds,
            Remarks = n.Remarks
        }).ToList();
    }

    /// <summary>
    /// 生成ASCII格式的拓扑图
    /// </summary>
    /// <param name="config">拓扑配置</param>
    /// <returns>ASCII拓扑图字符串</returns>
    private static string GenerateTopologyDiagram(ChutePathTopologyConfig config)
    {
        var sb = new System.Text.StringBuilder();
        var nodes = config.DiverterNodes.OrderBy(n => n.PositionIndex).ToList();
        
        if (nodes.Count == 0)
        {
            sb.AppendLine("拓扑配置为空，请先配置摆轮节点");
            sb.AppendLine("Topology configuration is empty, please configure diverter nodes first");
            return sb.ToString();
        }

        // 第一行：左侧格口（在摆轮上方）
        sb.Append("".PadLeft(8)); // 入口前的空格
        foreach (var node in nodes)
        {
            var leftChutes = node.LeftChuteIds.Count > 0 
                ? $"格口{string.Join(",", node.LeftChuteIds)}" 
                : "";
            sb.Append(leftChutes.PadLeft(DiagramColumnWidth));
        }
        sb.AppendLine();
        
        // 第二行：上箭头
        sb.Append("".PadLeft(8));
        foreach (var node in nodes)
        {
            var arrow = node.LeftChuteIds.Count > 0 ? "↑" : "";
            sb.Append(arrow.PadLeft(DiagramColumnWidth));
        }
        sb.AppendLine();
        
        // 第三行：主线（入口 → 摆轮1 → 摆轮2 → ... → 末端(异常口)）
        sb.Append($"入口 →");
        foreach (var node in nodes)
        {
            var diverterName = node.DiverterName ?? $"摆轮D{node.DiverterId}";
            // 截取名称使其适合宽度（减去箭头占用的空间）
            if (diverterName.Length > DiagramColumnWidth - DiagramArrowPadding)
            {
                diverterName = diverterName.Substring(0, DiagramColumnWidth - DiagramArrowPadding);
            }
            sb.Append($" {diverterName} →".PadLeft(DiagramColumnWidth));
        }
        sb.AppendLine($" 末端(异常口{config.ExceptionChuteId})");
        
        // 第四行：下箭头
        sb.Append("  ↓".PadLeft(8));
        foreach (var node in nodes)
        {
            var arrow = node.RightChuteIds.Count > 0 ? "↓" : "";
            sb.Append(arrow.PadLeft(DiagramColumnWidth));
        }
        sb.AppendLine();
        
        // 第五行：传感器和右侧格口
        sb.Append($"传感器{config.EntrySensorId}".PadLeft(8));
        foreach (var node in nodes)
        {
            var rightChutes = node.RightChuteIds.Count > 0 
                ? $"格口{string.Join(",", node.RightChuteIds)}" 
                : "";
            sb.Append(rightChutes.PadLeft(DiagramColumnWidth));
        }
        sb.AppendLine();
        
        // 添加摆轮前传感器信息
        sb.AppendLine();
        sb.AppendLine("摆轮前传感器配置:");
        foreach (var node in nodes)
        {
            var diverterName = node.DiverterName ?? $"摆轮D{node.DiverterId}";
            sb.AppendLine($"  {diverterName} → 传感器ID: {node.FrontSensorId}");
        }
        
        return sb.ToString();
    }
}
