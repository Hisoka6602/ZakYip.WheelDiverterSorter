using Microsoft.Extensions.Logging;
using csLTDMC;
using Polly;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛EMC控制器实现
/// 提供对雷赛LTDMC系列控制卡的基本操作
/// </summary>
/// <remarks>
/// 基于 ZakYip.Singulation 项目的 LeadshineLtdmcBusAdapter 实现。
/// 支持以太网模式（需要 ControllerIp）和本地 PCI 模式（ControllerIp 为空）。
/// </remarks>
public class LeadshineEmcController : IEmcController
{
    private readonly ILogger<LeadshineEmcController> _logger;
    private readonly ushort _cardNo;
    private readonly ushort _portNo;
    private readonly string? _controllerIp;
    private bool _isAvailable;
    private bool _isInitialized;

    /// <inheritdoc/>
    public ushort CardNo => _cardNo;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">EMC卡号</param>
    /// <param name="portNo">端口号（CAN/EtherCAT端口编号）</param>
    /// <param name="controllerIp">控制器IP地址（以太网模式），null则使用本地PCI模式</param>
    public LeadshineEmcController(
        ILogger<LeadshineEmcController> logger,
        ushort cardNo = 0,
        ushort portNo = 0,
        string? controllerIp = null)
    {
        _logger = logger;
        _cardNo = cardNo;
        _portNo = portNo;
        _controllerIp = string.IsNullOrWhiteSpace(controllerIp) ? null : controllerIp;
        _isAvailable = false;
        _isInitialized = false;
    }

