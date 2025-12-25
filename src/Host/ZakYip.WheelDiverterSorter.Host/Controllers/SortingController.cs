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
/// - 检测开关配置（统一管理干扰检测、超时检测、包裹丢失检测三个开关）
/// 
/// **落格回调模式**：
/// - OnWheelExecution：执行摆轮动作时立即发送分拣完成通知
/// - OnSensorTrigger：等待落格传感器感应后发送分拣完成通知（默认）
/// 
/// **检测开关**：
/// - 干扰检测开关（提前触发检测）：防止包裹提前到达导致的错位问题
/// - 超时检测开关：启用包裹传输超时检测和处理
/// - 包裹丢失检测开关：启用包裹丢失的主动巡检和处理
/// 
/// **Position间隔查询**：
/// 支持Position间隔统计观测，提供各positionIndex的实际触发间隔中位数。
/// ⚠️ 重要：中位数数据仅用于观测和监控，不用于任何分拣逻辑判断。
/// 所有超时判断、丢失判定均基于输送线配置（ConveyorSegmentConfiguration）。
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
    private readonly ISystemConfigurationRepository _systemConfigRepository;
    private readonly IConveyorSegmentRepository _conveyorSegmentRepository;
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
        ISystemConfigurationRepository systemConfigRepository,
        IConveyorSegmentRepository conveyorSegmentRepository,
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
        _systemConfigRepository = systemConfigRepository ?? throw new ArgumentNullException(nameof(systemConfigRepository));
        _conveyorSegmentRepository = conveyorSegmentRepository ?? throw new ArgumentNullException(nameof(conveyorSegmentRepository));
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
    /// ⚠️ 重要：此接口返回的中位数数据仅用于观测和监控，不用于任何分拣逻辑判断。
    /// 所有超时判断、丢失判定均基于输送线配置（ConveyorSegmentConfiguration）。
    /// 
    /// 返回每个positionIndex从上一个positionIndex到当前positionIndex的实际触发间隔中位数。
    /// 
    /// **数据说明**：
    /// - MedianIntervalMs：最近N次触发的间隔中位数（毫秒）- 仅供观测
    /// - SampleCount：实际采样次数（少于10次时为实际采样数）
    /// - LastTriggerTime：最后一次触发时间
    /// 
    /// **应用场景**：
    /// - 监控分拣流量密度
    /// - 识别异常慢速段
    /// - 性能分析和优化
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
        Summary = "获取所有Position的触发间隔中位数（仅观测）",
        Description = "返回各positionIndex的实际触发间隔统计信息，用于观测和监控（不用于分拣判断）",
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


    #region 检测开关统一配置

    /// <summary>
    /// 获取所有检测开关配置
    /// </summary>
    /// <returns>当前检测开关配置</returns>
    /// <response code="200">成功返回配置</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 统一查询三个检测开关的状态：
    /// 
    /// **干扰检测开关 (EnableInterferenceDetection)**：
    /// - 启用提前触发检测，防止包裹提前到达导致的错位问题
    /// - 默认值：false（禁用）
    /// 
    /// **超时检测开关 (EnableTimeoutDetection)**：
    /// - 启用包裹传输超时检测和处理
    /// - 默认值：true（启用）
    /// 
    /// **包裹丢失检测开关 (EnableParcelLossDetection)**：
    /// - 启用包裹丢失的主动巡检和处理
    /// - 默认值：true（启用）
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "enableInterferenceDetection": false,
    ///   "enableTimeoutDetection": true,
    ///   "enableParcelLossDetection": true,
    ///   "updatedAt": "2025-12-25T15:00:00Z"
    /// }
    /// ```
    /// </remarks>
    [HttpGet("detection-switches")]
    [SwaggerOperation(
        Summary = "获取所有检测开关配置",
        Description = "统一查询干扰检测、超时检测、包裹丢失检测三个开关的状态",
        OperationId = "GetDetectionSwitches",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "成功返回配置", typeof(ApiResponse<DetectionSwitchesDto>))]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<DetectionSwitchesDto>), 200)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ApiResponse<DetectionSwitchesDto>> GetDetectionSwitches()
    {
        try
        {
            // 获取系统配置（干扰检测开关）
            var systemConfig = _systemConfigRepository.Get();
            
            // 获取包裹丢失检测配置
            var lossDetectionConfig = _lossDetectionConfigRepository.Get();
            
            // 获取所有输送段配置（超时检测开关）
            var segments = _conveyorSegmentRepository.GetAll();
            // 超时检测状态：所有段都启用才算启用
            var enableTimeoutDetection = segments.Any() && segments.All(s => s.EnableLossDetection);
            
            // 取最新的更新时间
            var latestUpdateTime = new[] 
            { 
                systemConfig.UpdatedAt, 
                lossDetectionConfig.UpdatedAt 
            }.Max();
            
            var response = new DetectionSwitchesDto
            {
                EnableInterferenceDetection = systemConfig.EnableEarlyTriggerDetection,
                EnableTimeoutDetection = enableTimeoutDetection,
                EnableParcelLossDetection = lossDetectionConfig.IsEnabled,
                UpdatedAt = latestUpdateTime
            };
            
            return Ok(ApiResponse<DetectionSwitchesDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取检测开关配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("获取检测开关配置失败"));
        }
    }

    /// <summary>
    /// 更新检测开关配置
    /// </summary>
    /// <param name="request">检测开关配置更新请求</param>
    /// <returns>更新后的配置</returns>
    /// <response code="200">配置更新成功</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 统一更新三个检测开关的配置，所有字段均为可选，仅更新提供的字段。
    /// 
    /// **干扰检测开关 (EnableInterferenceDetection)**：
    /// - 可选参数
    /// - 控制提前触发检测功能
    /// - true: 启用检测，防止包裹提前到达导致错位
    /// - false: 禁用检测
    /// - 如不提供则保持当前值不变
    /// 
    /// **超时检测开关 (EnableTimeoutDetection)**：
    /// - 可选参数
    /// - 控制所有输送段的超时检测功能
    /// - true: 启用检测，包裹超时后路由到异常格口
    /// - false: 禁用检测，包裹超时后继续等待
    /// - 如不提供则保持当前值不变
    /// - ⚠️ 注意：此开关会同时影响所有输送段配置
    /// 
    /// **包裹丢失检测开关 (EnableParcelLossDetection)**：
    /// - 可选参数
    /// - 控制包裹丢失的主动巡检功能
    /// - true: 启用检测，后台服务定期巡检队列
    /// - false: 禁用检测，包裹不会因丢失而被自动移除
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
    ///   "enableInterferenceDetection": false,
    ///   "enableTimeoutDetection": true,
    ///   "enableParcelLossDetection": true
    /// }
    /// ```
    /// 
    /// **示例响应**：
    /// ```json
    /// {
    ///   "enableInterferenceDetection": false,
    ///   "enableTimeoutDetection": true,
    ///   "enableParcelLossDetection": true,
    ///   "updatedAt": "2025-12-25T15:30:00Z"
    /// }
    /// ```
    /// 
    /// **注意事项**：
    /// - 配置修改立即生效
    /// - 建议在低峰期或测试环境中调整参数
    /// - 超时检测开关会更新所有输送段配置
    /// </remarks>
    [HttpPut("detection-switches")]
    [SwaggerOperation(
        Summary = "更新检测开关配置",
        Description = "统一更新干扰检测、超时检测、包裹丢失检测三个开关的状态。修改后立即生效。",
        OperationId = "UpdateDetectionSwitches",
        Tags = new[] { "分拣管理" }
    )]
    [SwaggerResponse(200, "配置更新成功", typeof(ApiResponse<DetectionSwitchesDto>))]
    [SwaggerResponse(400, "请求参数无效")]
    [SwaggerResponse(500, "服务器内部错误")]
    [ProducesResponseType(typeof(ApiResponse<DetectionSwitchesDto>), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public ActionResult<ApiResponse<DetectionSwitchesDto>> UpdateDetectionSwitches(
        [FromBody] UpdateDetectionSwitchesRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.BadRequest("请求体不能为空"));
            }

            // 如果没有提供任何更新字段，返回错误
            if (request.EnableInterferenceDetection == null &&
                request.EnableTimeoutDetection == null &&
                request.EnableParcelLossDetection == null)
            {
                return BadRequest(ApiResponse<object>.BadRequest("至少需要提供一个检测开关字段进行更新"));
            }

            var now = _clock.LocalNow;

            // 更新干扰检测开关（系统配置）
            if (request.EnableInterferenceDetection.HasValue)
            {
                var systemConfig = _systemConfigRepository.Get();
                systemConfig.EnableEarlyTriggerDetection = request.EnableInterferenceDetection.Value;
                systemConfig.UpdatedAt = now;
                _systemConfigRepository.Update(systemConfig);
                
                _logger.LogInformation(
                    "干扰检测开关已更新: {EnableInterferenceDetection}",
                    request.EnableInterferenceDetection.Value);
            }

            // 更新超时检测开关（输送段配置）
            if (request.EnableTimeoutDetection.HasValue)
            {
                var segments = _conveyorSegmentRepository.GetAll();
                foreach (var segment in segments)
                {
                    // 创建新的记录以更新不可变对象
                    var updatedSegment = segment with
                    {
                        EnableLossDetection = request.EnableTimeoutDetection.Value,
                        UpdatedAt = now
                    };
                    _conveyorSegmentRepository.Update(updatedSegment);
                }
                
                _logger.LogInformation(
                    "超时检测开关已更新: {EnableTimeoutDetection}, 影响 {SegmentCount} 个输送段",
                    request.EnableTimeoutDetection.Value,
                    segments.Count());
            }

            // 更新包裹丢失检测开关
            if (request.EnableParcelLossDetection.HasValue)
            {
                var lossDetectionConfig = _lossDetectionConfigRepository.Get();
                lossDetectionConfig.IsEnabled = request.EnableParcelLossDetection.Value;
                lossDetectionConfig.UpdatedAt = now;
                _lossDetectionConfigRepository.Update(lossDetectionConfig);
                
                _logger.LogInformation(
                    "包裹丢失检测开关已更新: {EnableParcelLossDetection}",
                    request.EnableParcelLossDetection.Value);
            }

            // 重新获取更新后的配置并返回
            var updatedSystemConfig = _systemConfigRepository.Get();
            var updatedLossDetectionConfig = _lossDetectionConfigRepository.Get();
            var updatedSegments = _conveyorSegmentRepository.GetAll();
            var enableTimeoutDetection = updatedSegments.Any() && updatedSegments.All(s => s.EnableLossDetection);

            var response = new DetectionSwitchesDto
            {
                EnableInterferenceDetection = updatedSystemConfig.EnableEarlyTriggerDetection,
                EnableTimeoutDetection = enableTimeoutDetection,
                EnableParcelLossDetection = updatedLossDetectionConfig.IsEnabled,
                UpdatedAt = now
            };

            _logger.LogInformation(
                "检测开关配置已更新: 干扰检测={InterferenceDetection}, 超时检测={TimeoutDetection}, 丢失检测={LossDetection}",
                response.EnableInterferenceDetection,
                response.EnableTimeoutDetection,
                response.EnableParcelLossDetection);

            return Ok(ApiResponse<DetectionSwitchesDto>.Ok(response, "检测开关配置已更新"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新检测开关配置失败");
            return StatusCode(500, ApiResponse<object>.ServerError("更新检测开关配置失败"));
        }
    }

    #endregion
}
