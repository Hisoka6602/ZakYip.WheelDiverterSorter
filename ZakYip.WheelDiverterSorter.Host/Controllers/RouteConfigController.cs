using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 路由配置管理API控制器
/// </summary>
[ApiController]
[Route("api/config/routes")]
public class RouteConfigController : ControllerBase
{
    private readonly IRouteConfigurationRepository _repository;
    private readonly ILogger<RouteConfigController> _logger;

    public RouteConfigController(
        IRouteConfigurationRepository repository,
        ILogger<RouteConfigController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有启用的路由配置
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<RouteConfigResponse>> GetAllRoutes()
    {
        try
        {
            var configs = _repository.GetAllEnabled();
            var responses = configs.Select(MapToResponse);
            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取路由配置列表失败");
            return StatusCode(500, new { message = "获取配置列表失败" });
        }
    }

    /// <summary>
    /// 根据格口ID获取路由配置
    /// </summary>
    [HttpGet("{chuteId}")]
    public ActionResult<RouteConfigResponse> GetRoute(string chuteId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(chuteId))
            {
                return BadRequest(new { message = "格口ID不能为空" });
            }

            var config = _repository.GetByChuteId(chuteId);
            if (config == null)
            {
                return NotFound(new { message = $"格口 {chuteId} 的配置不存在" });
            }

            return Ok(MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取格口 {ChuteId} 的配置失败", chuteId);
            return StatusCode(500, new { message = "获取配置失败" });
        }
    }

    /// <summary>
    /// 创建新的路由配置
    /// </summary>
    [HttpPost]
    public ActionResult<RouteConfigResponse> CreateRoute([FromBody] RouteConfigRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ChuteId))
            {
                return BadRequest(new { message = "格口ID不能为空" });
            }

            if (request.DiverterConfigurations == null || request.DiverterConfigurations.Count == 0)
            {
                return BadRequest(new { message = "摆轮配置不能为空" });
            }

            // 验证摆轮配置
            var validation = ValidateDiverterConfigurations(request.DiverterConfigurations);
            if (!validation.IsValid)
            {
                return BadRequest(new { message = validation.ErrorMessage });
            }

            // 检查是否已存在
            var existing = _repository.GetByChuteId(request.ChuteId);
            if (existing != null)
            {
                return Conflict(new { message = $"格口 {request.ChuteId} 的配置已存在，请使用PUT方法更新" });
            }

            var config = MapToConfiguration(request);
            _repository.Upsert(config);

            _logger.LogInformation("创建格口 {ChuteId} 的路由配置成功", request.ChuteId);
            return CreatedAtAction(nameof(GetRoute), new { chuteId = request.ChuteId }, MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建格口 {ChuteId} 的配置失败", request.ChuteId);
            return StatusCode(500, new { message = "创建配置失败" });
        }
    }

    /// <summary>
    /// 更新现有路由配置（支持热更新）
    /// </summary>
    [HttpPut("{chuteId}")]
    public ActionResult<RouteConfigResponse> UpdateRoute(string chuteId, [FromBody] RouteConfigRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(chuteId))
            {
                return BadRequest(new { message = "格口ID不能为空" });
            }

            if (request.DiverterConfigurations == null || request.DiverterConfigurations.Count == 0)
            {
                return BadRequest(new { message = "摆轮配置不能为空" });
            }

            // 验证摆轮配置
            var validation = ValidateDiverterConfigurations(request.DiverterConfigurations);
            if (!validation.IsValid)
            {
                return BadRequest(new { message = validation.ErrorMessage });
            }

            // URL中的chuteId优先级高于Body中的
            request.ChuteId = chuteId;

            var config = MapToConfiguration(request);
            _repository.Upsert(config);

            _logger.LogInformation("更新格口 {ChuteId} 的路由配置成功，配置已热更新", chuteId);
            return Ok(MapToResponse(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新格口 {ChuteId} 的配置失败", chuteId);
            return StatusCode(500, new { message = "更新配置失败" });
        }
    }

    /// <summary>
    /// 删除路由配置
    /// </summary>
    [HttpDelete("{chuteId}")]
    public ActionResult DeleteRoute(string chuteId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(chuteId))
            {
                return BadRequest(new { message = "格口ID不能为空" });
            }

            var success = _repository.Delete(chuteId);
            if (!success)
            {
                return NotFound(new { message = $"格口 {chuteId} 的配置不存在" });
            }

            _logger.LogInformation("删除格口 {ChuteId} 的路由配置成功", chuteId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除格口 {ChuteId} 的配置失败", chuteId);
            return StatusCode(500, new { message = "删除配置失败" });
        }
    }

    /// <summary>
    /// 验证摆轮配置
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateDiverterConfigurations(List<DiverterConfigRequest> configs)
    {
        // 检查是否有空的DiverterId
        if (configs.Any(c => string.IsNullOrWhiteSpace(c.DiverterId)))
        {
            return (false, "摆轮ID不能为空");
        }

        // 检查顺序号是否连续且从1开始
        var sortedSequences = configs.Select(c => c.SequenceNumber).OrderBy(s => s).ToList();
        if (sortedSequences.First() != 1)
        {
            return (false, "顺序号必须从1开始");
        }

        for (int i = 0; i < sortedSequences.Count - 1; i++)
        {
            if (sortedSequences[i + 1] - sortedSequences[i] != 1)
            {
                return (false, "顺序号必须连续");
            }
        }

        // 检查是否有重复的顺序号
        if (configs.Select(c => c.SequenceNumber).Distinct().Count() != configs.Count)
        {
            return (false, "顺序号不能重复");
        }

        return (true, null);
    }

    /// <summary>
    /// 将请求模型映射到配置模型
    /// </summary>
    private ChuteRouteConfiguration MapToConfiguration(RouteConfigRequest request)
    {
        return new ChuteRouteConfiguration
        {
            ChuteId = request.ChuteId,
            DiverterConfigurations = request.DiverterConfigurations
                .Select(d => new DiverterConfigurationEntry
                {
                    DiverterId = d.DiverterId,
                    TargetAngle = d.TargetAngle,
                    SequenceNumber = d.SequenceNumber
                })
                .ToList(),
            IsEnabled = request.IsEnabled
        };
    }

    /// <summary>
    /// 将配置模型映射到响应模型
    /// </summary>
    private RouteConfigResponse MapToResponse(ChuteRouteConfiguration config)
    {
        return new RouteConfigResponse
        {
            Id = config.Id,
            ChuteId = config.ChuteId,
            DiverterConfigurations = config.DiverterConfigurations
                .Select(d => new DiverterConfigRequest
                {
                    DiverterId = d.DiverterId,
                    TargetAngle = d.TargetAngle,
                    SequenceNumber = d.SequenceNumber
                })
                .ToList(),
            IsEnabled = config.IsEnabled,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}
