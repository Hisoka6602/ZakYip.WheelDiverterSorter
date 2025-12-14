using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware.Connectivity;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services.Workers;

/// <summary>
/// IO 联动硬件初始化后台服务
/// </summary>
/// <remarks>
/// 在系统启动时异步初始化 IO 联动硬件（雷赛 EMC / 西门子 S7 PLC）。
/// 该服务在应用启动后执行，不阻塞 Swagger 和 API 的初始化。
/// PR-SWAGGER-FIX: 新增此服务以解决硬件连接阻塞应用启动的问题。
/// </remarks>
public sealed class IoLinkageInitHostedService : IHostedService
{
    /// <summary>
    /// 硬件 Ping 检查超时时间（毫秒）
    /// </summary>
    private const int HardwarePingTimeoutMs = 2000;

    private readonly IEmcController? _emcController;
    private readonly S7Connection? _s7Connection;
    private readonly INetworkConnectivityChecker _connectivityChecker;
    private readonly DriverOptions _driverOptions;
    private readonly ISafeExecutionService _safeExecutor;
    private readonly ILogger<IoLinkageInitHostedService> _logger;

    public IoLinkageInitHostedService(
        INetworkConnectivityChecker connectivityChecker,
        DriverOptions driverOptions,
        ISafeExecutionService safeExecutor,
        ILogger<IoLinkageInitHostedService> logger,
        IEmcController? emcController = null,
        S7Connection? s7Connection = null)
    {
        _connectivityChecker = connectivityChecker ?? throw new ArgumentNullException(nameof(connectivityChecker));
        _driverOptions = driverOptions ?? throw new ArgumentNullException(nameof(driverOptions));
        _safeExecutor = safeExecutor ?? throw new ArgumentNullException(nameof(safeExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emcController = emcController;
        _s7Connection = s7Connection;
    }

    /// <summary>
    /// 启动时初始化 IO 联动硬件
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                _logger.LogInformation("========== IO 联动硬件初始化 ==========");

                // 初始化雷赛 EMC（如果已注册）
                if (_emcController != null)
                {
                    await InitializeLeadshineEmcAsync(cancellationToken);
                }
                else
                {
                    _logger.LogDebug("雷赛 EMC 控制器未注册，跳过初始化");
                }

                // 初始化西门子 S7 PLC（如果已注册）
                if (_s7Connection != null)
                {
                    await InitializeSiemensS7Async(cancellationToken);
                }
                else
                {
                    _logger.LogDebug("西门子 S7 PLC 未注册，跳过初始化");
                }

                _logger.LogInformation("========================================");
            },
            operationName: "IoLinkageHardwareInitialization",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 初始化雷赛 EMC 控制器
    /// </summary>
    private async Task InitializeLeadshineEmcAsync(CancellationToken cancellationToken)
    {
        try
        {
            var controllerIp = _driverOptions.Leadshine.ControllerIp;
            var isEthernetMode = !string.IsNullOrWhiteSpace(controllerIp);

            LogEmcInitializationStart(isEthernetMode, controllerIp);

            // 如果是以太网模式，先 Ping 检查连通性
            if (isEthernetMode)
            {
                var isReachable = await CheckEmcReachabilityAsync(controllerIp!, cancellationToken);
                if (!isReachable)
                {
                    return;
                }
            }

            // 初始化 EMC 控制器
            await PerformEmcInitializationAsync(controllerIp, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 初始化雷赛 EMC 控制器时发生异常");
        }
    }

    /// <summary>
    /// 记录 EMC 初始化开始日志
    /// </summary>
    private void LogEmcInitializationStart(bool isEthernetMode, string? controllerIp)
    {
        _logger.LogInformation(
            "正在初始化雷赛 EMC 控制器，卡号: {CardNo}, 端口: {PortNo}, 模式: {Mode}, IP: {IP}",
            _driverOptions.Leadshine.CardNo,
            _driverOptions.Leadshine.PortNo,
            isEthernetMode ? "以太网" : "PCI",
            controllerIp ?? "N/A");
    }

    /// <summary>
    /// 检查 EMC 控制器可达性
    /// </summary>
    /// <returns>如果可达返回 true，否则返回 false</returns>
    private async Task<bool> CheckEmcReachabilityAsync(string controllerIp, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ping 检查雷赛 EMC 控制器: {IP}", controllerIp);
        var pingResult = await _connectivityChecker.PingAsync(controllerIp, HardwarePingTimeoutMs, cancellationToken);

        if (!pingResult.IsReachable)
        {
            _logger.LogWarning(
                "⚠️ 雷赛 EMC 控制器不可达: {IP}，原因: {Error}。跳过初始化，IO 操作将返回失败。",
                controllerIp, pingResult.ErrorMessage);
            _logger.LogInformation(
                "提示: 请检查 EMC 控制器是否通电、网络连接是否正常、IP 地址配置是否正确。");
            return false;
        }

        _logger.LogInformation(
            "✅ 雷赛 EMC 控制器可达: {IP}，响应时间: {ResponseTime}ms",
            controllerIp, pingResult.ResponseTimeMs);
        return true;
    }

    /// <summary>
    /// 执行 EMC 控制器初始化并记录结果
    /// </summary>
    private async Task PerformEmcInitializationAsync(string? controllerIp, CancellationToken cancellationToken)
    {
        var initResult = await _emcController!.InitializeAsync(cancellationToken);

        if (initResult)
        {
            _logger.LogInformation(
                "✅ 雷赛 EMC 控制器初始化成功。CardNo: {CardNo}, PortNo: {PortNo}, IP: {IP}",
                _driverOptions.Leadshine.CardNo,
                _driverOptions.Leadshine.PortNo,
                controllerIp ?? "N/A (PCI Mode)");
        }
        else
        {
            _logger.LogWarning(
                "⚠️ 雷赛 EMC 控制器初始化失败。CardNo: {CardNo}, PortNo: {PortNo}, IP: {IP}。\n" +
                "可能原因：\n" +
                "1) 控制卡未连接或未通电\n" +
                "2) IP地址配置错误（以太网模式）\n" +
                "3) LTDMC.dll 未正确安装\n" +
                "4) 总线异常（错误码非 0）\n" +
                "EMC 控制器将处于不可用状态，所有 IO 操作将返回失败。",
                _driverOptions.Leadshine.CardNo,
                _driverOptions.Leadshine.PortNo,
                controllerIp ?? "N/A (PCI Mode)");
        }
    }

    /// <summary>
    /// 初始化西门子 S7 PLC
    /// </summary>
    private async Task InitializeSiemensS7Async(CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = _s7Connection!.IpAddress;

            LogS7InitializationStart(ipAddress);

            // Ping 检查连通性
            var isReachable = await CheckS7ReachabilityAsync(ipAddress, cancellationToken);
            if (!isReachable)
            {
                return;
            }

            // 尝试连接 S7 PLC
            await PerformS7ConnectionAsync(ipAddress, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 初始化西门子 S7 PLC 时发生异常");
        }
    }

    /// <summary>
    /// 记录 S7 初始化开始日志
    /// </summary>
    private void LogS7InitializationStart(string ipAddress)
    {
        _logger.LogInformation(
            "正在初始化西门子 S7 PLC: {IpAddress}",
            ipAddress);
    }

    /// <summary>
    /// 检查 S7 PLC 可达性
    /// </summary>
    /// <returns>如果可达返回 true，否则返回 false</returns>
    private async Task<bool> CheckS7ReachabilityAsync(string ipAddress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ping 检查西门子 S7 PLC: {IP}", ipAddress);
        var pingResult = await _connectivityChecker.PingAsync(ipAddress, HardwarePingTimeoutMs, cancellationToken);

        if (!pingResult.IsReachable)
        {
            _logger.LogWarning(
                "⚠️ 西门子 S7 PLC 不可达: {IP}，原因: {Error}。跳过连接，IO 操作将返回失败。",
                ipAddress, pingResult.ErrorMessage);
            _logger.LogInformation(
                "提示: 请检查 S7 PLC 是否通电、网络连接是否正常、IP 地址配置是否正确。");
            return false;
        }

        _logger.LogInformation(
            "✅ 西门子 S7 PLC 可达: {IP}，响应时间: {ResponseTime}ms",
            ipAddress, pingResult.ResponseTimeMs);
        return true;
    }

    /// <summary>
    /// 执行 S7 PLC 连接并记录结果
    /// </summary>
    private async Task PerformS7ConnectionAsync(string ipAddress, CancellationToken cancellationToken)
    {
        var connectResult = await _s7Connection!.ConnectAsync(cancellationToken);

        if (connectResult)
        {
            _logger.LogInformation(
                "✅ 西门子 S7 PLC 连接成功: {IpAddress}",
                ipAddress);
        }
        else
        {
            _logger.LogWarning(
                "⚠️ 西门子 S7 PLC 连接失败: {IpAddress}。\n" +
                "可能原因：\n" +
                "1) PLC 未启动或处于 STOP 模式\n" +
                "2) Rack/Slot 配置错误\n" +
                "3) CPU 类型配置错误\n" +
                "4) 网络防火墙阻止连接（端口 102）\n" +
                "S7 连接将处于断开状态，IO 操作将返回失败。",
                ipAddress);
        }
    }

    /// <summary>
    /// 停止服务（无操作）
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IoLinkageInit service停止");
        return Task.CompletedTask;
    }
}
