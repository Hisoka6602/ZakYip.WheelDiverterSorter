using ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Modi;

/// <summary>
/// 莫迪摆轮模拟设备（用于测试和开发）
/// </summary>
/// <remarks>
/// 提供莫迪摆轮设备的模拟实现，不需要实际硬件连接。
/// 用于系统测试、开发调试和演示。
/// </remarks>
public sealed class ModiSimulatedDevice : IWheelDiverterDriver
{
    private readonly ILogger<ModiSimulatedDevice> _logger;
    private readonly ModiDeviceEntry _config;
    private string _currentStatus = "已就绪（仿真）";
    private ModiControlCommand _lastCommand = ModiControlCommand.Stop;

    /// <inheritdoc/>
    public string DiverterId => _config.DiverterId.ToString();

    /// <summary>
    /// 初始化莫迪模拟设备
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="logger">日志记录器</param>
    public ModiSimulatedDevice(
        ModiDeviceEntry config,
        ILogger<ModiSimulatedDevice> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation(
            "已初始化莫迪模拟设备 {DiverterId}，设备编号={DeviceId}（仿真模式）",
            DiverterId, _config.DeviceId);
    }

    /// <inheritdoc/>
    public Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default)
    {
        _lastCommand = ModiControlCommand.TurnLeft;
        _currentStatus = "左转（仿真）";
        _logger.LogDebug("摆轮 {DiverterId} 执行左转（仿真）", DiverterId);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> TurnRightAsync(CancellationToken cancellationToken = default)
    {
        _lastCommand = ModiControlCommand.TurnRight;
        _currentStatus = "右转（仿真）";
        _logger.LogDebug("摆轮 {DiverterId} 执行右转（仿真）", DiverterId);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> PassThroughAsync(CancellationToken cancellationToken = default)
    {
        _lastCommand = ModiControlCommand.ReturnCenter;
        _currentStatus = "直通（仿真）";
        _logger.LogDebug("摆轮 {DiverterId} 执行直通（仿真）", DiverterId);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        _lastCommand = ModiControlCommand.Stop;
        _currentStatus = "已停止（仿真）";
        _logger.LogDebug("摆轮 {DiverterId} 执行停止（仿真）", DiverterId);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<string> GetStatusAsync()
    {
        return Task.FromResult(_currentStatus);
    }

    /// <summary>
    /// 获取最后执行的命令（用于测试验证）
    /// </summary>
    public ModiControlCommand LastCommand => _lastCommand;
}
