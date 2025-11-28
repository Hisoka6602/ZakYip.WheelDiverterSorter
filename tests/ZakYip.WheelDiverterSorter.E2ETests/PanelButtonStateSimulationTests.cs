using FluentAssertions;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// 面板按钮 IO 状态机 + 仿真场景验证测试
/// </summary>
public class PanelButtonStateSimulationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly ISystemRunStateService _stateService;
    private readonly SystemStateIoLinkageService _linkageService;
    private readonly SimulatedOutputPort _simulatedOutput;

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
        services.AddSingleton<ISystemRunStateService, DefaultSystemRunStateService>();
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();
        
        // 添加模拟 IO 端口
        var simulatedOutput = new SimulatedOutputPort();
        services.AddSingleton<IOutputPort>(simulatedOutput);
        services.AddSingleton<IIoLinkageExecutor, DefaultIoLinkageExecutor>();
        
        // 添加状态-IO 联动服务
        services.AddSingleton<SystemStateIoLinkageService>();

        _serviceProvider = services.BuildServiceProvider();
        _stateService = _serviceProvider.GetRequiredService<ISystemRunStateService>();
        _linkageService = _serviceProvider.GetRequiredService<SystemStateIoLinkageService>();
        _simulatedOutput = simulatedOutput;
    }

    [Fact]
    public void Scenario1_DefaultState_AndStartButton()
    {
        // 场景 1：默认状态与启动按钮
        _output.WriteLine("=== 场景 1：默认状态与启动按钮 ===");

        // 断言：初始状态为待机（就绪）
        _stateService.Current.Should().Be(SystemOperatingState.Standby);
        _output.WriteLine($"✓ 初始状态：{_stateService.Current}");

        // 模拟按下启动按钮
        var startResult = _linkageService.HandleStartAsync().GetAwaiter().GetResult();

        // 断言：状态切换成功
        startResult.IsSuccess.Should().BeTrue();
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine($"✓ 启动按钮按下，状态切换为：{_stateService.Current}");

        // 断言：跟随启动联动的 IO 已被写入（10, 11 设置为 HIGH）
        _simulatedOutput.GetWriteHistory().Should().Contain(w => w.BitIndex == 10 && w.Value == true);
        _simulatedOutput.GetWriteHistory().Should().Contain(w => w.BitIndex == 11 && w.Value == true);
        _output.WriteLine("✓ 跟随启动联动 IO (10, 11) 已写入 HIGH");

        // 断言：包裹创建应该成功
        var createResult = _stateService.ValidateParcelCreation();
        createResult.IsSuccess.Should().BeTrue();
        _output.WriteLine("✓ 当前状态允许创建包裹");
    }

    [Fact]
    public void Scenario2_StopButton()
    {
        // 场景 2：停止按钮
        _output.WriteLine("=== 场景 2：停止按钮 ===");

        // 先启动系统
        _linkageService.HandleStartAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine($"✓ 系统已启动：{_stateService.Current}");

        // 清空 IO 历史以便验证停止时的 IO 写入
        _simulatedOutput.ClearHistory();

        // 模拟按下停止按钮
        var stopResult = _linkageService.HandleStopAsync().GetAwaiter().GetResult();

        // 断言：状态切换成功
        stopResult.IsSuccess.Should().BeTrue();
        _stateService.Current.Should().Be(SystemOperatingState.Stopped);
        _output.WriteLine($"✓ 停止按钮按下，状态切换为：{_stateService.Current}");

        // 断言：跟随停止联动的 IO 已被写入（10, 11 设置为 LOW）
        _simulatedOutput.GetWriteHistory().Should().Contain(w => w.BitIndex == 10 && w.Value == false);
        _simulatedOutput.GetWriteHistory().Should().Contain(w => w.BitIndex == 11 && w.Value == false);
        _output.WriteLine("✓ 跟随停止联动 IO (10, 11) 已写入 LOW");

        // 断言：包裹创建应该被拒绝
        var createResult = _stateService.ValidateParcelCreation();
        createResult.IsSuccess.Should().BeFalse();
        createResult.ErrorMessage.Should().Contain("停止");
        _output.WriteLine($"✓ 包裹创建被拒绝：{createResult.ErrorMessage}");

        // 再次按下停止按钮（应该无效）
        _simulatedOutput.ClearHistory();
        var stopAgainResult = _linkageService.HandleStopAsync().GetAwaiter().GetResult();

        // 断言：状态保持不变
        stopAgainResult.IsSuccess.Should().BeFalse();
        _stateService.Current.Should().Be(SystemOperatingState.Stopped);
        _output.WriteLine("✓ 再次按停止按钮无效，状态保持 Stopped");

        // 断言：IO 不重复写入
        _simulatedOutput.GetWriteHistory().Should().BeEmpty();
        _output.WriteLine("✓ 跟随停止联动 IO 未重复写入");
    }

    [Fact]
    public void Scenario3_RunningState_RepeatStartButton()
    {
        // 场景 3：运行状态下重复启动按钮
        _output.WriteLine("=== 场景 3：运行状态下重复启动按钮 ===");

        // 先启动系统
        _linkageService.HandleStartAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine($"✓ 系统已启动：{_stateService.Current}");

        // 清空 IO 历史
        _simulatedOutput.ClearHistory();

        // 再次按下启动按钮
        var startAgainResult = _linkageService.HandleStartAsync().GetAwaiter().GetResult();

        // 断言：操作失败
        startAgainResult.IsSuccess.Should().BeFalse();
        startAgainResult.ErrorMessage.Should().Contain("已处于运行状态");
        _output.WriteLine($"✓ 重复启动被拒绝：{startAgainResult.ErrorMessage}");

        // 断言：状态保持运行
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine("✓ 状态保持 Running");

        // 断言：IO 不重复写入
        _simulatedOutput.GetWriteHistory().Should().BeEmpty();
        _output.WriteLine("✓ 跟随启动联动 IO 未重复写入");
    }

    [Fact]
    public void Scenario4_EmergencyStopAndFaultState()
    {
        // 场景 4：急停与故障状态
        _output.WriteLine("=== 场景 4：急停与故障状态 ===");

        // 先启动系统
        _linkageService.HandleStartAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine($"✓ 系统已启动：{_stateService.Current}");

        // 清空 IO 历史
        _simulatedOutput.ClearHistory();

        // 模拟按下急停按钮
        var emergencyStopResult = _linkageService.HandleEmergencyStopAsync().GetAwaiter().GetResult();

        // 断言：状态切换成功
        emergencyStopResult.IsSuccess.Should().BeTrue();
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine($"✓ 急停按钮按下，状态切换为：{_stateService.Current}");

        // 断言：跟随停止联动的 IO 已被写入
        _simulatedOutput.GetWriteHistory().Should().Contain(w => w.BitIndex == 10 && w.Value == false);
        _simulatedOutput.GetWriteHistory().Should().Contain(w => w.BitIndex == 11 && w.Value == false);
        _output.WriteLine("✓ 跟随停止联动 IO (10, 11) 已写入 LOW（停机状态）");

        // 断言：包裹创建应该被拒绝
        var createResult = _stateService.ValidateParcelCreation();
        createResult.IsSuccess.Should().BeFalse();
        createResult.ErrorMessage.Should().Contain("急停");
        _output.WriteLine($"✓ 包裹创建被拒绝：{createResult.ErrorMessage}");

        // 在急停状态下尝试按各种按钮
        _output.WriteLine("\n--- 测试急停状态下按钮无效 ---");

        // 尝试再次急停
        var emergencyAgainResult = _linkageService.HandleEmergencyStopAsync().GetAwaiter().GetResult();
        emergencyAgainResult.IsSuccess.Should().BeFalse();
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine($"✓ 再次急停无效：{emergencyAgainResult.ErrorMessage}");

        // 尝试启动
        var startResult = _linkageService.HandleStartAsync().GetAwaiter().GetResult();
        startResult.IsSuccess.Should().BeFalse();
        startResult.ErrorMessage.Should().Contain("急停");
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine($"✓ 启动按钮无效：{startResult.ErrorMessage}");

        // 尝试停止
        var stopResult = _linkageService.HandleStopAsync().GetAwaiter().GetResult();
        stopResult.IsSuccess.Should().BeFalse();
        stopResult.ErrorMessage.Should().Contain("急停");
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine($"✓ 停止按钮无效：{stopResult.ErrorMessage}");
    }

    [Fact]
    public void Scenario5_EmergencyResetAndReadyState()
    {
        // 场景 5：急停解除与就绪状态
        _output.WriteLine("=== 场景 5：急停解除与就绪状态 ===");

        // 先进入急停状态
        _linkageService.HandleStartAsync().Wait();
        _linkageService.HandleEmergencyStopAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine($"✓ 系统处于急停状态：{_stateService.Current}");

        // 模拟急停解除
        var resetResult = _linkageService.HandleEmergencyReset();

        // 断言：状态切换为待机（就绪）
        resetResult.IsSuccess.Should().BeTrue();
        _stateService.Current.Should().Be(SystemOperatingState.Standby);
        _output.WriteLine($"✓ 急停解除，状态切换为：{_stateService.Current}");

        // 断言：不自动创建包裹，不自动进入运行
        var createResult = _stateService.ValidateParcelCreation();
        createResult.IsSuccess.Should().BeFalse();
        _output.WriteLine("✓ 待机状态不允许创建包裹（需手动启动）");

        // 在待机状态下测试各按钮
        _output.WriteLine("\n--- 测试待机状态下按钮操作 ---");

        // 测试启动按钮（应该有效）
        _simulatedOutput.ClearHistory();
        var startResult = _linkageService.HandleStartAsync().GetAwaiter().GetResult();
        startResult.IsSuccess.Should().BeTrue();
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine("✓ 待机状态下启动按钮有效");

        // 恢复到待机状态继续测试
        // 先解除急停回到待机
        _linkageService.HandleStopAsync().Wait();
        _linkageService.HandleEmergencyStopAsync().Wait();
        _linkageService.HandleEmergencyReset();
        _stateService.Current.Should().Be(SystemOperatingState.Standby);

        // 测试停止按钮（待机状态下应无效）
        var stopResult = _linkageService.HandleStopAsync().GetAwaiter().GetResult();
        stopResult.IsSuccess.Should().BeFalse();
        _output.WriteLine($"✓ 待机状态下停止按钮无效：{stopResult.ErrorMessage}");

        // 测试急停按钮（应该有效）
        var emergencyResult = _linkageService.HandleEmergencyStopAsync().GetAwaiter().GetResult();
        emergencyResult.IsSuccess.Should().BeTrue();
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine("✓ 待机状态下急停按钮有效");
    }

    [Fact]
    public void Scenario6_CompleteWorkflow_AllStates()
    {
        // 场景 6：完整工作流测试（所有状态转换）
        _output.WriteLine("=== 场景 6：完整工作流测试 ===");

        // 1. 初始状态：Standby
        _stateService.Current.Should().Be(SystemOperatingState.Standby);
        _output.WriteLine($"1. 初始状态：{_stateService.Current}");

        // 2. Standby -> Running
        _linkageService.HandleStartAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine($"2. 启动后状态：{_stateService.Current}");

        // 3. Running -> Stopped
        _linkageService.HandleStopAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.Stopped);
        _output.WriteLine($"3. 停止后状态：{_stateService.Current}");

        // 4. Stopped -> Running
        _linkageService.HandleStartAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.Running);
        _output.WriteLine($"4. 重新启动后状态：{_stateService.Current}");

        // 5. Running -> EmergencyStopped
        _linkageService.HandleEmergencyStopAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine($"5. 急停后状态：{_stateService.Current}");

        // 6. EmergencyStopped -> Standby
        _linkageService.HandleEmergencyReset();
        _stateService.Current.Should().Be(SystemOperatingState.Standby);
        _output.WriteLine($"6. 急停解除后状态：{_stateService.Current}");

        // 7. Standby -> EmergencyStopped (测试任何状态都可急停)
        _linkageService.HandleEmergencyStopAsync().Wait();
        _stateService.Current.Should().Be(SystemOperatingState.EmergencyStopped);
        _output.WriteLine($"7. 待机状态急停后状态：{_stateService.Current}");

        _output.WriteLine("\n✓ 完整工作流测试通过，所有状态转换正常");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// 模拟数字输出端口（用于测试）
/// </summary>
internal class SimulatedOutputPort : IOutputPort
{
    private readonly List<(int BitIndex, bool Value, DateTime Timestamp)> _writeHistory = new();

    public Task<bool> WriteAsync(int bitIndex, bool value)
    {
        _writeHistory.Add((bitIndex, value, DateTime.UtcNow));
        return Task.FromResult(true);
    }

    public Task<bool> WriteBatchAsync(int startBit, bool[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            _writeHistory.Add((startBit + i, values[i], DateTime.UtcNow));
        }
        return Task.FromResult(true);
    }

    public List<(int BitIndex, bool Value, DateTime Timestamp)> GetWriteHistory()
    {
        return new List<(int, bool, DateTime)>(_writeHistory);
    }

    public void ClearHistory()
    {
        _writeHistory.Clear();
    }
}
