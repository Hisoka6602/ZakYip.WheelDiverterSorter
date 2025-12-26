using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Health;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
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
    private readonly IConveyorSegmentService _segmentService;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChutePathTopologyController> _logger;

    /// <summary>
    /// 初始化格口路径拓扑配置控制器
    /// </summary>
    public ChutePathTopologyController(
        IChutePathTopologyService topologyService,
        IConveyorSegmentService segmentService,
        ISystemClock clock,
        ILogger<ChutePathTopologyController> logger)
    {
        _topologyService = topologyService;
        _segmentService = segmentService;
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
            _logger.LogError(ex, "获取格口路径拓扑配置失败: {ErrorMessage}", ex.Message);
            return StatusCode(500, ApiResponse<object>.ServerError($"获取格口路径拓扑配置失败: {ex.Message}"));
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
