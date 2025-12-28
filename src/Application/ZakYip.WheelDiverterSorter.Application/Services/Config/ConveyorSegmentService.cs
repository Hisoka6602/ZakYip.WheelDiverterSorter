using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 输送线段配置服务实现
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public class ConveyorSegmentService : IConveyorSegmentService
{
    private static readonly object AllSegmentsCacheKey = new();
    
    private readonly IConveyorSegmentRepository _repository;
    private readonly ISlidingConfigCache _configCache;
    private readonly ILogger<ConveyorSegmentService> _logger;
    private readonly ISystemClock _systemClock;
    
    // PR-PERF01: 字典缓存用于 O(1) 查找，避免热路径中的 O(n) LINQ 扫描
    private Dictionary<long, ConveyorSegmentConfiguration>? _segmentDictCache;

    public ConveyorSegmentService(
        IConveyorSegmentRepository repository,
        ISlidingConfigCache configCache,
        ILogger<ConveyorSegmentService> logger,
        ISystemClock systemClock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    }

    public IEnumerable<ConveyorSegmentConfiguration> GetAllSegments()
    {
        return _configCache.GetOrAdd(AllSegmentsCacheKey, () => _repository.GetAll().ToList());
    }

    public ConveyorSegmentConfiguration? GetSegmentById(long segmentId)
    {
        // PR-PERF01: 使用字典缓存实现 O(1) 查找（从 O(n) LINQ 优化）
        // 热路径性能优化：每次传感器触发时调用，从 ~100-200μs 降至 ~1-5μs
        if (_segmentDictCache == null)
        {
            _segmentDictCache = GetAllSegments().ToDictionary(s => s.SegmentId);
        }
        
        return _segmentDictCache.TryGetValue(segmentId, out var segment) ? segment : null;
    }

    public async Task<ConveyorSegmentOperationResult> CreateSegmentAsync(ConveyorSegmentConfiguration config)
    {
        await Task.Yield();
        try
        {
            // 验证配置
            var (isValid, errorMessage) = ValidateSegment(config);
            if (!isValid)
            {
                _logger.LogWarning("输送线段配置验证失败: SegmentId={SegmentId}, Error={Error}", 
                    config.SegmentId, errorMessage);
                return ConveyorSegmentOperationResult.Failure(errorMessage!);
            }

            // 检查ID是否已存在
            if (_repository.Exists(config.SegmentId))
            {
                var error = $"输送线段ID {config.SegmentId} 已存在";
                _logger.LogWarning("输送线段创建失败: {Error}", error);
                return ConveyorSegmentOperationResult.Failure(error);
            }

            // 插入配置
            _repository.Insert(config);

            // 热更新：立即刷新缓存
            RefreshCache();

            // 获取插入后的配置
            var insertedConfig = _repository.GetById(config.SegmentId);
            if (insertedConfig == null)
            {
                var error = "创建成功但无法获取创建后的配置";
                _logger.LogError("输送线段创建异常: SegmentId={SegmentId}", config.SegmentId);
                return ConveyorSegmentOperationResult.Failure(error);
            }

            _logger.LogInformation("输送线段配置已创建: SegmentId={SegmentId}, Name={Name}, Length={Length}mm, Speed={Speed}mm/s",
                insertedConfig.SegmentId, insertedConfig.SegmentName, insertedConfig.LengthMm, insertedConfig.SpeedMmps);

            return ConveyorSegmentOperationResult.Success(insertedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建输送线段配置失败: SegmentId={SegmentId}", config.SegmentId);
            return ConveyorSegmentOperationResult.Failure($"创建失败: {ex.Message}");
        }
    }

    public async Task<ConveyorSegmentOperationResult> UpdateSegmentAsync(ConveyorSegmentConfiguration config)
    {
        await Task.Yield();
        try
        {
            // 验证配置
            var (isValid, errorMessage) = ValidateSegment(config);
            if (!isValid)
            {
                _logger.LogWarning("输送线段配置验证失败: SegmentId={SegmentId}, Error={Error}", 
                    config.SegmentId, errorMessage);
                return ConveyorSegmentOperationResult.Failure(errorMessage!);
            }

            // 检查是否存在
            if (!_repository.Exists(config.SegmentId))
            {
                var error = $"输送线段ID {config.SegmentId} 不存在";
                _logger.LogWarning("输送线段更新失败: {Error}", error);
                return ConveyorSegmentOperationResult.Failure(error);
            }

            // 更新配置
            var updated = _repository.Update(config);
            if (!updated)
            {
                var error = "更新失败";
                _logger.LogError("输送线段更新失败: SegmentId={SegmentId}", config.SegmentId);
                return ConveyorSegmentOperationResult.Failure(error);
            }

            // 热更新：立即刷新缓存
            RefreshCache();

            // 获取更新后的配置
            var updatedConfig = _repository.GetById(config.SegmentId);
            if (updatedConfig == null)
            {
                var error = "更新成功但无法获取更新后的配置";
                _logger.LogError("输送线段更新异常: SegmentId={SegmentId}", config.SegmentId);
                return ConveyorSegmentOperationResult.Failure(error);
            }

            _logger.LogInformation("输送线段配置已更新: SegmentId={SegmentId}, Name={Name}, Length={Length}mm, Speed={Speed}mm/s",
                updatedConfig.SegmentId, updatedConfig.SegmentName, updatedConfig.LengthMm, updatedConfig.SpeedMmps);

            return ConveyorSegmentOperationResult.Success(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新输送线段配置失败: SegmentId={SegmentId}", config.SegmentId);
            return ConveyorSegmentOperationResult.Failure($"更新失败: {ex.Message}");
        }
    }

    public async Task<ConveyorSegmentOperationResult> DeleteSegmentAsync(long segmentId)
    {
        await Task.Yield();
        try
        {
            // 检查是否存在
            var existing = _repository.GetById(segmentId);
            if (existing == null)
            {
                var error = $"输送线段ID {segmentId} 不存在";
                _logger.LogWarning("输送线段删除失败: {Error}", error);
                return ConveyorSegmentOperationResult.Failure(error);
            }

            // 删除配置
            var deleted = _repository.Delete(segmentId);
            if (!deleted)
            {
                var error = "删除失败";
                _logger.LogError("输送线段删除失败: SegmentId={SegmentId}", segmentId);
                return ConveyorSegmentOperationResult.Failure(error);
            }

            // 热更新：立即刷新缓存
            RefreshCache();

            _logger.LogInformation("输送线段配置已删除: SegmentId={SegmentId}", segmentId);

            return ConveyorSegmentOperationResult.Success(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除输送线段配置失败: SegmentId={SegmentId}", segmentId);
            return ConveyorSegmentOperationResult.Failure($"删除失败: {ex.Message}");
        }
    }

    public async Task<ConveyorSegmentBatchResult> CreateSegmentsBatchAsync(IEnumerable<ConveyorSegmentConfiguration> configs)
    {
        await Task.Yield();
        var configsList = configs.ToList();
        var errors = new List<string>();
        var successCount = 0;
        var failureCount = 0;

        try
        {
            foreach (var config in configsList)
            {
                // 验证配置
                var (isValid, errorMessage) = ValidateSegment(config);
                if (!isValid)
                {
                    errors.Add($"SegmentId {config.SegmentId}: {errorMessage}");
                    failureCount++;
                    continue;
                }

                // 检查ID是否已存在
                if (_repository.Exists(config.SegmentId))
                {
                    errors.Add($"SegmentId {config.SegmentId}: ID已存在");
                    failureCount++;
                    continue;
                }

                successCount++;
            }

            if (successCount == 0)
            {
                _logger.LogWarning("批量创建输送线段配置失败: 所有配置验证失败");
                return new ConveyorSegmentBatchResult
                {
                    SuccessCount = 0,
                    FailureCount = failureCount,
                    Errors = errors
                };
            }

            // 批量插入
            var validConfigs = configsList.Where(c => !_repository.Exists(c.SegmentId)).ToList();
            _repository.InsertBatch(validConfigs);

            // 热更新：立即刷新缓存
            RefreshCache();

            _logger.LogInformation("批量创建输送线段配置成功: 成功={Success}, 失败={Failure}", 
                successCount, failureCount);

            return new ConveyorSegmentBatchResult
            {
                SuccessCount = successCount,
                FailureCount = failureCount,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量创建输送线段配置失败");
            errors.Add($"批量创建失败: {ex.Message}");
            return new ConveyorSegmentBatchResult
            {
                SuccessCount = 0,
                FailureCount = configsList.Count,
                Errors = errors
            };
        }
    }

    public (bool IsValid, string? ErrorMessage) ValidateSegment(ConveyorSegmentConfiguration config)
    {
        if (config == null)
        {
            return (false, "配置不能为空");
        }

        if (config.SegmentId <= 0)
        {
            return (false, "线段ID必须大于0");
        }

        if (config.LengthMm <= 0)
        {
            return (false, "线段长度必须大于0");
        }

        if (config.SpeedMmps <= 0)
        {
            return (false, "线速必须大于0");
        }

        if (config.TimeToleranceMs < 0)
        {
            return (false, "时间容差不能为负数");
        }

        // 验证计算的传输时间是否合理（> 0 且 < 1小时）
        var transitTime = config.CalculateTransitTimeMs();
        if (transitTime <= 0 || transitTime > 3600000)
        {
            return (false, $"计算的传输时间不合理: {transitTime}ms (应在0-3600000ms之间)");
        }

        return (true, null);
    }

    public ConveyorSegmentConfiguration GetDefaultTemplate(long segmentId)
    {
        return ConveyorSegmentConfiguration.GetDefault(segmentId, _systemClock.LocalNow);
    }

    /// <summary>
    /// 刷新缓存
    /// </summary>
    private void RefreshCache()
    {
        var allSegments = _repository.GetAll().ToList();
        _configCache.Set(AllSegmentsCacheKey, allSegments);
        
        // PR-PERF01: 同时清空字典缓存，下次查询时重建
        _segmentDictCache = null;
    }
}
