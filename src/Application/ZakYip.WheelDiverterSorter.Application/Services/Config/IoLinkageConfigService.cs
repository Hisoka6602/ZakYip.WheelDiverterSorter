using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Validation;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// IO 联动配置服务实现
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public class IoLinkageConfigService : IIoLinkageConfigService
{
    private static readonly object IoLinkageConfigCacheKey = new();

    private readonly IIoLinkageConfigurationRepository _repository;
    private readonly IIoLinkageDriver _ioLinkageDriver;
    private readonly IIoLinkageCoordinator _ioLinkageCoordinator;
    private readonly ISlidingConfigCache _configCache;
    private readonly ISystemClock _systemClock;
    private readonly ILogger<IoLinkageConfigService> _logger;
    private readonly IConfigurationAuditLogger _auditLogger;

    public IoLinkageConfigService(
        IIoLinkageConfigurationRepository repository,
        IIoLinkageDriver ioLinkageDriver,
        IIoLinkageCoordinator ioLinkageCoordinator,
        ISlidingConfigCache configCache,
        ISystemClock systemClock,
        ILogger<IoLinkageConfigService> logger,
        IConfigurationAuditLogger auditLogger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ioLinkageDriver = ioLinkageDriver ?? throw new ArgumentNullException(nameof(ioLinkageDriver));
        _ioLinkageCoordinator = ioLinkageCoordinator ?? throw new ArgumentNullException(nameof(ioLinkageCoordinator));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    /// <inheritdoc />
    public IoLinkageConfiguration GetConfiguration()
    {
        return _configCache.GetOrAdd(IoLinkageConfigCacheKey, () => _repository.Get());
    }

    /// <inheritdoc />
    public IoLinkageConfigUpdateResult UpdateConfiguration(UpdateIoLinkageConfigCommand command)
    {
        try
        {
            // 获取修改前的配置
            var beforeConfig = _repository.Get();

            // 将命令映射到域配置模型
            var config = MapToConfiguration(command);

            // 保存配置
            _repository.Update(config);

            // 热更新：立即刷新缓存
            var updatedConfig = _repository.Get();
            _configCache.Set(IoLinkageConfigCacheKey, updatedConfig);

            // 记录配置审计日志
            _auditLogger.LogConfigurationChange(
                configName: "IoLinkageConfiguration",
                operationType: "Update",
                beforeConfig: beforeConfig,
                afterConfig: updatedConfig);

            _logger.LogInformation(
                "IO 联动配置已更新（热更新生效）: Enabled={Enabled}, RunningIos={RunningCount}, StoppedIos={StoppedCount}, " +
                "EmergencyStopIos={EmergencyStopCount}, UpstreamExceptionIos={UpstreamExceptionCount}, DiverterExceptionIos={DiverterExceptionCount}, " +
                "PostPreStartWarningIos={PostPreStartWarningCount}, WheelDiverterDisconnectedIos={WheelDiverterDisconnectedCount}",
                updatedConfig.Enabled,
                updatedConfig.RunningStateIos.Count,
                updatedConfig.StoppedStateIos.Count,
                updatedConfig.EmergencyStopStateIos.Count,
                updatedConfig.UpstreamConnectionExceptionStateIos.Count,
                updatedConfig.DiverterExceptionStateIos.Count,
                updatedConfig.PostPreStartWarningStateIos.Count,
                updatedConfig.WheelDiverterDisconnectedStateIos.Count);

            return new IoLinkageConfigUpdateResult(true, null, updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 IO 联动配置失败");
            return new IoLinkageConfigUpdateResult(false, "更新 IO 联动配置失败", null);
        }
    }

    /// <inheritdoc />
    public async Task<IoLinkageTriggerResult> TriggerIoLinkageAsync(SystemState systemState)
    {
        try
        {
            var config = GetConfiguration();

            if (!config.Enabled)
            {
                return new IoLinkageTriggerResult
                {
                    Success = false,
                    SystemState = systemState.ToString(),
                    ErrorMessage = "IO 联动功能未启用"
                };
            }

            // 转换为 IoLinkageOptions 用于协调器
            var options = ConvertToOptions(config);

            // 确定需要设置的 IO 点
            var ioPoints = _ioLinkageCoordinator.DetermineIoLinkagePoints(systemState, options);

            if (ioPoints.Count == 0)
            {
                return new IoLinkageTriggerResult
                {
                    Success = false,
                    SystemState = systemState.ToString(),
                    ErrorMessage = $"系统状态 {systemState} 下无 IO 联动配置"
                };
            }

            // 执行 IO 联动
            await _ioLinkageDriver.SetIoPointsAsync(ioPoints);

            _logger.LogInformation(
                "手动触发 IO 联动成功: SystemState={SystemState}, IoPointsCount={Count}",
                systemState,
                ioPoints.Count);

            return new IoLinkageTriggerResult
            {
                Success = true,
                SystemState = systemState.ToString(),
                TriggeredIoPoints = ioPoints.Select(p => new IoPointInfo
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                }).ToList()
            };
        }
        catch (AggregateException aggEx)
        {
            // TD-IOLINKAGE-004: 特别处理聚合异常，提供更详细的错误信息
            var innerMessages = string.Join("; ", aggEx.InnerExceptions.Select(e => e.Message));
            _logger.LogError(
                aggEx,
                "触发 IO 联动部分失败: SystemState={SystemState}, 错误数量={ErrorCount}, 详细信息={Details}",
                systemState,
                aggEx.InnerExceptions.Count,
                innerMessages);
            
            return new IoLinkageTriggerResult
            {
                Success = false,
                SystemState = systemState.ToString(),
                ErrorMessage = $"触发 IO 联动部分失败 ({aggEx.InnerExceptions.Count} 个错误): {innerMessages}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发 IO 联动失败: SystemState={SystemState}", systemState);
            return new IoLinkageTriggerResult
            {
                Success = false,
                SystemState = systemState.ToString(),
                ErrorMessage = $"触发 IO 联动失败: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<IoPointStatus> GetIoPointStatusAsync(int bitNumber)
    {
        if (!IoEndpointValidator.IsValidEndpoint(bitNumber))
        {
            throw new ArgumentException(IoEndpointValidator.GetValidationError(bitNumber));
        }

        var state = await _ioLinkageDriver.ReadIoPointAsync(bitNumber);

        return new IoPointStatus
        {
            BitNumber = bitNumber,
            State = state
        };
    }

    /// <inheritdoc />
    public async Task<List<IoPointStatus>> GetBatchIoPointStatusAsync(IEnumerable<int> bitNumbers)
    {
        var results = new List<IoPointStatus>();

        foreach (var bitNumber in bitNumbers)
        {
            if (!IoEndpointValidator.IsValidEndpoint(bitNumber))
            {
                _logger.LogWarning("跳过无效 IO 端点: BitNumber={BitNumber}", bitNumber);
                continue;
            }

            var state = await _ioLinkageDriver.ReadIoPointAsync(bitNumber);
            results.Add(new IoPointStatus
            {
                BitNumber = bitNumber,
                State = state
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IoPointSetResult> SetIoPointAsync(int bitNumber, TriggerLevel level)
    {
        try
        {
            if (!IoEndpointValidator.IsValidEndpoint(bitNumber))
            {
                var errorMsg = IoEndpointValidator.GetValidationError(bitNumber);
                _logger.LogWarning("尝试设置无效 IO 端点: BitNumber={BitNumber}, Error={Error}", bitNumber, errorMsg);
                return new IoPointSetResult
                {
                    Success = false,
                    BitNumber = bitNumber,
                    Level = level.ToString(),
                    ErrorMessage = errorMsg
                };
            }

            var ioPoint = new IoLinkagePoint
            {
                BitNumber = bitNumber,
                Level = level
            };

            await _ioLinkageDriver.SetIoPointAsync(ioPoint);

            _logger.LogInformation(
                "IO 点设置成功: BitNumber={BitNumber}, Level={Level}",
                bitNumber,
                level);

            return new IoPointSetResult
            {
                Success = true,
                BitNumber = bitNumber,
                Level = level.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置 IO 点失败: BitNumber={BitNumber}", bitNumber);
            return new IoPointSetResult
            {
                Success = false,
                BitNumber = bitNumber,
                Level = level.ToString(),
                ErrorMessage = "设置 IO 点失败"
            };
        }
    }

    /// <inheritdoc />
    public async Task<BatchIoPointSetResult> SetBatchIoPointsAsync(IEnumerable<IoLinkagePoint> ioPoints)
    {
        try
        {
            var allPoints = ioPoints.ToList();

            // 验证所有 IO 点编号并过滤无效端点
            var validPoints = IoEndpointValidator.FilterAndLogInvalidEndpoints(allPoints, _logger).ToList();

            if (validPoints.Count == 0)
            {
                return new BatchIoPointSetResult
                {
                    Success = false,
                    TotalRequested = allPoints.Count,
                    ValidCount = 0,
                    SkippedCount = allPoints.Count,
                    ErrorMessage = "所有 IO 端点均无效，未执行任何操作"
                };
            }

            await _ioLinkageDriver.SetIoPointsAsync(validPoints);

            _logger.LogInformation(
                "批量设置 IO 点成功: TotalRequested={TotalRequested}, ValidCount={ValidCount}",
                allPoints.Count,
                validPoints.Count);

            return new BatchIoPointSetResult
            {
                Success = true,
                TotalRequested = allPoints.Count,
                ValidCount = validPoints.Count,
                SkippedCount = allPoints.Count - validPoints.Count,
                IoPoints = validPoints.Select(p => new IoPointInfo
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level.ToString()
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量设置 IO 点失败");
            return new BatchIoPointSetResult
            {
                Success = false,
                ErrorMessage = "批量设置 IO 点失败"
            };
        }
    }

    private IoLinkageConfiguration MapToConfiguration(UpdateIoLinkageConfigCommand command)
    {
        return new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = command.Enabled,
            ReadyStateIos = command.ReadyStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            RunningStateIos = command.RunningStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            StoppedStateIos = command.StoppedStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            EmergencyStopStateIos = command.EmergencyStopStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            UpstreamConnectionExceptionStateIos = command.UpstreamConnectionExceptionStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            DiverterExceptionStateIos = command.DiverterExceptionStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            PostPreStartWarningStateIos = command.PostPreStartWarningStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            WheelDiverterDisconnectedStateIos = command.WheelDiverterDisconnectedStateIos
                .Select(p => new IoLinkagePoint
                {
                    BitNumber = p.BitNumber,
                    Level = p.Level
                })
                .ToList(),
            CreatedAt = _systemClock.LocalNow,
            UpdatedAt = _systemClock.LocalNow
        };
    }

    private static IoLinkageOptions ConvertToOptions(IoLinkageConfiguration config)
    {
        return new IoLinkageOptions
        {
            Enabled = config.Enabled,
            ReadyStateIos = config.ReadyStateIos.ToList(),
            RunningStateIos = config.RunningStateIos.ToList(),
            StoppedStateIos = config.StoppedStateIos.ToList(),
            EmergencyStopStateIos = config.EmergencyStopStateIos.ToList(),
            UpstreamConnectionExceptionStateIos = config.UpstreamConnectionExceptionStateIos.ToList(),
            DiverterExceptionStateIos = config.DiverterExceptionStateIos.ToList(),
            PostPreStartWarningStateIos = config.PostPreStartWarningStateIos.ToList(),
            WheelDiverterDisconnectedStateIos = config.WheelDiverterDisconnectedStateIos.ToList()
        };
    }
}
