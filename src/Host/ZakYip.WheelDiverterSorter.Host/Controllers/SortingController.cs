using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Debug;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Host.Models;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Execution.Queues;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// 统一分拣管理 API 控制器
/// </summary>
/// <remarks>
/// **功能概述**：
/// 
/// 本控制器整合所有分拣相关功能的统一入口，包括：
/// - 分拣改口（动态更改包裹目标格口）
/// - 分拣测试（手动触发分拣用于测试）
/// - Position间隔查询（支持包裹丢失检测的方案A+）
/// - 落格回调配置（配置分拣完成通知的触发模式）
/// 
/// **落格回调模式**：
/// - OnWheelExecution：执行摆轮动作时立即发送分拣完成通知
/// - OnSensorTrigger：等待落格传感器感应后发送分拣完成通知（默认）
/// 
/// **Position间隔查询**：
/// 支持方案A+（中位数自适应超时检测）的实施，提供各positionIndex的实际触发间隔中位数。
/// </remarks>
[ApiController]
[Route("api/sorting")]
[Produces("application/json")]
public class SortingController : ApiControllerBase
{
    private readonly IChangeParcelChuteService _changeParcelChuteService;
    private readonly IDebugSortService? _debugSortService;
    private readonly IPositionIndexQueueManager _queueManager;
    private readonly IChuteDropoffCallbackConfigurationRepository _callbackConfigRepository;
    private readonly ISystemClock _clock;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SortingController> _logger;

