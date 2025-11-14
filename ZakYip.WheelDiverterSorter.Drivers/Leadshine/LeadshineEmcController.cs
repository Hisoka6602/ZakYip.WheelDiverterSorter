using Microsoft.Extensions.Logging;
using csLTDMC;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Leadshine;

/// <summary>
/// 雷赛EMC控制器实现
/// 提供对雷赛LTDMC系列控制卡的基本操作
/// </summary>
public class LeadshineEmcController : IEmcController
{
    private readonly ILogger<LeadshineEmcController> _logger;
    private readonly ushort _cardNo;
    private bool _isAvailable;
    private bool _isInitialized;

    /// <inheritdoc/>
    public ushort CardNo => _cardNo;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">EMC卡号</param>
    public LeadshineEmcController(
        ILogger<LeadshineEmcController> logger,
        ushort cardNo = 0)
    {
        _logger = logger;
        _cardNo = cardNo;
        _isAvailable = false;
        _isInitialized = false;
    }

    /// <inheritdoc/>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("正在初始化EMC，卡号: {CardNo}", _cardNo);

            // 打开控制卡
            short result = LTDMC.dmc_board_init();
            if (result != 0)
            {
                _logger.LogError("初始化EMC失败，错误码: {ErrorCode}", result);
                return false;
            }

            // 复位控制卡
            result = LTDMC.dmc_soft_reset(_cardNo);
            if (result != 0)
            {
                _logger.LogWarning("软复位EMC失败，错误码: {ErrorCode}，继续初始化", result);
            }

            await Task.Delay(100, cancellationToken);

            _isInitialized = true;
            _isAvailable = true;
            _logger.LogInformation("EMC初始化成功，卡号: {CardNo}", _cardNo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化EMC时发生异常");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ColdResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始执行EMC冷重置，卡号: {CardNo}", _cardNo);

            _isAvailable = false;

            // 关闭控制卡
            short result = LTDMC.dmc_board_close();
            if (result != 0)
            {
                _logger.LogWarning("关闭EMC失败，错误码: {ErrorCode}", result);
            }

            // 等待硬件完全断开
            await Task.Delay(500, cancellationToken);

            // 重新打开控制卡
            result = LTDMC.dmc_board_init();
            if (result != 0)
            {
                _logger.LogError("重新打开EMC失败，错误码: {ErrorCode}", result);
                return false;
            }

            // 复位控制卡
            result = LTDMC.dmc_soft_reset(_cardNo);
            if (result != 0)
            {
                _logger.LogWarning("软复位EMC失败，错误码: {ErrorCode}", result);
            }

            await Task.Delay(100, cancellationToken);

            _isAvailable = true;
            _logger.LogInformation("EMC冷重置完成，卡号: {CardNo}", _cardNo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行EMC冷重置时发生异常");
            _isAvailable = false;
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HotResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始执行EMC热重置，卡号: {CardNo}", _cardNo);

            _isAvailable = false;

            // 软复位控制卡
            short result = LTDMC.dmc_soft_reset(_cardNo);
            if (result != 0)
            {
                _logger.LogError("软复位EMC失败，错误码: {ErrorCode}", result);
                return false;
            }

            await Task.Delay(100, cancellationToken);

            _isAvailable = true;
            _logger.LogInformation("EMC热重置完成，卡号: {CardNo}", _cardNo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行EMC热重置时发生异常");
            _isAvailable = false;
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("暂停使用EMC，卡号: {CardNo}", _cardNo);
            _isAvailable = false;

            // 停止所有输出
            // 注意：这里简化处理，实际应用中可能需要记录当前状态并优雅停止
            for (ushort i = 0; i < 32; i++)
            {
                LTDMC.dmc_write_outbit(_cardNo, i, 0);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停使用EMC时发生异常");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("恢复使用EMC，卡号: {CardNo}", _cardNo);

            if (!_isInitialized)
            {
                // 如果未初始化，先初始化
                return await InitializeAsync(cancellationToken);
            }

            _isAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复使用EMC时发生异常");
            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsAvailable()
    {
        return _isAvailable && _isInitialized;
    }
}
