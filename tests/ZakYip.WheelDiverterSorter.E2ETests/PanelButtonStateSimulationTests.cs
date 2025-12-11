using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Host.StateMachine;
using ZakYip.WheelDiverterSorter.Drivers;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 面板按钮 IO 状态机 + 仿真场景验证测试
/// </summary>
/// <remarks>
/// TODO: 这些测试需要重构 - SystemStateIoLinkageService已被移除，因为它在生产代码中未使用。
/// 现在状态管理由 SystemStateWheelDiverterCoordinator 和 PanelButtonMonitorWorker 处理。
/// 需要重写测试以直接使用 ISystemStateManager 和 IIoLinkageConfigService。
/// </remarks>
public class PanelButtonStateSimulationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly ISystemStateManager _stateService;

    public PanelButtonStateSimulationTests(ITestOutputHelper output)
    {
        _output = output;

        // 构建依赖注入容器
        var services = new ServiceCollection();

        // 添加日志
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // 添加配置
        services.Configure<SystemConfiguration>(config =>
        {
            config.IoLinkage = new IoLinkageOptions
            {
                Enabled = true,
                RunningStateIos = new List<IoLinkagePoint>
                {
                    new() { BitNumber = 10, Level = TriggerLevel.ActiveHigh },
                    new() { BitNumber = 11, Level = TriggerLevel.ActiveHigh },
                },
                StoppedStateIos = new List<IoLinkagePoint>
                {
                    new() { BitNumber = 10, Level = TriggerLevel.ActiveLow },
                    new() { BitNumber = 11, Level = TriggerLevel.ActiveLow },
                }
            };
        });

        // 添加核心服务
        services.AddSingleton<ISystemStateManager, SystemStateManager>();
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();
        services.AddSingleton<IIoLinkageExecutor, DefaultIoLinkageExecutor>();

        _serviceProvider = services.BuildServiceProvider();
        _stateService = _serviceProvider.GetRequiredService<ISystemStateManager>();
    }

    [Fact(Skip = "需要重构 - SystemStateIoLinkageService已被移除，需要使用 ISystemStateManager 和 PanelButtonMonitorWorker")]
    public void Scenario1_DefaultState_AndStartButton()
    {
        // TODO: 重构此测试以使用 ISystemStateManager 直接测试状态转换
        // 并使用 IIoLinkageConfigService 测试 IO 联动
    }

    [Fact(Skip = "需要重构 - SystemStateIoLinkageService已被移除")]
    public void Scenario2_StartAndStop()
    {
        // TODO: 重构此测试以使用 ISystemStateManager 直接测试
    }

    [Fact(Skip = "需要重构 - SystemStateIoLinkageService已被移除")]
    public void Scenario3_EmergencyStop()
    {
        // TODO: 重构此测试以使用 ISystemStateManager 直接测试
    }

    [Fact(Skip = "需要重构 - SystemStateIoLinkageService已被移除")]
    public void Scenario4_EmergencyStopAndReset()
    {
        // TODO: 重构此测试以使用 ISystemStateManager 直接测试
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
