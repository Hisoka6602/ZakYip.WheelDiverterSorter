using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
using ZakYip.WheelDiverterSorter.Execution.Tracking;
using ZakYip.WheelDiverterSorter.Observability;

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
    private readonly IParcelLossDetectionConfigurationRepository _lossDetectionConfigRepository;
    private readonly ISystemClock _clock;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SortingController> _logger;
    private readonly Execution.Tracking.IPositionIntervalTracker? _intervalTracker;
    private readonly AlarmService _alarmService;
    private readonly ISortingStatisticsService _statisticsService;

    public SortingController(
        IChangeParcelChuteService changeParcelChuteService,
        IPositionIndexQueueManager queueManager,
        IChuteDropoffCallbackConfigurationRepository callbackConfigRepository,
        IParcelLossDetectionConfigurationRepository lossDetectionConfigRepository,
        ISystemClock clock,
        IWebHostEnvironment environment,
        ILogger<SortingController> logger,
        AlarmService alarmService,
        ISortingStatisticsService statisticsService,
        IDebugSortService? debugSortService = null,
        Execution.Tracking.IPositionIntervalTracker? intervalTracker = null)
    {
        _changeParcelChuteService = changeParcelChuteService ?? throw new ArgumentNullException(nameof(changeParcelChuteService));
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _callbackConfigRepository = callbackConfigRepository ?? throw new ArgumentNullException(nameof(callbackConfigRepository));
        _lossDetectionConfigRepository = lossDetectionConfigRepository ?? throw new ArgumentNullException(nameof(lossDetectionConfigRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));
        _statisticsService = statisticsService ?? throw new ArgumentNullException(nameof(statisticsService));
        _debugSortService = debugSortService;
        _intervalTracker = intervalTracker;
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
            // 如果间隔追踪器可用，返回实际数据
            if (_intervalTracker != null)
            {
                var statistics = _intervalTracker.GetAllStatistics();
                
                var intervals = statistics.Select(stat => new PositionIntervalDto
                {
                    PositionIndex = stat.PositionIndex,
                    MedianIntervalMs = stat.MedianIntervalMs,
                    SampleCount = stat.SampleCount,
                    MinIntervalMs = stat.MinIntervalMs,
                    MaxIntervalMs = stat.MaxIntervalMs,
                    LastUpdatedAt = stat.LastUpdatedAt
                }).ToList();
                
                // 如果追踪器中没有数据，从队列管理器获取所有position并返回空统计
                if (!intervals.Any())
                {
                    var queueStatuses = _queueManager.GetAllQueueStatuses();
                    intervals = queueStatuses.Keys.OrderBy(k => k).Select(positionIndex => new PositionIntervalDto
                    {
                        PositionIndex = positionIndex,
                        MedianIntervalMs = null,
                        SampleCount = 0,
                        MinIntervalMs = null,
                        MaxIntervalMs = null,
                        LastUpdatedAt = null
                    }).ToList();
                }

                return Ok(new
                {
                    intervals = intervals,
                    timestamp = _clock.LocalNow,
                    note = intervals.Any(i => i.MedianIntervalMs.HasValue) 
                        ? "实时间隔统计数据" 
                        : "等待包裹触发以收集间隔数据"
                });
            }
            
            // 间隔追踪器不可用，返回默认空数据结构
            var queueStatusesDefault = _queueManager.GetAllQueueStatuses();
            var intervalsDefault = queueStatusesDefault.Keys.OrderBy(k => k).Select(positionIndex => new PositionIntervalDto
            {
                PositionIndex = positionIndex,
                MedianIntervalMs = null,
                SampleCount = 0,
                MinIntervalMs = null,
                MaxIntervalMs = null,
                LastUpdatedAt = null
            }).ToList();

            return Ok(new
            {
                intervals = intervalsDefault,
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
                TriggerMode = config.CallbackMode,
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
            if (request == null)
            {
                return BadRequest(new { message = "请求体不能为空" });
            }

            // 创建或更新配置
            var config = new ChuteDropoffCallbackConfiguration
            {
                ConfigName = "chute-dropoff-callback",  // 设置required属性
                CallbackMode = request.TriggerMode
            };

            _callbackConfigRepository.Update(config);

            _logger.LogInformation(
                "落格回调配置已更新: {CallbackMode}",
                request.TriggerMode);

            // 重新获取config以确保有UpdatedAt
            var updatedConfig = _callbackConfigRepository.Get();
            
            var response = new ChuteDropoffCallbackConfigDto
            {
                TriggerMode = updatedConfig.CallbackMode,
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

    #region 分拣统计

    /// <summary>
    /// 获取当前分拣失败率
    /// </summary>
    /// <returns>分拣失败率统计信息</returns>
    /// <response code="200">成功返回失败率</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回当前时间窗口内的分拣失败率，包括小数值和百分比形式
    /// </remarks>
    [HttpGet("failure-rate")]
    [SwaggerOperation(
        Summary = "获取当前分拣失败率",
        Description = "返回系统当前的分拣失败率统计，包括失败率小数值和百分比表示",
        OperationId = "GetSortingFailureRate",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "成功返回失败率", typeof(object))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<object> GetFailureRate()
    {
        try
        {
            var failureRate = _alarmService.GetSortingFailureRate();
            return Ok(new
            {
                failureRate,
                percentage = $"{failureRate * 100:F2}%"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分拣失败率失败");
            return StatusCode(500, new { message = "获取分拣失败率失败" });
        }
    }

    /// <summary>
    /// 获取分拣统计数据
    /// </summary>
    /// <returns>分拣统计信息</returns>
    /// <response code="200">成功返回统计数据</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回分拣系统的实时统计数据，包括：
    /// - successCount: 分拣成功数量
    /// - timeoutCount: 分拣超时数量（包裹延迟但仍被导向异常口）
    /// - lostCount: 包裹丢失数量（包裹物理丢失，从队列删除）
    /// - affectedCount: 受影响包裹数量（因其他包裹丢失而被重路由到异常口）
    /// 
    /// 统计数据：
    /// - 永久存储在内存中，不过期
    /// - 使用原子操作保证线程安全
    /// - 支持超高并发查询（无锁设计）
    /// - 可通过 POST /api/sorting/reset-statistics 重置
    /// 
    /// 示例响应：
    /// ```json
    /// {
    ///   "successCount": 12345,
    ///   "timeoutCount": 23,
    ///   "lostCount": 5,
    ///   "affectedCount": 8,
    ///   "timestamp": "2025-12-14T12:00:00Z"
    /// }
    /// ```
    /// </remarks>
    [HttpGet("statistics")]
    [SwaggerOperation(
        Summary = "获取分拣统计数据",
        Description = "返回分拣系统的实时统计数据（成功/超时/丢失/受影响），支持超高并发查询",
        OperationId = "GetSortingStatistics",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "成功返回统计数据", typeof(SortingStatisticsDto))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(SortingStatisticsDto), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<SortingStatisticsDto> GetStatistics()
    {
        try
        {
            var stats = new SortingStatisticsDto
            {
                SuccessCount = _statisticsService.SuccessCount,
                TimeoutCount = _statisticsService.TimeoutCount,
                LostCount = _statisticsService.LostCount,
                AffectedCount = _statisticsService.AffectedCount,
                Timestamp = _clock.LocalNow
            };
            
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分拣统计数据失败");
            return StatusCode(500, new { message = "获取分拣统计数据失败" });
        }
    }

    /// <summary>
    /// 重置分拣统计计数器
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">成功重置统计计数器</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 重置分拣成功/失败计数器和详细统计计数器，用于开始新的统计周期或测试场景
    /// </remarks>
    [HttpPost("reset-statistics")]
    [SwaggerOperation(
        Summary = "重置分拣统计计数器",
        Description = "清除当前的分拣统计数据，包括失败率和详细统计计数器，通常用于开始新的统计周期",
        OperationId = "ResetSortingStatistics",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "成功重置统计计数器", typeof(object))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult ResetStatistics()
    {
        try
        {
            // 保证原子性：两个服务要么都重置成功，要么都不重置
            // 如果第二个重置失败，回滚第一个服务的重置
            bool alarmReset = false;
            bool statisticsReset = false;
            
            try
            {
                _alarmService.ResetSortingStatistics();
                alarmReset = true;
                
                _statisticsService.Reset();
                statisticsReset = true;
                
                _logger.LogInformation("分拣统计计数器已重置（包含失败率和详细统计） / Sorting statistics reset (including failure rate and detailed statistics)");
                return Ok(new { message = "统计计数器已重置 / Statistics reset" });
            }
            catch (Exception ex)
            {
                // 如果第二个操作失败但第一个成功，尝试回滚第一个
                if (alarmReset && !statisticsReset)
                {
                    _logger.LogWarning("统计服务重置失败，尝试回滚告警服务重置");
                    // 注意：AlarmService.ResetSortingStatistics 不支持回滚，
                    // 这是一个已知限制，记录在技术债文档中
                }
                
                _logger.LogError(ex, "重置统计失败，alarmReset={AlarmReset}, statisticsReset={StatisticsReset}", 
                    alarmReset, statisticsReset);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置统计失败");
            return StatusCode(500, new { message = "重置统计失败" });
        }
    }

    #endregion

    #region 包裹丢失检测配置

    /// <summary>
    /// 获取包裹丢失检测配置
    /// </summary>
    /// <returns>当前包裹丢失检测配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 返回包裹丢失检测的相关配置参数，包括：
    /// 
    /// **丢失检测系数 (LostDetectionMultiplier)**：
    /// - 用于计算丢失判定阈值 = 中位数间隔 * 丢失检测系数
    /// - 默认值：1.5
    /// - 推荐范围：1.5-2.5
    /// - 值越小越敏感，但可能误报；值越大越宽松，但可能漏报
    /// 
    /// **超时检测系数 (TimeoutMultiplier)**：
    /// - 用于计算超时判定阈值 = 中位数间隔 * 超时检测系数
    /// - 默认值：3.0
    /// - 推荐范围：2.5-3.5
    /// - 超时包裹会被导向异常格口，但不会从队列删除
    /// 
    /// **历史窗口大小 (WindowSize)**：
    /// - 保留最近N个间隔样本用于统计
    /// - 默认值：10
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "monitoringIntervalMs": 60,
    ///   "lostDetectionMultiplier": 1.5,
    ///   "timeoutMultiplier": 3.0,
    ///   "windowSize": 10
    /// }
    /// ```
    /// </remarks>
    [HttpGet("loss-detection-config")]
    [SwaggerOperation(
        Summary = "获取包裹丢失检测配置",
        Description = "返回当前包裹丢失检测的配置参数，包括监控间隔、丢失检测系数、超时检测系数等",
        OperationId = "GetParcelLossDetectionConfig",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ParcelLossDetectionConfigDto))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ParcelLossDetectionConfigDto), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ParcelLossDetectionConfigDto> GetLossDetectionConfig()
    {
        try
        {
            var config = _lossDetectionConfigRepository.Get();
            
            var response = new ParcelLossDetectionConfigDto
            {
                IsEnabled = config.IsEnabled,
                MonitoringIntervalMs = config.MonitoringIntervalMs,
                AutoClearMedianIntervalMs = config.AutoClearMedianIntervalMs,
                AutoClearQueueIntervalSeconds = config.AutoClearQueueIntervalSeconds,
                LostDetectionMultiplier = config.LostDetectionMultiplier,
                TimeoutMultiplier = config.TimeoutMultiplier,
                WindowSize = config.WindowSize
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取包裹丢失检测配置失败");
            return StatusCode(500, new { message = "获取包裹丢失检测配置失败" });
        }
    }

    /// <summary>
    /// 更新包裹丢失检测配置
    /// </summary>
    /// <param name="request">配置更新请求</param>
    /// <returns>更新后的配置</returns>
    /// <response code="200">配置更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 更新包裹丢失检测的配置参数。修改后立即生效。
    /// 
    /// **启用开关 (IsEnabled)**：
    /// - 可选参数
    /// - 控制整个包裹丢失检测功能的开关
    /// - true: 启用丢失检测和超时检测
    /// - false: 关闭所有检测，包裹不会因超时或丢失而被移除
    /// - 如不提供则保持当前值不变
    /// 
    /// **监控间隔 (MonitoringIntervalMs)**：
    /// - 可选参数
    /// - 取值范围：50-1000ms
    /// - 包裹丢失监控服务扫描队列的时间间隔
    /// - 默认值：60ms
    /// - 如不提供则保持当前值不变
    /// 
    /// **自动清空中位数间隔 (AutoClearMedianIntervalMs)**：
    /// - 可选参数
    /// - 取值范围：0-3600000ms (0-60分钟)
    /// - 当超过此时间未创建新包裹时，自动清空所有 Position 的中位数统计数据
    /// - 设置为 0 表示不自动清空
    /// - 默认值：300000ms (5分钟)
    /// - 如不提供则保持当前值不变
    /// 
    /// **丢失检测系数 (LostDetectionMultiplier)**：
    /// - 可选参数
    /// - 取值范围：1.0-5.0
    /// - 建议根据实际线速和包裹密度调整
    /// - 例如：高速线体建议使用较小值（1.5-2.0），低速线体可使用较大值（2.0-2.5）
    /// - 如不提供则保持当前值不变
    /// 
    /// **超时检测系数 (TimeoutMultiplier)**：
    /// - 可选参数
    /// - 取值范围：1.5-10.0
    /// - 如不提供则保持当前值不变
    /// 
    /// **历史窗口大小 (WindowSize)**：
    /// - 可选参数
    /// - 取值范围：10-10000
    /// - 保留最近N个间隔样本用于计算中位数
    /// - 如不提供则保持当前值不变
    /// 
    /// **部分更新支持**：
    /// - 所有字段均为可选，仅更新提供的字段
    /// - 未提供的字段保持当前值不变
    /// - 支持单独更新任意字段组合
    /// 
    /// **示例请求**：
    /// ```json
    /// {
    ///   "isEnabled": true,
    ///   "monitoringIntervalMs": 60,
    ///   "autoClearMedianIntervalMs": 300000,
    ///   "autoClearQueueIntervalSeconds": 30,
    ///   "lostDetectionMultiplier": 2.0,
    ///   "timeoutMultiplier": 3.5,
    ///   "windowSize": 10
    /// }
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "isEnabled": true,
    ///   "monitoringIntervalMs": 60,
    ///   "autoClearMedianIntervalMs": 300000,
    ///   "autoClearQueueIntervalSeconds": 30,
    ///   "lostDetectionMultiplier": 2.0,
    ///   "timeoutMultiplier": 3.5,
    ///   "windowSize": 10
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 配置修改立即生效，影响后续的丢失判定
    /// - 已在队列中的任务不受影响（使用创建时的阈值）
    /// - 建议在低峰期或测试环境中调整参数
    /// </remarks>
    [HttpPost("loss-detection-config")]
    [SwaggerOperation(
        Summary = "更新包裹丢失检测配置",
        Description = "修改包裹丢失检测的配置参数，包括启用开关、丢失检测系数、超时检测系数等。修改后立即生效。",
        OperationId = "UpdateParcelLossDetectionConfig",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "配置更新成功", typeof(ParcelLossDetectionConfigDto))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ParcelLossDetectionConfigDto), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ParcelLossDetectionConfigDto> UpdateLossDetectionConfig(
        [FromBody] UpdateParcelLossDetectionConfigRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "请求体不能为空" });
            }

            // 验证参数范围
            if (request.LostDetectionMultiplier.HasValue &&
                (request.LostDetectionMultiplier.Value < 1.0 || request.LostDetectionMultiplier.Value > 5.0))
            {
                return BadRequest(new { message = "丢失检测系数必须在1.0-5.0之间" });
            }

            if (request.TimeoutMultiplier.HasValue && 
                (request.TimeoutMultiplier.Value < 1.5 || request.TimeoutMultiplier.Value > 10.0))
            {
                return BadRequest(new { message = "超时检测系数必须在1.5-10.0之间" });
            }

            if (request.MonitoringIntervalMs.HasValue &&
                (request.MonitoringIntervalMs.Value < 50 || request.MonitoringIntervalMs.Value > 1000))
            {
                return BadRequest(new { message = "监控间隔必须在50-1000ms之间" });
            }

            if (request.AutoClearMedianIntervalMs.HasValue &&
                (request.AutoClearMedianIntervalMs.Value < 0 || request.AutoClearMedianIntervalMs.Value > 3600000))
            {
                return BadRequest(new { message = "自动清空中位数间隔必须在0-3600000ms之间" });
            }

            if (request.AutoClearQueueIntervalSeconds.HasValue &&
                (request.AutoClearQueueIntervalSeconds.Value < 0 || request.AutoClearQueueIntervalSeconds.Value > 600))
            {
                return BadRequest(new { message = "自动清空队列间隔必须在0-600秒之间" });
            }

            if (request.WindowSize.HasValue &&
                (request.WindowSize.Value < 10 || request.WindowSize.Value > 10000))
            {
                return BadRequest(new { message = "历史窗口大小必须在10-10000之间" });
            }

            // 获取当前配置
            var config = _lossDetectionConfigRepository.Get();
            
            // 更新配置值（仅更新提供的字段）
            if (request.IsEnabled.HasValue)
            {
                config.IsEnabled = request.IsEnabled.Value;
            }
            
            if (request.LostDetectionMultiplier.HasValue)
            {
                config.LostDetectionMultiplier = request.LostDetectionMultiplier.Value;
            }
            
            if (request.TimeoutMultiplier.HasValue)
            {
                config.TimeoutMultiplier = request.TimeoutMultiplier.Value;
            }
            
            if (request.MonitoringIntervalMs.HasValue)
            {
                config.MonitoringIntervalMs = request.MonitoringIntervalMs.Value;
            }
            
            if (request.AutoClearMedianIntervalMs.HasValue)
            {
                config.AutoClearMedianIntervalMs = request.AutoClearMedianIntervalMs.Value;
            }
            
            if (request.AutoClearQueueIntervalSeconds.HasValue)
            {
                config.AutoClearQueueIntervalSeconds = request.AutoClearQueueIntervalSeconds.Value;
            }
            
            if (request.WindowSize.HasValue)
            {
                config.WindowSize = request.WindowSize.Value;
            }
            
            // 设置更新时间
            config.UpdatedAt = _clock.LocalNow;
            
            // 保存到数据库
            _lossDetectionConfigRepository.Update(config);
            
            _logger.LogInformation(
                "包裹丢失检测配置已更新: IsEnabled={IsEnabled}, MonitoringIntervalMs={MonitoringInterval}ms, " +
                "AutoClearMedianIntervalMs={AutoClearMedianInterval}ms, AutoClearQueueIntervalSeconds={AutoClearQueueInterval}s, " +
                "LostDetectionMultiplier={LostMultiplier}, TimeoutMultiplier={TimeoutMultiplier}, WindowSize={WindowSize}",
                config.IsEnabled,
                config.MonitoringIntervalMs,
                config.AutoClearMedianIntervalMs,
                config.AutoClearQueueIntervalSeconds,
                config.LostDetectionMultiplier,
                config.TimeoutMultiplier,
                config.WindowSize);
            
            // 返回更新后的配置
            var response = new ParcelLossDetectionConfigDto
            {
                IsEnabled = config.IsEnabled,
                MonitoringIntervalMs = config.MonitoringIntervalMs,
                AutoClearMedianIntervalMs = config.AutoClearMedianIntervalMs,
                AutoClearQueueIntervalSeconds = config.AutoClearQueueIntervalSeconds,
                LostDetectionMultiplier = config.LostDetectionMultiplier,
                TimeoutMultiplier = config.TimeoutMultiplier,
                WindowSize = config.WindowSize
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新包裹丢失检测配置失败");
            return StatusCode(500, new { message = "更新包裹丢失检测配置失败" });
        }
    }

    #endregion
}
