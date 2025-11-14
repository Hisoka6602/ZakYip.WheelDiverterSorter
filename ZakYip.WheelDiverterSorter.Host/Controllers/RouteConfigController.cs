using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Host.Utilities;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 路由配置管理API控制器
/// </summary>
/// <remarks>
/// 提供格口路由配置的增删改查功能，支持热更新
/// </remarks>
[ApiController]
[Route("api/config/routes")]
[Produces("application/json")]
public class RouteConfigController : ControllerBase {
    private readonly IRouteConfigurationRepository _repository;
    private readonly ILogger<RouteConfigController> _logger;

    public RouteConfigController(
        IRouteConfigurationRepository repository,
        ILogger<RouteConfigController> logger) {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有启用的路由配置
    /// </summary>
    /// <returns>所有启用的路由配置列表</returns>
    /// <response code="200">成功返回配置列表</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RouteConfigResponse>), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IEnumerable<RouteConfigResponse>> GetAllRoutes() {
        try {
            var configs = _repository.GetAllEnabled();
            var responses = configs.Select(MapToResponse);
            return Ok(responses);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "获取路由配置列表失败");
            return StatusCode(500, new { message = "获取配置列表失败" });
        }
    }

    /// <summary>
    /// 根据格口ID获取路由配置
    /// </summary>
    /// <param name="chuteId">格口标识</param>
    /// <returns>指定格口的路由配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="404">配置不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("{chuteId}")]
    [ProducesResponseType(typeof(RouteConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<RouteConfigResponse> GetRoute(int chuteId) {
        try {
            if (chuteId <= 0) {
                return BadRequest(new { message = "格口ID必须大于0" });
            }

            var config = _repository.GetByChuteId(chuteId);
            if (config == null) {
                return NotFound(new { message = $"格口 {chuteId} 的配置不存在" });
            }

            return Ok(MapToResponse(config));
        }
        catch (Exception ex) {
            _logger.LogError(ex, "获取格口 {ChuteId} 的配置失败", chuteId);
            return StatusCode(500, new { message = "获取配置失败" });
        }
    }

    /// <summary>
    /// 创建新的路由配置
    /// </summary>
    /// <param name="request">路由配置请求</param>
    /// <returns>创建的路由配置</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="409">配置已存在</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     POST /api/config/routes
    ///     {
    ///         "chuteId": 1,
    ///         "diverterConfigurations": [
    ///             {
    ///                 "diverterId": 1,
    ///                 "targetDirection": 1,
    ///                 "sequenceNumber": 1
    ///             },
    ///             {
    ///                 "diverterId": 2,
    ///                 "targetDirection": 1,
    ///                 "sequenceNumber": 2
    ///             }
    ///         ],
    ///         "isEnabled": true
    ///     }
    ///
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(RouteConfigResponse), 201)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 409)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<RouteConfigResponse> CreateRoute([FromBody] RouteConfigRequest request) {
        try {
            if (request.ChuteId <= 0) {
                return BadRequest(new { message = "格口ID必须大于0" });
            }

            if (request.DiverterConfigurations == null || request.DiverterConfigurations.Count == 0) {
                return BadRequest(new { message = "摆轮配置不能为空" });
            }

            // 验证摆轮配置
            var validation = ValidateDiverterConfigurations(request.DiverterConfigurations);
            if (!validation.IsValid) {
                return BadRequest(new { message = validation.ErrorMessage });
            }

            // 检查是否已存在相同的格口ID
            var existing = _repository.GetByChuteId(request.ChuteId);
            if (existing != null) {
                return Conflict(new { message = $"格口 {request.ChuteId} 的配置已存在，请使用PUT方法更新" });
            }

            // 检查是否存在重复的路由配置（相同的摆轮方向组合）
            var duplicateRoute = CheckForDuplicateRoute(request.DiverterConfigurations);
            if (duplicateRoute != null) {
                return Conflict(new { message = $"路由配置重复：与格口 {duplicateRoute.ChuteId} 的配置完全相同" });
            }

            var config = MapToConfiguration(request);
            _repository.Upsert(config);

            _logger.LogInformation("创建格口 {ChuteId} 的路由配置成功", request.ChuteId);
            return CreatedAtAction(nameof(GetRoute), new { chuteId = request.ChuteId }, MapToResponse(config));
        }
        catch (Exception ex) {
            _logger.LogError(ex, "创建格口 {ChuteId} 的配置失败", request.ChuteId);
            return StatusCode(500, new { message = "创建配置失败" });
        }
    }

    /// <summary>
    /// 更新现有路由配置（支持热更新）
    /// </summary>
    /// <param name="chuteId">格口标识（URL中的参数优先于请求体）</param>
    /// <param name="request">路由配置请求</param>
    /// <returns>更新后的路由配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    ///
    ///     PUT /api/config/routes/1
    ///     {
    ///         "chuteId": 1,
    ///         "diverterConfigurations": [
    ///             {
    ///                 "diverterId": 1,
    ///                 "targetDirection": 2,
    ///                 "sequenceNumber": 1
    ///             }
    ///         ],
    ///         "isEnabled": true
    ///     }
    ///
    /// 配置更新后立即生效，无需重启服务
    /// </remarks>
    [HttpPut("{chuteId}")]
    [ProducesResponseType(typeof(RouteConfigResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<RouteConfigResponse> UpdateRoute(int chuteId, [FromBody] RouteConfigRequest request) {
        try {
            if (chuteId <= 0) {
                return BadRequest(new { message = "格口ID必须大于0" });
            }

            if (request.DiverterConfigurations == null || request.DiverterConfigurations.Count == 0) {
                return BadRequest(new { message = "摆轮配置不能为空" });
            }

            // 验证摆轮配置
            var validation = ValidateDiverterConfigurations(request.DiverterConfigurations);
            if (!validation.IsValid) {
                return BadRequest(new { message = validation.ErrorMessage });
            }

            // 检查是否存在重复的路由配置（相同的摆轮方向组合），排除当前格口
            var duplicateRoute = CheckForDuplicateRoute(request.DiverterConfigurations, chuteId);
            if (duplicateRoute != null) {
                return Conflict(new { message = $"路由配置重复：与格口 {duplicateRoute.ChuteId} 的配置完全相同" });
            }

            // URL中的chuteId优先级高于Body中的
            request.ChuteId = chuteId;

            var config = MapToConfiguration(request);
            _repository.Upsert(config);

            _logger.LogInformation("更新格口 {ChuteId} 的路由配置成功，配置已热更新", chuteId);
            return Ok(MapToResponse(config));
        }
        catch (Exception ex) {
            _logger.LogError(ex, "更新格口 {ChuteId} 的配置失败", chuteId);
            return StatusCode(500, new { message = "更新配置失败" });
        }
    }

    /// <summary>
    /// 删除路由配置
    /// </summary>
    /// <param name="chuteId">格口标识</param>
    /// <returns>无内容</returns>
    /// <response code="204">删除成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="404">配置不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpDelete("{chuteId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult DeleteRoute(int chuteId) {
        try {
            if (chuteId <= 0) {
                return BadRequest(new { message = "格口ID必须大于0" });
            }

            var success = _repository.Delete(chuteId);
            if (!success) {
                return NotFound(new { message = $"格口 {chuteId} 的配置不存在" });
            }

            _logger.LogInformation("删除格口 {ChuteId} 的路由配置成功", chuteId);
            return NoContent();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "删除格口 {ChuteId} 的配置失败", chuteId);
            return StatusCode(500, new { message = "删除配置失败" });
        }
    }

    /// <summary>
    /// 导出所有路由配置
    /// </summary>
    /// <returns>所有路由配置的JSON数据</returns>
    /// <response code="200">导出成功</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 导出所有路由配置为JSON格式，可用于备份或迁移
    /// </remarks>
    [HttpGet("export")]
    [ProducesResponseType(typeof(IEnumerable<RouteConfigResponse>), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<IEnumerable<RouteConfigResponse>> ExportRoutes() {
        try {
            var configs = _repository.GetAllEnabled();
            var responses = configs.Select(MapToResponse);
            _logger.LogInformation("导出路由配置成功，共 {Count} 条", configs.Count());
            return Ok(responses);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "导出路由配置失败");
            return StatusCode(500, new { message = "导出路由配置失败" });
        }
    }

    /// <summary>
    /// 导入路由配置
    /// </summary>
    /// <param name="routes">路由配置列表</param>
    /// <returns>导入结果</returns>
    /// <response code="200">导入成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 批量导入路由配置。如果配置已存在，将跳过该配置。
    /// 导入过程会验证每个配置的有效性。
    ///
    /// 示例请求:
    ///
    ///     POST /api/config/routes/import
    ///     [
    ///         {
    ///             "chuteId": 1,
    ///             "diverterConfigurations": [
    ///                 {
    ///                     "diverterId": 1,
    ///                     "targetDirection": 1,
    ///                     "sequenceNumber": 1
    ///                 }
    ///             ],
    ///             "isEnabled": true
    ///         }
    ///     ]
    ///
    /// </remarks>
    [HttpPost("import")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult ImportRoutes([FromBody] List<RouteConfigRequest> routes) {
        try {
            if (routes == null || routes.Count == 0) {
                return BadRequest(new { message = "导入的路由配置列表不能为空" });
            }

            var successCount = 0;
            var skipCount = 0;
            var errorCount = 0;
            var errors = new List<string>();

            foreach (var route in routes) {
                try {
                    // 验证配置
                    if (route.ChuteId <= 0) {
                        errors.Add($"格口ID必须大于0");
                        errorCount++;
                        continue;
                    }

                    if (route.DiverterConfigurations == null || route.DiverterConfigurations.Count == 0) {
                        errors.Add($"格口 {route.ChuteId}: 摆轮配置不能为空");
                        errorCount++;
                        continue;
                    }

                    var validation = ValidateDiverterConfigurations(route.DiverterConfigurations);
                    if (!validation.IsValid) {
                        errors.Add($"格口 {route.ChuteId}: {validation.ErrorMessage}");
                        errorCount++;
                        continue;
                    }

                    // 检查是否已存在
                    var existing = _repository.GetByChuteId(route.ChuteId);
                    if (existing != null) {
                        skipCount++;
                        continue;
                    }

                    // 检查是否存在重复的路由配置
                    var duplicateRoute = CheckForDuplicateRoute(route.DiverterConfigurations);
                    if (duplicateRoute != null) {
                        errors.Add($"格口 {route.ChuteId}: 路由配置重复，与格口 {duplicateRoute.ChuteId} 的配置完全相同");
                        errorCount++;
                        continue;
                    }

                    var config = MapToConfiguration(route);
                    _repository.Upsert(config);
                    successCount++;
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "导入格口 {ChuteId} 的配置失败", route.ChuteId);
                    errors.Add($"格口 {route.ChuteId}: {ex.Message}");
                    errorCount++;
                }
            }

            _logger.LogInformation(
                "导入路由配置完成：成功 {SuccessCount} 条，跳过 {SkipCount} 条，失败 {ErrorCount} 条",
                successCount, skipCount, errorCount);

            return Ok(new {
                message = "导入完成",
                successCount,
                skipCount,
                errorCount,
                errors = errorCount > 0 ? errors : null
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "导入路由配置失败");
            return StatusCode(500, new { message = "导入路由配置失败" });
        }
    }

    /// <summary>
    /// 验证摆轮配置
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateDiverterConfigurations(List<DiverterConfigRequest> configs) {
        // 检查是否有无效的DiverterId
        if (configs.Any(c => c.DiverterId <= 0)) {
            return (false, "摆轮ID必须大于0");
        }

        // 检查顺序号是否连续且从1开始
        var sortedSequences = configs.Select(c => c.SequenceNumber).OrderBy(s => s).ToList();
        if (sortedSequences.First() != 1) {
            return (false, "顺序号必须从1开始");
        }

        for (int i = 0; i < sortedSequences.Count - 1; i++) {
            if (sortedSequences[i + 1] - sortedSequences[i] != 1) {
                return (false, "顺序号必须连续");
            }
        }

        // 检查是否有重复的顺序号
        if (configs.Select(c => c.SequenceNumber).Distinct().Count() != configs.Count) {
            return (false, "顺序号不能重复");
        }

        return (true, null);
    }

    /// <summary>
    /// 检查是否存在重复的路由配置（相同的摆轮方向组合）
    /// </summary>
    /// <param name="diverterConfigs">要检查的摆轮配置列表</param>
    /// <param name="excludeChuteId">要排除的格口ID（用于更新时排除自身）</param>
    /// <returns>如果存在重复则返回重复的配置，否则返回null</returns>
    private ChuteRouteConfiguration? CheckForDuplicateRoute(List<DiverterConfigRequest> diverterConfigs, int? excludeChuteId = null) {
        var allConfigs = _repository.GetAllEnabled();

        // 创建当前配置的签名（按顺序的摆轮ID和方向组合）
        var currentSignature = string.Join("|", diverterConfigs
            .OrderBy(c => c.SequenceNumber)
            .Select(c => $"{c.DiverterId}:{c.TargetDirection}"));

        foreach (var existing in allConfigs) {
            // 排除指定的格口ID
            if (excludeChuteId.HasValue && existing.ChuteId == excludeChuteId.Value) {
                continue;
            }

            // 创建现有配置的签名
            var existingSignature = string.Join("|", existing.DiverterConfigurations
                .OrderBy(c => c.SequenceNumber)
                .Select(c => $"{c.DiverterId}:{c.TargetDirection}"));

            if (currentSignature == existingSignature) {
                return existing;
            }
        }

        return null;
    }

    /// <summary>
    /// 将请求模型映射到配置模型
    /// </summary>
    private ChuteRouteConfiguration MapToConfiguration(RouteConfigRequest request) {
        return new ChuteRouteConfiguration {
            ChuteId = request.ChuteId,
            ChuteName = request.ChuteName,
            DiverterConfigurations = request.DiverterConfigurations
                .Select(d => new DiverterConfigurationEntry {
                    DiverterId = d.DiverterId,
                    TargetDirection = d.TargetDirection,
                    SequenceNumber = d.SequenceNumber,
                    SegmentLengthMm = d.SegmentLengthMm,
                    SegmentSpeedMmPerSecond = d.SegmentSpeedMmPerSecond,
                    SegmentToleranceTimeMs = d.SegmentToleranceTimeMs
                })
                .ToList(),
            BeltSpeedMmPerSecond = request.BeltSpeedMmPerSecond,
            BeltLengthMm = request.BeltLengthMm,
            ToleranceTimeMs = request.ToleranceTimeMs,
            SensorConfig = request.SensorConfig != null ? new ChuteSensorConfig {
                SensorId = request.SensorConfig.SensorId,
                SensorType = request.SensorConfig.SensorType,
                InputBit = request.SensorConfig.InputBit,
                IsEnabled = request.SensorConfig.IsEnabled,
                DebounceTimeMs = request.SensorConfig.DebounceTimeMs
            } : null,
            IsEnabled = request.IsEnabled
        };
    }

    /// <summary>
    /// 将配置模型映射到响应模型
    /// </summary>
    private RouteConfigResponse MapToResponse(ChuteRouteConfiguration config) {
        return new RouteConfigResponse {
            Id = config.Id,
            ChuteId = config.ChuteId,
            ChuteName = config.ChuteName,
            DiverterConfigurations = config.DiverterConfigurations
                .Select(d => new DiverterConfigRequest {
                    DiverterId = d.DiverterId,
                    TargetDirection = d.TargetDirection,
                    SequenceNumber = d.SequenceNumber,
                    SegmentLengthMm = d.SegmentLengthMm,
                    SegmentSpeedMmPerSecond = d.SegmentSpeedMmPerSecond,
                    SegmentToleranceTimeMs = d.SegmentToleranceTimeMs
                })
                .ToList(),
            BeltSpeedMmPerSecond = config.BeltSpeedMmPerSecond,
            BeltLengthMm = config.BeltLengthMm,
            ToleranceTimeMs = config.ToleranceTimeMs,
            SensorConfig = config.SensorConfig != null ? new ChuteSensorConfigRequest {
                SensorId = config.SensorConfig.SensorId,
                SensorType = config.SensorConfig.SensorType,
                InputBit = config.SensorConfig.InputBit,
                IsEnabled = config.SensorConfig.IsEnabled,
                DebounceTimeMs = config.SensorConfig.DebounceTimeMs
            } : null,
            IsEnabled = config.IsEnabled,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}