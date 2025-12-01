using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 格口分配超时配置API控制器
/// </summary>
[ApiController]
[Route("api/config/chute-assignment-timeout")]
[Produces("application/json")]
public class ChuteAssignmentTimeoutController : ApiControllerBase
{
    // TODO: 当前假设只有一条线，未来支持多线时需要动态获取LineId
    private const long DefaultLineId = 1;
    
    private readonly ISystemConfigurationRepository _configRepository;
    private readonly IChuteAssignmentTimeoutCalculator? _timeoutCalculator;
    private readonly ILogger<ChuteAssignmentTimeoutController> _logger;

    public ChuteAssignmentTimeoutController(
        ISystemConfigurationRepository configRepository,
        ILogger<ChuteAssignmentTimeoutController> logger,
        IChuteAssignmentTimeoutCalculator? timeoutCalculator = null)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutCalculator = timeoutCalculator;
    }

    /// <summary>
    /// 获取格口分配超时配置
    /// </summary>
    /// <returns>当前超时配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前格口分配超时的配置参数，包括安全系数和计算出的有效超时时间。
    /// 
    /// **重要**: 当格口分配超时时，不会进行重试或降级，包裹将直接路由到异常格口。
    /// </remarks>
    [HttpGet]
    [SwaggerOperation(
        Summary = "获取格口分配超时配置",
        Description = "返回当前格口分配超时的配置参数。超时/失败时包裹直接路由到异常格口，不进行重试或降级。",
        OperationId = "GetChuteAssignmentTimeout",
        Tags = new[] { "格口分配超时配置" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<ChuteAssignmentTimeoutResponse>))]
    [SwaggerResponse(500, "服务器内部错误")]
    public ActionResult<ApiResponse<ChuteAssignmentTimeoutResponse>> GetConfiguration()
    {
        try
        {
            var systemConfig = _configRepository.Get();
            var options = systemConfig.ChuteAssignmentTimeout ?? new ChuteAssignmentTimeoutOptions();

            // 尝试计算当前的理论物理极限时间和有效超时时间
            decimal? theoreticalLimit = null;
            decimal effectiveTimeout = options.FallbackTimeoutSeconds;

            if (_timeoutCalculator != null)
            {
                try
                {
                    var context = new ChuteAssignmentTimeoutContext(
                        LineId: DefaultLineId,
                        SafetyFactor: options.SafetyFactor
                    );
                    effectiveTimeout = _timeoutCalculator.CalculateTimeoutSeconds(context);
                    
                    // 尝试计算理论物理极限（SafetyFactor=1.0）
                    var maxContext = new ChuteAssignmentTimeoutContext(
                        LineId: DefaultLineId,
                        SafetyFactor: 1.0m
                    );
                    theoreticalLimit = _timeoutCalculator.CalculateTimeoutSeconds(maxContext);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "计算理论物理极限时间失败，使用降级值");
                }
            }

            var response = new ChuteAssignmentTimeoutResponse
            {
                SafetyFactor = options.SafetyFactor,
                FallbackTimeoutSeconds = options.FallbackTimeoutSeconds,
                TheoreticalLimitSeconds = theoreticalLimit,
                EffectiveTimeoutSeconds = effectiveTimeout
            };

            return Success(response, "获取格口分配超时配置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取格口分配超时配置失败");
            return ServerError<ChuteAssignmentTimeoutResponse>("获取配置失败");
        }
    }

    /// <summary>
    /// 更新格口分配超时配置
    /// </summary>
    /// <param name="request">超时配置请求</param>
    /// <returns>更新后的配置</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 更新格口分配超时的配置参数。
    /// 超时时间由线体拓扑（入口到第一个摆轮的距离和速度）× 安全系数计算。
    /// 安全系数必须在0.1到1.0之间，不允许超过1.0（物理极限）。
    /// 
    /// **重要**: 当格口分配超时时，不会进行重试或降级，包裹将直接路由到异常格口。
    /// 此超时配置用于确定等待上游分配指令的最大时间。
    /// 
    /// 配置会立即生效无需重启。
    /// </remarks>
    [HttpPut]
    [SwaggerOperation(
        Summary = "更新格口分配超时配置",
        Description = "更新格口分配超时的配置参数。超时/失败时包裹直接路由到异常格口，不进行重试或降级。",
        OperationId = "UpdateChuteAssignmentTimeout",
        Tags = new[] { "格口分配超时配置" }
    )]
    [SwaggerResponse(200, "更新成功", typeof(ApiResponse<ChuteAssignmentTimeoutResponse>))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    public ActionResult<ApiResponse<ChuteAssignmentTimeoutResponse>> UpdateConfiguration(
        [FromBody] ChuteAssignmentTimeoutRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                
                return ValidationError<ChuteAssignmentTimeoutResponse>(
                    string.Join("; ", errors));
            }

            // 获取当前配置
            var systemConfig = _configRepository.Get();
            
            // 更新超时配置
            systemConfig.ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions
            {
                SafetyFactor = request.SafetyFactor,
                FallbackTimeoutSeconds = request.FallbackTimeoutSeconds
            };

            // 验证配置
            var validation = systemConfig.ChuteAssignmentTimeout.Validate();
            if (!validation.IsValid)
            {
                return ValidationError<ChuteAssignmentTimeoutResponse>(
                    validation.ErrorMessage ?? "配置验证失败");
            }

            // 保存配置
            _configRepository.Update(systemConfig);

            _logger.LogInformation(
                "格口分配超时配置已更新: SafetyFactor={SafetyFactor}, FallbackTimeout={FallbackTimeout}秒",
                request.SafetyFactor,
                request.FallbackTimeoutSeconds);

            // 返回更新后的配置
            return GetConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新格口分配超时配置失败");
            return ServerError<ChuteAssignmentTimeoutResponse>("更新配置失败");
        }
    }
}