    public SortingController(
        IChangeParcelChuteService changeParcelChuteService,
        IPositionIndexQueueManager queueManager,
        IChuteDropoffCallbackConfigurationRepository callbackConfigRepository,
        ISystemClock clock,
        IWebHostEnvironment environment,
        ILogger<SortingController> logger,
        IDebugSortService? debugSortService = null)
    {
        _changeParcelChuteService = changeParcelChuteService ?? throw new ArgumentNullException(nameof(changeParcelChuteService));
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _callbackConfigRepository = callbackConfigRepository ?? throw new ArgumentNullException(nameof(callbackConfigRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugSortService = debugSortService;
    }

    #region 分拣改口

    /// <summary>
    /// 请求更改包裹的目标格口（改口）
    /// </summary>
    /// <param name="request">改口请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>改口处理结果</returns>
    /// <response code="200">改口请求已处理（可能接受、忽略或拒绝）</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 示例请求:
    /// 
    ///     POST /api/sorting/change-chute
    ///     {
    ///         "parcelId": 1001,
    ///         "requestedChuteId": 5,
    ///         "requestedAt": "2025-11-17T10:30:00Z"
    ///     }
    /// 
    /// 改口功能用于在包裹分拣过程中动态修改目标格口。
    /// 改口请求会根据包裹当前状态决定是否接受：
    /// - Accepted：改口成功，路径已重新规划
    /// - IgnoredAlreadyCompleted：包裹已完成分拣，改口被忽略
    /// - IgnoredExceptionRouted：包裹已进入异常格口，改口被忽略
    /// - RejectedInvalidState：包裹状态不允许改口
    /// - RejectedTooLate：改口请求太晚，包裹已无法改变路径
    /// </remarks>
    [HttpPost("change-chute")]
    [SwaggerOperation(
        Summary = "请求更改包裹的目标格口（改口）",
        Description = "在包裹分拣过程中动态修改目标格口，系统会根据包裹当前位置和状态决定是否接受改口请求",
        OperationId = "ChangeParcelChute",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "改口请求已处理", typeof(ChuteChangeResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ChuteChangeResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<ChuteChangeResponse>> ChangeChute(
        [FromBody] ChuteChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request body is required" });
            }

            if (request.ParcelId <= 0)
            {
                return BadRequest(new { message = "Invalid ParcelId" });
            }

            if (request.RequestedChuteId <= 0)
            {
                return BadRequest(new { message = "Invalid RequestedChuteId" });
            }

            var command = new ChangeParcelChuteCommand
            {
                ParcelId = request.ParcelId,
                RequestedChuteId = request.RequestedChuteId,
                RequestedAt = request.RequestedAt
            };

            var result = await _changeParcelChuteService.ChangeParcelChuteAsync(command, cancellationToken);

            var response = new ChuteChangeResponse
            {
                IsSuccess = result.IsSuccess,
                ParcelId = result.ParcelId,
                OriginalChuteId = result.OriginalChuteId,
                RequestedChuteId = result.RequestedChuteId,
                EffectiveChuteId = result.EffectiveChuteId,
                Outcome = result.Outcome?.ToString(),
                Message = result.Message,
                ProcessedAt = result.ProcessedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chute change request for parcel {ParcelId}", request?.ParcelId);
            return StatusCode(500, new { message = "Internal server error processing chute change request" });
        }
    }

    #endregion

    #region 分拣测试

    /// <summary>
    /// 手动触发包裹分拣（仅供测试/仿真环境）
    /// </summary>
    /// <param name="request">分拣请求，包含包裹ID和目标格口ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分拣执行结果</returns>
    /// <response code="200">分拣执行成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="403">生产环境禁止调用</response>
    /// <response code="503">服务未配置或不可用</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// **⚠️ 重要警告：仅供测试/仿真环境使用，生产环境禁止调用**
    /// 
    /// **功能说明**：
    /// 
    /// 接收包裹ID和目标格口ID，生成摆轮切换路径并执行分拣操作。
    /// 
    /// **环境限制**：
    /// - **仅在测试和仿真环境中可用**
    /// - 在生产环境（Production）中调用将返回 403 错误
    /// - 生产环境下应通过扫码或供包台触发分拣，而非此接口
    /// 
    /// **示例请求**：
    /// ```json
    /// {
    ///   "parcelId": "PKG-20231201-001",
    ///   "targetChuteId": 5
    /// }
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "parcelId": "PKG-20231201-001",
    ///   "targetChuteId": 5,
    ///   "actualChuteId": 5,
    ///   "isSuccess": true,
    ///   "message": "分拣执行成功",
    ///   "pathSegmentCount": 3
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 包裹ID必须唯一，避免与真实包裹冲突
    /// - 目标格口ID必须在系统路由配置中存在
    /// - 此接口会实际触发摆轮动作（在硬件环境下）
    /// </remarks>
    [HttpPost("test-sort")]
    [SwaggerOperation(
        Summary = "手动触发包裹分拣（仅供测试/仿真环境）",
        Description = "接收包裹ID和目标格口ID，生成并执行摆轮分拣路径。**仅在测试/仿真环境可用，生产环境禁止调用**",
        OperationId = "TriggerTestSort",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "分拣执行成功", typeof(DebugSortResponse))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(403, "生产环境禁止调用")]
    [SwaggerResponse(503, "服务未配置或不可用")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(DebugSortResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 403)]
    [ProducesResponseType(typeof(object), 503)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> TriggerTestSort(
        [FromBody] DebugSortRequest request,
        CancellationToken cancellationToken)
    {
        // 检查环境：生产环境禁止调用
        if (_environment.IsProduction())
        {
            _logger.LogWarning(
                "生产环境下尝试调用测试分拣接口 /api/sorting/test-sort，已拒绝。ParcelId: {ParcelId}",
                request.ParcelId);
            
            return StatusCode(403, new
            {
                message = "生产环境下禁止调用测试分拣接口",
                errorCode = "FORBIDDEN_IN_PRODUCTION",
                hint = "此接口仅供开发、测试和仿真环境使用。生产环境下应通过扫码或供包台触发分拣。"
            });
        }

        // 参数验证
        if (string.IsNullOrWhiteSpace(request.ParcelId))
        {
            return BadRequest(new { message = "包裹ID不能为空" });
        }

        if (request.TargetChuteId <= 0)
        {
            return BadRequest(new { message = "目标格口ID必须大于0" });
        }

        // 检查是否注入了 DebugSortService
        if (_debugSortService == null)
        {
            _logger.LogError("DebugSortService 未注册，无法执行调试分拣");
            return StatusCode(503, new 
            { 
                message = "调试分拣服务未配置或不可用",
                hint = "请确保在测试/仿真环境中正确注册 DebugSortService"
            });
        }

        try
        {
            _logger.LogInformation(
                "测试分拣：手动触发分拣，ParcelId: {ParcelId}, TargetChuteId: {TargetChuteId}",
                request.ParcelId,
                request.TargetChuteId);

            var result = await _debugSortService.ExecuteDebugSortAsync(
                request.ParcelId,
                request.TargetChuteId,
                cancellationToken);

            // Map Application layer result to Host layer response
            var response = new DebugSortResponse
            {
                ParcelId = result.ParcelId,
                TargetChuteId = result.TargetChuteId,
                IsSuccess = result.IsSuccess,
                ActualChuteId = result.ActualChuteId,
                Message = result.Message,
                FailureReason = result.FailureReason,
                PathSegmentCount = result.PathSegmentCount
            };

            if (response.IsSuccess)
            {
                _logger.LogInformation(
                    "测试分拣：分拣执行成功，ParcelId: {ParcelId}",
                    request.ParcelId);
            }
            else
            {
                _logger.LogWarning(
                    "测试分拣：分拣执行失败，ParcelId: {ParcelId}, Message: {Message}",
                    request.ParcelId,
                    response.Message);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试分拣：分拣执行异常，ParcelId: {ParcelId}", request.ParcelId);
            return StatusCode(500, new
            {
                message = "分拣执行失败",
                error = ex.Message
            });
        }
    }

    #endregion

    #region Position间隔查询

    /// <summary>
    /// 获取所有Position的触发间隔中位数
    /// </summary>
    /// <returns>各Position的触发间隔统计信息</returns>
    /// <response code="200">成功返回Position间隔数据</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 此接口用于支持包裹丢失检测方案A+（中位数自适应超时检测）。
    /// 
    /// 返回每个positionIndex从上一个positionIndex到当前positionIndex的实际触发间隔中位数。
    /// 
    /// **数据说明**：
    /// - MedianIntervalMs：最近N次触发的间隔中位数（毫秒）
    /// - SampleCount：实际采样次数（少于10次时为实际采样数）
    /// - LastTriggerTime：最后一次触发时间
    /// 
    /// **应用场景**：
    /// - 配置动态超时阈值
    /// - 监控分拣流量密度
    /// - 识别异常慢速段
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "intervals": [
    ///     {
    ///       "positionIndex": 1,
    ///       "medianIntervalMs": 350.5,
    ///       "sampleCount": 10,
    ///       "lastTriggerTime": "2025-12-13T12:00:00Z"
    ///     },
    ///     {
    ///       "positionIndex": 2,
    ///       "medianIntervalMs": 420.3,
    ///       "sampleCount": 10,
    ///       "lastTriggerTime": "2025-12-13T12:00:01Z"
    ///     }
    ///   ],
    ///   "timestamp": "2025-12-13T12:00:02Z"
    /// }
    /// ```
    /// </remarks>
    [HttpGet("position-intervals")]
    [SwaggerOperation(
        Summary = "获取所有Position的触发间隔中位数",
        Description = "返回各positionIndex的实际触发间隔统计信息，用于支持包裹丢失检测方案A+",
        OperationId = "GetPositionIntervals",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "成功返回Position间隔数据", typeof(object))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public IActionResult GetPositionIntervals()
    {
        try
        {
            // TODO: 实现Position间隔追踪服务后，从服务获取数据
            // 当前返回模拟数据结构
            var intervals = new List<PositionIntervalDto>();
            
            // 获取所有队列状态，推断有哪些position
            var queueStatuses = _queueManager.GetAllQueueStatuses();
            
            foreach (var kvp in queueStatuses.OrderBy(x => x.Key))
            {
                intervals.Add(new PositionIntervalDto
                {
                    PositionIndex = kvp.Key,
                    MedianIntervalMs = null, // TODO: 从追踪服务获取
                    SampleCount = 0,
                    MinIntervalMs = null,
                    MaxIntervalMs = null,
                    LastUpdatedAt = null
                });
            }

            return Ok(new
            {
                intervals = intervals,
                timestamp = _clock.LocalNow,
                note = "Position间隔追踪功能待实施"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Position间隔数据失败");
            return StatusCode(500, new { message = "获取Position间隔数据失败" });
        }
    }

    #endregion

    #region 落格回调配置

    /// <summary>
    /// 获取落格回调配置
    /// </summary>
    /// <returns>当前落格回调配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="404">配置不存在</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 落格回调配置用于控制何时向上游发送分拣完成通知：
    /// 
    /// **OnWheelExecution（执行摆轮时触发）**：
    /// - 执行摆轮动作时立即发送分拣完成通知
    /// - 不等待包裹实际落格
    /// - 适用于没有落格传感器或对响应速度要求高的场景
    /// 
    /// **OnSensorTrigger（落格传感器触发）**：
    /// - 等待落格传感器感应到包裹后发送分拣完成通知
    /// - 确认包裹真正落入格口
    /// - 适用于有落格传感器且需要精确确认的场景（默认模式）
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "callbackMode": "OnSensorTrigger",
    ///   "description": "落格传感器触发时发送",
    ///   "updatedAt": "2025-12-13T10:00:00Z"
    /// }
    /// ```
    /// </remarks>
    [HttpGet("chute-dropoff-callback-config")]
    [SwaggerOperation(
        Summary = "获取落格回调配置",
        Description = "返回当前落格回调模式配置（OnWheelExecution 或 OnSensorTrigger）",
        OperationId = "GetChuteDropoffCallbackConfig",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ChuteDropoffCallbackConfigDto))]
    [SwaggerResponse(404, "配置不存在")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ChuteDropoffCallbackConfigDto), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public IActionResult GetChuteDropoffCallbackConfig()
    {
        try
        {
            var config = _callbackConfigRepository.Get();

            var response = new ChuteDropoffCallbackConfigDto
            {
                TriggerMode = config.CallbackMode.ToString(),
                IsEnabled = true,  // 当前总是启用
                UpdatedAt = config.UpdatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取落格回调配置失败");
            return StatusCode(500, new { message = "获取落格回调配置失败" });
        }
    }

    /// <summary>
    /// 更新落格回调配置
    /// </summary>
    /// <param name="request">落格回调配置更新请求</param>
    /// <returns>更新后的配置</returns>
    /// <response code="200">配置更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 更新落格回调触发模式。
    /// 
    /// **可选模式**：
    /// - `OnWheelExecution`：执行摆轮动作时立即发送分拣完成通知
    /// - `OnSensorTrigger`：等待落格传感器感应后发送分拣完成通知
    /// 
    /// **示例请求**：
    /// ```json
    /// {
    ///   "callbackMode": "OnWheelExecution"
    /// }
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "callbackMode": "OnWheelExecution",
    ///   "description": "执行摆轮时发送",
    ///   "updatedAt": "2025-12-13T10:30:00Z"
    /// }
    /// ```
    /// </remarks>
    [HttpPost("chute-dropoff-callback-config")]
    [SwaggerOperation(
        Summary = "更新落格回调配置",
        Description = "设置落格回调触发模式（OnWheelExecution 或 OnSensorTrigger）",
        OperationId = "UpdateChuteDropoffCallbackConfig",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "配置更新成功", typeof(ChuteDropoffCallbackConfigDto))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ChuteDropoffCallbackConfigDto), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public IActionResult UpdateChuteDropoffCallbackConfig(
        [FromBody] UpdateChuteDropoffCallbackConfigRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TriggerMode))
            {
                return BadRequest(new { message = "TriggerMode 不能为空" });
            }

            // 解析枚举
            if (!Enum.TryParse<Core.Enums.Sorting.ChuteDropoffCallbackMode>(request.TriggerMode, true, out var mode))
            {
                return BadRequest(new 
                { 
                    message = "无效的 TriggerMode",
                    hint = "支持的值: OnWheelExecution, OnSensorTrigger"
                });
            }

            // 创建或更新配置
            var config = new ChuteDropoffCallbackConfiguration
            {
                ConfigName = "chute-dropoff-callback",  // 设置required属性
                CallbackMode = mode
            };

            _callbackConfigRepository.Update(config);

            _logger.LogInformation(
                "落格回调配置已更新: {CallbackMode}",
                mode);

            // 重新获取config以确保有UpdatedAt
            var updatedConfig = _callbackConfigRepository.Get();
            
            var response = new ChuteDropoffCallbackConfigDto
            {
                TriggerMode = updatedConfig.CallbackMode.ToString(),
                IsEnabled = true,  // 当前总是启用
                UpdatedAt = updatedConfig.UpdatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新落格回调配置失败");
            return StatusCode(500, new { message = "更新落格回调配置失败" });
        }
    }

    #endregion
}
