using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
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
/// </remarks>
[ApiController]
[Route("api/config/line-topology")]
[Produces("application/json")]
public class LineTopologyController : ControllerBase
{
    private readonly ILineTopologyRepository _topologyRepository;
    private readonly IWheelBindingsRepository _wheelBindingsRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<LineTopologyController> _logger;

    public LineTopologyController(
        ILineTopologyRepository topologyRepository,
        IWheelBindingsRepository wheelBindingsRepository,
        ISystemClock clock,
        ILogger<LineTopologyController> logger)
    {
        _topologyRepository = topologyRepository;
        _wheelBindingsRepository = wheelBindingsRepository;
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
    ///                 "dropOffsetMm": 500.0,
    ///                 "isEnabled": true
    ///             }
    ///         ],
    ///         "lineSegments": [
    ///             {
    ///                 "segmentId": "ENTRY-TO-WHEEL1",
    ///                 "fromNodeId": "ENTRY",
    ///                 "toNodeId": "WHEEL-1",
    ///                 "lengthMm": 5000.0,
    ///                 "nominalSpeedMmPerSec": 1000.0,
    ///                 "description": "入口到第一个摆轮"
    ///             }
    ///         ],
    ///         "entrySensorId": "SENSOR-ENTRY",
    ///         "exitSensorId": "SENSOR-EXIT",
    ///         "defaultLineSpeedMmps": 1000.0
    ///     }
    ///
    /// 配置说明：
    /// - 线体段（LineSegments）描述节点之间的物理连接，包括长度和速度
    /// - 摆轮节点（WheelNodes）按物理位置顺序排列，positionIndex从1开始
    /// - 格口（Chutes）必须绑定到某个摆轮节点的某个方向
    /// - 落格偏移（DropOffsetMm）用于精确计算包裹到达格口的时间
    /// - 系统会根据这些配置计算理论到达时间和超时阈值
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新线体拓扑配置",
        Description = "更新完整的线体拓扑配置，配置立即生效",
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

            // 验证线体段的节点必须存在
            if (request.LineSegments != null)
            {
                var allNodeIds = nodeIds.ToHashSet();
                allNodeIds.Add(LineTopologyConfig.EntryNodeId); // 使用常量
                
                foreach (var chute in request.Chutes)
                {
                    allNodeIds.Add(chute.ChuteId); // 添加格口作为可能的目标节点
                }

                foreach (var segment in request.LineSegments)
                {
                    if (!allNodeIds.Contains(segment.FromNodeId))
                    {
                        return BadRequest(ApiResponse<object>.BadRequest($"线体段 {segment.SegmentId} 的起始节点 {segment.FromNodeId} 不存在"));
                    }
                    if (!allNodeIds.Contains(segment.ToNodeId))
                    {
                        return BadRequest(ApiResponse<object>.BadRequest($"线体段 {segment.SegmentId} 的目标节点 {segment.ToNodeId} 不存在"));
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
    /// 获取摆轮硬件绑定配置
    /// </summary>
    /// <returns>摆轮硬件绑定配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("~/api/config/wheel-bindings")]
    [SwaggerOperation(
        Summary = "获取摆轮硬件绑定配置",
        Description = "返回摆轮逻辑ID与物理驱动器的绑定关系配置",
        OperationId = "GetWheelBindings",
        Tags = new[] { "摆轮硬件绑定" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<WheelBindingsResponse>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<WheelBindingsResponse>> GetWheelBindings()
    {
        try
        {
            var config = _wheelBindingsRepository.Get();
            var response = new WheelBindingsResponse
            {
                Bindings = config.Bindings.Select(b => new WheelHardwareBindingRequest
                {
                    WheelNodeId = b.WheelNodeId,
                    WheelName = b.WheelName,
                    DriverId = b.DriverId,
                    DriverName = b.DriverName,
                    // IO驱动配置
                    IoDriverType = b.IoDriverType,
                    IoAddress = b.IoAddress,
                    IoChannel = b.IoChannel,
                    OutputStartBit = b.OutputStartBit,
                    FeedbackInputBit = b.FeedbackInputBit,
                    // 摆轮驱动配置
                    WheelDriverType = b.WheelDriverType,
                    WheelDriverHost = b.WheelDriverHost,
                    WheelDriverPort = b.WheelDriverPort,
                    WheelDeviceAddress = b.WheelDeviceAddress,
                    IsEnabled = b.IsEnabled,
                    Remarks = b.Remarks
                }).ToList(),
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };
            return Ok(ApiResponse<WheelBindingsResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取摆轮硬件绑定配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取摆轮硬件绑定配置失败 - Failed to get wheel bindings configuration"));
        }
    }

    /// <summary>
    /// 更新摆轮硬件绑定配置
    /// </summary>
    /// <param name="request">摆轮硬件绑定配置请求</param>
    /// <returns>更新后的摆轮硬件绑定配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **摆轮硬件绑定说明**：
    /// 每个摆轮节点需要配置两部分硬件绑定：
    /// 
    /// 1. **IO驱动配置**（用于传感器信号和继电器控制）：
    ///    - ioDriverType: IO驱动厂商类型（Leadshine/Siemens/Mitsubishi/Omron）
    ///    - ioAddress: IO板地址（例如：192.168.1.100 或雷赛卡号）
    ///    - ioChannel: IO通道号
    ///    - outputStartBit: 输出起始位（用于继电器控制）
    ///    - feedbackInputBit: 反馈输入位（用于传感器信号读取）
    /// 
    /// 2. **摆轮驱动配置**（用于摆轮方向控制）：
    ///    - wheelDriverType: 摆轮驱动厂商类型（ShuDiNiao/Modi）
    ///    - wheelDriverHost: 摆轮驱动TCP主机地址
    ///    - wheelDriverPort: 摆轮驱动TCP端口
    ///    - wheelDeviceAddress: 摆轮设备地址
    /// 
    /// 示例请求:
    ///
    ///     PUT /api/config/wheel-bindings
    ///     {
    ///         "bindings": [
    ///             {
    ///                 "wheelNodeId": "WHEEL-1",
    ///                 "wheelName": "第一摆轮",
    ///                 "driverId": 1,
    ///                 "driverName": "1号摆轮",
    ///                 "ioDriverType": "Leadshine",
    ///                 "ioAddress": "0",
    ///                 "ioChannel": 1,
    ///                 "outputStartBit": 0,
    ///                 "feedbackInputBit": 10,
    ///                 "wheelDriverType": "ShuDiNiao",
    ///                 "wheelDriverHost": "192.168.0.100",
    ///                 "wheelDriverPort": 2000,
    ///                 "wheelDeviceAddress": 81,
    ///                 "isEnabled": true,
    ///                 "remarks": "入口第一个摆轮"
    ///             }
    ///         ]
    ///     }
    ///
    /// 配置说明：
    /// - wheelNodeId 必须与线体拓扑配置中的摆轮节点ID一致
    /// - IO驱动和摆轮驱动共同组成完整的摆轮控制拓扑
    /// </remarks>
    [HttpPut("~/api/config/wheel-bindings")]
    [SwaggerOperation(
        Summary = "更新摆轮硬件绑定配置",
        Description = "更新摆轮逻辑ID与物理驱动器的绑定关系（包含IO驱动和摆轮驱动两部分配置），配置立即生效",
        OperationId = "UpdateWheelBindings",
        Tags = new[] { "摆轮硬件绑定" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<WheelBindingsResponse>))]
    [SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<object>))]
    [SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<object>))]
    public ActionResult<ApiResponse<WheelBindingsResponse>> UpdateWheelBindings([FromBody] WheelBindingsRequest request)
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

            // 验证绑定列表不能为空
            if (request.Bindings.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("绑定列表不能为空 - Bindings list cannot be empty"));
            }

            // 验证WheelNodeId不能重复
            var wheelNodeIds = request.Bindings.Select(b => b.WheelNodeId).ToList();
            var duplicates = wheelNodeIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Any())
            {
                return BadRequest(ApiResponse<object>.BadRequest($"摆轮节点ID重复: {string.Join(", ", duplicates)}"));
            }

            // 验证DriverId不能重复
            var driverIds = request.Bindings.Select(b => b.DriverId).ToList();
            var duplicateDrivers = driverIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateDrivers.Any())
            {
                return BadRequest(ApiResponse<object>.BadRequest($"驱动器ID重复: {string.Join(", ", duplicateDrivers)}"));
            }

            var now = _clock.LocalNow;
            var config = new WheelBindingsConfig
            {
                Bindings = request.Bindings.Select(b => new WheelHardwareBinding
                {
                    WheelNodeId = b.WheelNodeId,
                    WheelName = b.WheelName,
                    DriverId = b.DriverId,
                    DriverName = b.DriverName,
                    // IO驱动配置
                    IoDriverType = b.IoDriverType,
                    IoAddress = b.IoAddress,
                    IoChannel = b.IoChannel,
                    OutputStartBit = b.OutputStartBit,
                    FeedbackInputBit = b.FeedbackInputBit,
                    // 摆轮驱动配置
                    WheelDriverType = b.WheelDriverType,
                    WheelDriverHost = b.WheelDriverHost,
                    WheelDriverPort = b.WheelDriverPort,
                    WheelDeviceAddress = b.WheelDeviceAddress,
                    IsEnabled = b.IsEnabled,
                    Remarks = b.Remarks
                }).ToList(),
                UpdatedAt = now
            };

            _wheelBindingsRepository.Update(config);

            _logger.LogInformation(
                "摆轮硬件绑定配置已更新: BindingCount={BindingCount}",
                config.Bindings.Count);

            var updatedConfig = _wheelBindingsRepository.Get();
            var response = new WheelBindingsResponse
            {
                Bindings = updatedConfig.Bindings.Select(b => new WheelHardwareBindingRequest
                {
                    WheelNodeId = b.WheelNodeId,
                    WheelName = b.WheelName,
                    DriverId = b.DriverId,
                    DriverName = b.DriverName,
                    // IO驱动配置
                    IoDriverType = b.IoDriverType,
                    IoAddress = b.IoAddress,
                    IoChannel = b.IoChannel,
                    OutputStartBit = b.OutputStartBit,
                    FeedbackInputBit = b.FeedbackInputBit,
                    // 摆轮驱动配置
                    WheelDriverType = b.WheelDriverType,
                    WheelDriverHost = b.WheelDriverHost,
                    WheelDriverPort = b.WheelDriverPort,
                    WheelDeviceAddress = b.WheelDeviceAddress,
                    IsEnabled = b.IsEnabled,
                    Remarks = b.Remarks
                }).ToList(),
                CreatedAt = updatedConfig.CreatedAt,
                UpdatedAt = updatedConfig.UpdatedAt
            };

            return Ok(ApiResponse<WheelBindingsResponse>.Ok(response, "摆轮硬件绑定配置已更新 - Wheel bindings configuration updated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新摆轮硬件绑定配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("更新摆轮硬件绑定配置失败 - Failed to update wheel bindings configuration"));
        }
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
                FromNodeId = s.FromNodeId,
                ToNodeId = s.ToNodeId,
                LengthMm = s.LengthMm,
                NominalSpeedMmPerSec = s.NominalSpeedMmPerSec,
                Description = s.Description
            }).ToList(),
            EntrySensorId = config.EntrySensorId,
            ExitSensorId = config.ExitSensorId,
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
            TopologyId = LiteDbLineTopologyRepository.DefaultTopologyId, // 使用常量
            TopologyName = request.TopologyName,
            Description = request.Description,
            WheelNodes = request.WheelNodes.Select(n => new WheelNodeConfig
            {
                NodeId = n.NodeId,
                NodeName = n.NodeName,
                PositionIndex = n.PositionIndex,
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
                FromNodeId = s.FromNodeId,
                ToNodeId = s.ToNodeId,
                LengthMm = s.LengthMm,
                NominalSpeedMmPerSec = s.NominalSpeedMmPerSec,
                Description = s.Description
            }).ToList() ?? new List<LineSegmentConfig>(),
            EntrySensorId = request.EntrySensorId,
            ExitSensorId = request.ExitSensorId,
            DefaultLineSpeedMmps = request.DefaultLineSpeedMmps,
            CreatedAt = now, // Will be overridden by repository if updating
            UpdatedAt = now
        };
    }
}
