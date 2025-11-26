using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
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
/// <item>线体段配置 - 引用 LineSegmentConfig 中的 SegmentId</item>
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
    private readonly IChutePathTopologyRepository _topologyRepository;
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly ILineTopologyRepository _lineTopologyRepository;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChutePathTopologyController> _logger;

    /// <summary>
    /// 初始化格口路径拓扑配置控制器
    /// </summary>
    public ChutePathTopologyController(
        IChutePathTopologyRepository topologyRepository,
        ISensorConfigurationRepository sensorRepository,
        ILineTopologyRepository lineTopologyRepository,
        ISystemClock clock,
        ILogger<ChutePathTopologyController> logger)
    {
        _topologyRepository = topologyRepository;
        _sensorRepository = sensorRepository;
        _lineTopologyRepository = lineTopologyRepository;
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
            var config = _topologyRepository.Get();
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
    ///         "exceptionChuteId": 999,
    ///         "defaultLineSpeedMmps": 500
    ///     }
    ///
    /// 配置说明：
    /// - entrySensorId 必须引用一个已配置的 ParcelCreation 类型的感应IO
    /// - diverterNodes 中的 segmentId 必须引用已配置的线体段
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

            // 验证摆轮节点不能为空
            if (request.DiverterNodes.Count == 0)
            {
                return BadRequest(ApiResponse<object>.BadRequest("至少需要配置一个摆轮节点 - At least one diverter node is required"));
            }

            // 获取已配置的感应IO列表用于验证
            var sensorConfig = _sensorRepository.Get();
            var configuredSensorIds = sensorConfig.Sensors?.Select(s => s.SensorId).ToHashSet() ?? new HashSet<long>();

            // 验证入口传感器ID
            if (!configuredSensorIds.Contains(request.EntrySensorId))
            {
                return BadRequest(ApiResponse<object>.BadRequest(
                    $"入口传感器ID ({request.EntrySensorId}) 未配置，请先在感应IO配置中添加"));
            }

            // 验证入口传感器类型必须是 ParcelCreation
            var entrySensor = sensorConfig.Sensors?.FirstOrDefault(s => s.SensorId == request.EntrySensorId);
            if (entrySensor != null && entrySensor.IoType != SensorIoType.ParcelCreation)
            {
                return BadRequest(ApiResponse<object>.BadRequest(
                    $"入口传感器ID ({request.EntrySensorId}) 类型必须是 ParcelCreation，当前类型为 {entrySensor.IoType}"));
            }

            // 获取已配置的线体段列表用于验证
            var lineTopologyConfig = _lineTopologyRepository.Get();
            var configuredSegmentIds = lineTopologyConfig.LineSegments?.Select(s => s.SegmentId).ToHashSet() ?? new HashSet<long>();

            // 验证每个摆轮节点
            foreach (var node in request.DiverterNodes)
            {
                // 验证线体段ID
                if (!configuredSegmentIds.Contains(node.SegmentId))
                {
                    return BadRequest(ApiResponse<object>.BadRequest(
                        $"摆轮节点 {node.DiverterId} 的线体段ID ({node.SegmentId}) 未配置，请先在线体拓扑配置中添加线体段"));
                }

                // 验证摆轮前感应IO（可选）
                if (node.FrontSensorId.HasValue && node.FrontSensorId.Value > 0)
                {
                    if (!configuredSensorIds.Contains(node.FrontSensorId.Value))
                    {
                        return BadRequest(ApiResponse<object>.BadRequest(
                            $"摆轮节点 {node.DiverterId} 的摆轮前感应IO ({node.FrontSensorId}) 未配置，请先在感应IO配置中添加"));
                    }

                    // 验证类型必须是 WheelFront
                    var frontSensor = sensorConfig.Sensors?.FirstOrDefault(s => s.SensorId == node.FrontSensorId.Value);
                    if (frontSensor != null && frontSensor.IoType != SensorIoType.WheelFront)
                    {
                        return BadRequest(ApiResponse<object>.BadRequest(
                            $"摆轮节点 {node.DiverterId} 的摆轮前感应IO ({node.FrontSensorId}) 类型必须是 WheelFront，当前类型为 {frontSensor.IoType}"));
                    }
                }

                // 验证至少有一侧有格口
                var leftCount = node.LeftChuteIds?.Count ?? 0;
                var rightCount = node.RightChuteIds?.Count ?? 0;
                if (leftCount == 0 && rightCount == 0)
                {
                    return BadRequest(ApiResponse<object>.BadRequest(
                        $"摆轮节点 {node.DiverterId} 必须至少配置一侧格口"));
                }
            }

            // 验证摆轮节点的位置索引不能重复
            var duplicatePositions = request.DiverterNodes
                .GroupBy(n => n.PositionIndex)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatePositions.Any())
            {
                return BadRequest(ApiResponse<object>.BadRequest(
                    $"摆轮节点位置索引重复: {string.Join(", ", duplicatePositions)}"));
            }

            var config = MapToConfig(request);
            _topologyRepository.Update(config);

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
            DefaultLineSpeedMmps = config.DefaultLineSpeedMmps,
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
            DiverterNodes = request.DiverterNodes.Select(n => new DiverterPathNode
            {
                DiverterId = n.DiverterId,
                DiverterName = n.DiverterName,
                PositionIndex = n.PositionIndex,
                SegmentId = n.SegmentId,
                FrontSensorId = n.FrontSensorId,
                LeftChuteIds = n.LeftChuteIds?.AsReadOnly() ?? Array.Empty<long>().ToList().AsReadOnly(),
                RightChuteIds = n.RightChuteIds?.AsReadOnly() ?? Array.Empty<long>().ToList().AsReadOnly(),
                Remarks = n.Remarks
            }).ToList(),
            ExceptionChuteId = request.ExceptionChuteId,
            DefaultLineSpeedMmps = request.DefaultLineSpeedMmps,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