    /// <inheritdoc/>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 重试策略：0ms → 300ms → 1s → 2s（参考 ZakYip.Singulation 项目）
        var delays = new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
        };

        var policy = Policy
            .HandleResult<bool>(r => r == false)
            .Or<Exception>()
            .WaitAndRetryAsync(
                delays,
                onRetryAsync: (outcome, delay, retryAttempt, _) =>
                {
                    var reason = outcome.Exception?.Message ?? "初始化失败";
                    _logger.LogWarning(
                        "EMC初始化重试 #{RetryAttempt}，等待 {Delay}ms，原因: {Reason}",
                        retryAttempt, delay.TotalMilliseconds, reason);
                    return Task.CompletedTask;
                });

        return await policy.ExecuteAsync(async () =>
        {
            try
            {
                var isEthernet = _controllerIp != null;
                var methodName = isEthernet ? "dmc_board_init_eth" : "dmc_board_init";
                
                _logger.LogInformation(
                    "正在初始化EMC，卡号: {CardNo}, 端口: {PortNo}, 模式: {Mode}, IP: {IP}",
                    _cardNo,
                    _portNo,
                    isEthernet ? "以太网" : "PCI",
                    _controllerIp ?? "N/A");

                // 根据是否配置IP选择初始化方式
                short result;
                if (isEthernet)
                {
                    // 以太网模式：使用 dmc_board_init_eth
                    result = LTDMC.dmc_board_init_eth(_cardNo, _controllerIp!);
                    if (result != 0)
                    {
                        _logger.LogError(
                            "【EMC初始化失败】方法: {Method}, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}, IP: {IP}",
                            methodName, result, _cardNo, _controllerIp);
                        return false;
                    }
                }
                else
                {
                    // PCI 模式：使用 dmc_board_init
                    result = LTDMC.dmc_board_init();
                    if (result != 0)
                    {
                        _logger.LogError(
                            "【EMC初始化失败】方法: {Method}, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}",
                            methodName, result, _cardNo);
                        return false;
                    }
                }

                _logger.LogInformation(
                    "【EMC初始化成功】方法: {Method}, 返回值: {Result}, 卡号: {CardNo}",
                    methodName, result, _cardNo);

                // 检查总线状态
                ushort errcode = 0;
                LTDMC.nmc_get_errcode(_cardNo, _portNo, ref errcode);
                if (errcode != 0)
                {
                    _logger.LogWarning(
                        "【EMC总线异常检测】方法: nmc_get_errcode, 错误码: {ErrorCode}（预期: 0），卡号: {CardNo}, 端口: {PortNo}，尝试软复位并重新连接",
                        errcode, _cardNo, _portNo);

                    // 执行软复位
                    LTDMC.dmc_soft_reset(_cardNo);
                    
                    // 关闭连接
                    LTDMC.dmc_board_close();
                    
                    // 等待复位完成
                    await Task.Delay(500, cancellationToken);

                    // 重新初始化连接（软复位后必须重新调用 dmc_board_init_eth/dmc_board_init）
                    if (isEthernet)
                    {
                        result = LTDMC.dmc_board_init_eth(_cardNo, _controllerIp!);
                        if (result != 0)
                        {
                            _logger.LogError(
                                "【EMC软复位后重新初始化失败】方法: dmc_board_init_eth, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}, IP: {IP}",
                                result, _cardNo, _controllerIp);
                            return false;
                        }
                    }
                    else
                    {
                        result = LTDMC.dmc_board_init();
                        if (result != 0)
                        {
                            _logger.LogError(
                                "【EMC软复位后重新初始化失败】方法: dmc_board_init, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}",
                                result, _cardNo);
                            return false;
                        }
                    }
                    
                    _logger.LogInformation(
                        "【EMC软复位后重新初始化成功】方法: {Method}, 卡号: {CardNo}",
                        methodName, _cardNo);

                    // 再次检查总线状态
                    LTDMC.nmc_get_errcode(_cardNo, _portNo, ref errcode);
                    if (errcode != 0)
                    {
                        _logger.LogError(
                            "【EMC总线异常未恢复】方法: nmc_get_errcode, 错误码: {ErrorCode}（预期: 0），卡号: {CardNo}, 端口: {PortNo}",
                            errcode, _cardNo, _portNo);
                        return false;
                    }
                    _logger.LogInformation(
                        "【EMC总线异常已恢复】错误码: {ErrorCode}, 卡号: {CardNo}, 端口: {PortNo}",
                        errcode, _cardNo, _portNo);
                }

                _isInitialized = true;
                _isAvailable = true;
                _logger.LogInformation("EMC初始化完成，卡号: {CardNo}, 端口: {PortNo}", _cardNo, _portNo);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化EMC时发生异常");
                return false;
            }
        });
    }

    /// <inheritdoc/>
    public async Task<bool> ColdResetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始执行EMC冷重置，卡号: {CardNo}", _cardNo);

            _isAvailable = false;

            // 执行冷复位
            LTDMC.dmc_cool_reset(_cardNo);

            // 关闭控制卡
            short result = LTDMC.dmc_board_close();
            if (result != 0)
            {
                _logger.LogWarning("关闭EMC失败，错误码: {ErrorCode}", result);
            }

            // 等待硬件完全断开（冷复位需要较长等待时间）
            _logger.LogInformation("等待控制器冷复位完成（10秒）...");
            await Task.Delay(10000, cancellationToken);

            // 重新初始化
            var initResult = await InitializeAsync(cancellationToken);
            if (!initResult)
            {
                _logger.LogError("EMC冷重置后初始化失败，卡号: {CardNo}", _cardNo);
                return false;
            }

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
                _logger.LogError(
                    "【EMC热复位失败】方法: dmc_soft_reset, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}",
                    result, _cardNo);
                return false;
            }
            _logger.LogInformation(
                "【EMC热复位成功】方法: dmc_soft_reset, 返回值: {Result}, 卡号: {CardNo}",
                result, _cardNo);

            // 关闭当前连接
            LTDMC.dmc_board_close();

            // 等待控制器复位（官方建议 300~1500ms）
            await Task.Delay(800, cancellationToken);

            // 重新初始化
            if (_controllerIp != null)
            {
                result = LTDMC.dmc_board_init_eth(_cardNo, _controllerIp);
                if (result != 0)
                {
                    _logger.LogError(
                        "【EMC热复位后初始化失败】方法: dmc_board_init_eth, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}",
                        result, _cardNo);
                    return false;
                }
            }
            else
            {
                result = LTDMC.dmc_board_init();
                if (result != 0)
                {
                    _logger.LogError(
                        "【EMC热复位后初始化失败】方法: dmc_board_init, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}",
                        result, _cardNo);
                    return false;
                }
            }
            _logger.LogInformation(
                "【EMC热复位后初始化成功】卡号: {CardNo}",
                _cardNo);

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

    /// <summary>
    /// 获取当前错误码
    /// </summary>
    /// <returns>错误码，0 表示正常</returns>
    public int GetErrorCode()
    {
        try
        {
            ushort errcode = 0;
            LTDMC.nmc_get_errcode(_cardNo, _portNo, ref errcode);
            return errcode;
        }
        catch
        {
            return -999;
        }
    }

    /// <summary>
    /// 获取总线上的轴数量
    /// </summary>
    /// <returns>轴数量</returns>
    public int GetAxisCount()
    {
        try
        {
            ushort total = 0;
            var ret = LTDMC.nmc_get_total_slaves(_cardNo, _portNo, ref total);
            if (ret != 0)
            {
                _logger.LogError(
                    "【获取轴数失败】方法: nmc_get_total_slaves, 返回值: {ErrorCode}（预期: 0），卡号: {CardNo}, 端口: {PortNo}",
                    ret, _cardNo, _portNo);
                return 0;
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }
}
