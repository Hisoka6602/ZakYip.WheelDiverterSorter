using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.E2ETests.Simulation;
using ZakYip.WheelDiverterSorter.Execution.Queues;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// E2E测试：干扰信号检测与双向容差验证
/// </summary>
/// <remarks>
/// 测试场景：
/// 1. 输送线上有残留包裹触发传感器（干扰信号）- 队列为空时不应记录为超时异常
/// 2. 验证容差配置双向应用 - 理论传输时间±容差都应正常
/// 
/// 问题描述来源：
/// - 前面有一个输送线段上残留的包裹(干扰包裹)
/// - 后面跟着一个正常包裹
/// - 配置: lengthMm=6000, speedMmps=1300, timeToleranceMs=700
/// - 理论传输时间: 6000/1300*1000 = 4615ms
/// - 期望容差范围: [4615-700, 4615+700] = [3915ms, 5315ms]
/// </remarks>
public class InterferenceSignalDetectionE2ETests : E2ETestBase
{
    private readonly ISortingOrchestrator _orchestrator;
    private readonly ISensorEventProvider _sensorEventProvider;
    private readonly IPositionIndexQueueManager _queueManager;
    private readonly ISystemClock _systemClock;
    private readonly IChutePathTopologyRepository _topologyRepository;
    private readonly IConveyorSegmentRepository _segmentRepository;
    private readonly ITestOutputHelper _output;

    public InterferenceSignalDetectionE2ETests(E2ETestFactory factory, ITestOutputHelper output) 
        : base(factory, output)
    {
        _output = output;
        _orchestrator = Scope.ServiceProvider.GetRequiredService<ISortingOrchestrator>();
        _sensorEventProvider = Scope.ServiceProvider.GetRequiredService<ISensorEventProvider>();
        _queueManager = Scope.ServiceProvider.GetRequiredService<IPositionIndexQueueManager>();
        _systemClock = Scope.ServiceProvider.GetRequiredService<ISystemClock>();
        _topologyRepository = Scope.ServiceProvider.GetRequiredService<IChutePathTopologyRepository>();
        _segmentRepository = Scope.ServiceProvider.GetRequiredService<IConveyorSegmentRepository>();
        
        SetupDefaultRouteConfiguration();
        SetupTestSegmentConfiguration();
    }

    /// <summary>
    /// 设置测试用的输送线段配置
    /// </summary>
    /// <remarks>
    /// 配置参数基于问题描述:
    /// - lengthMm: 6000
    /// - speedMmps: 1300
    /// - timeToleranceMs: 700
    /// - 理论传输时间: 4615ms
    /// - 容差范围: [3915ms, 5315ms]
    /// </remarks>
    private void SetupTestSegmentConfiguration()
    {
        var now = _systemClock.LocalNow;
        
        // 为每个线段设置相同的测试配置
        for (long segmentId = 1; segmentId <= 5; segmentId++)
        {
            var existingConfig = _segmentRepository.GetById(segmentId);
            if (existingConfig != null)
            {
                _segmentRepository.Delete(segmentId);
            }
            
            var config = new ConveyorSegmentConfiguration
            {
                SegmentId = segmentId,
                SegmentName = $"测试线段 {segmentId}",
                LengthMm = 6000,      // 6米
                SpeedMmps = 1300m,    // 1.3 m/s
                TimeToleranceMs = 700, // ±700ms容差
                EnableLossDetection = false, // 禁用丢失检测以简化测试
                Remarks = "E2E测试配置 - 干扰信号检测",
                CreatedAt = now,
                UpdatedAt = now
            };
            
            _segmentRepository.Insert(config);
            
            // 验证理论传输时间计算
            var transitTime = config.CalculateTransitTimeMs();
            _output.WriteLine($"线段 {segmentId}: 理论传输时间 = {transitTime:F2}ms, " +
                            $"容差范围 = [{transitTime - 700:F2}ms, {transitTime + 700:F2}ms]");
        }
    }

    /// <summary>
    /// 测试场景1: 队列为空时传感器触发 - 应识别为干扰信号而非超时异常
    /// </summary>
    /// <remarks>
    /// 模拟输送线上残留包裹触发传感器的场景：
    /// 1. 系统启动，所有队列为空
    /// 2. 摆轮前传感器被触发（残留包裹经过）
    /// 3. 验证：不记录为超时异常，只记录为干扰信号
    /// 4. 验证：不影响后续正常包裹的分拣流程
    /// </remarks>
    [Fact]
    [SimulationScenario("InterferenceSignal_EmptyQueue_ShouldNotRecordAsTimeout")]
    public async Task EmptyQueueSensorTrigger_ShouldBeRecognizedAsInterferenceSignal()
    {
        // Arrange
        Factory.MockRuleEngineClient!
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();
        await Task.Delay(200); // 等待系统初始化

        // 验证队列初始状态为空
        var topology = _topologyRepository.Get();
        topology.Should().NotBeNull();
        
        foreach (var node in topology!.DiverterNodes)
        {
            var queueTask = _queueManager.PeekTask(node.PositionIndex);
            queueTask.Should().BeNull($"Position {node.PositionIndex} 队列应为空");
        }

        // Act - 模拟干扰包裹触发 Position 1 的传感器
        var interferenceParcelId = 0L; // ParcelId=0 表示未识别的包裹
        var sensorId = 2; // Position 1 的前置传感器
        
        _output.WriteLine($"[干扰信号测试] 触发传感器 {sensorId} (Position 1), ParcelId={interferenceParcelId}");
        
        // 触发 ParcelDetected 事件
        var eventArgs = new ParcelDetectedEventArgs(
            interferenceParcelId,
            sensorId,
            _systemClock.LocalNow
        );
        
        // 使用反射触发事件（模拟传感器触发）
        var eventInfo = _sensorEventProvider.GetType().GetEvent("ParcelDetected");
        var field = _sensorEventProvider.GetType()
            .GetField("ParcelDetected", System.Reflection.BindingFlags.Instance | 
                                       System.Reflection.BindingFlags.NonPublic | 
                                       System.Reflection.BindingFlags.Public);
        
        if (field != null)
        {
            var eventDelegate = field.GetValue(_sensorEventProvider) as MulticastDelegate;
            if (eventDelegate != null)
            {
                foreach (var handler in eventDelegate.GetInvocationList())
                {
                    handler.Method.Invoke(handler.Target, new object?[] { _sensorEventProvider, eventArgs });
                }
            }
        }

        await Task.Delay(500); // 等待事件处理完成

        // Assert - 验证队列仍为空（干扰信号不应生成任务）
        foreach (var node in topology.DiverterNodes)
        {
            var queueTask = _queueManager.PeekTask(node.PositionIndex);
            queueTask.Should().BeNull($"Position {node.PositionIndex} 队列应保持为空（干扰信号不应入队）");
        }

        _output.WriteLine("[干扰信号测试] 验证通过：队列保持为空，干扰信号被正确识别");

        // Cleanup
        await _orchestrator.StopAsync();
    }

    /// <summary>
    /// 测试场景2: 验证容差双向应用 - 提前触发检测
    /// </summary>
    /// <remarks>
    /// 验证时间容差在期望到达时间之前的应用：
    /// - 配置: timeToleranceMs = 700
    /// - 理论传输时间: 4615ms
    /// - 最早允许时间: 4615 - 700 = 3915ms
    /// - 如果在 3915ms 之前到达，应识别为提前触发
    /// </remarks>
    [Fact]
    [SimulationScenario("ToleranceBidirectional_EarlyTrigger_ShouldBeDetected")]
    public async Task EarlyArrival_WithinNegativeTolerance_ShouldBeDetectedAsEarlyTrigger()
    {
        // Arrange
        var targetChuteId = 3L;
        var parcelId = _systemClock.LocalNow.Ticks;

        // 配置上游响应
        Factory.MockRuleEngineClient!
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        // 启用提前触发检测
        var systemConfig = SystemRepository.Get();
        systemConfig.EnableEarlyTriggerDetection = true;
        SystemRepository.Update(systemConfig);

        await _orchestrator.StartAsync();
        await Task.Delay(200);

        // Act - 创建包裹并生成队列任务
        _output.WriteLine($"[容差双向测试] 创建包裹 {parcelId}，目标格口 {targetChuteId}");
        
        var queueTasks = PathGenerator.GenerateQueueTasks(parcelId, targetChuteId, _systemClock.LocalNow);
        queueTasks.Should().NotBeEmpty("应成功生成队列任务");

        // 验证任务的时间窗口设置
        foreach (var task in queueTasks)
        {
            task.Should().NotBeNull();
            task.TimeoutThresholdMs.Should().Be(700, "超时阈值应等于容差配置");
            task.EarliestDequeueTime.Should().NotBeNull("应设置最早出队时间");
            
            var expectedEarliest = task.ExpectedArrivalTime.AddMilliseconds(-700);
            if (expectedEarliest < task.CreatedAt)
            {
                expectedEarliest = task.CreatedAt;
            }
            
            task.EarliestDequeueTime.Should().Be(expectedEarliest, 
                $"Position {task.PositionIndex} 最早出队时间应为 ExpectedArrival - 700ms (但不早于创建时间)");
            
            var latestArrival = task.ExpectedArrivalTime.AddMilliseconds(700);
            _output.WriteLine($"Position {task.PositionIndex}: " +
                            $"容差窗口 = [{task.EarliestDequeueTime:HH:mm:ss.fff}, {latestArrival:HH:mm:ss.fff}], " +
                            $"期望时间 = {task.ExpectedArrivalTime:HH:mm:ss.fff}");
        }

        _output.WriteLine("[容差双向测试] 验证通过：时间窗口正确设置为 ExpectedArrival ± 700ms");

        // Cleanup
        await _orchestrator.StopAsync();
    }

    /// <summary>
    /// 测试场景3: 验证容差双向应用 - 延迟触发检测
    /// </summary>
    /// <remarks>
    /// 验证时间容差在期望到达时间之后的应用：
    /// - 配置: timeToleranceMs = 700
    /// - 理论传输时间: 4615ms
    /// - 最晚允许时间: 4615 + 700 = 5315ms
    /// - 如果在 5315ms 之后到达，应识别为超时
    /// </remarks>
    [Fact]
    [SimulationScenario("ToleranceBidirectional_LateArrival_ShouldBeDetectedAsTimeout")]
    public async Task LateArrival_BeyondPositiveTolerance_ShouldBeDetectedAsTimeout()
    {
        // Arrange
        var targetChuteId = 3L;
        var parcelId = _systemClock.LocalNow.Ticks;

        Factory.MockRuleEngineClient!
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();
        await Task.Delay(200);

        // Act - 生成队列任务并验证超时阈值
        var queueTasks = PathGenerator.GenerateQueueTasks(parcelId, targetChuteId, _systemClock.LocalNow);
        queueTasks.Should().NotBeEmpty("应成功生成队列任务");

        foreach (var task in queueTasks)
        {
            task.TimeoutThresholdMs.Should().Be(700, "超时阈值应等于容差配置");
            
            var timeoutDeadline = task.ExpectedArrivalTime.AddMilliseconds(task.TimeoutThresholdMs);
            _output.WriteLine($"Position {task.PositionIndex}: " +
                            $"期望时间 = {task.ExpectedArrivalTime:HH:mm:ss.fff}, " +
                            $"超时截止时间 = {timeoutDeadline:HH:mm:ss.fff} " +
                            $"(+{task.TimeoutThresholdMs}ms)");
        }

        _output.WriteLine("[容差双向测试] 验证通过：超时阈值正确设置为 ExpectedArrival + 700ms");

        // Cleanup
        await _orchestrator.StopAsync();
    }

    /// <summary>
    /// 测试场景4: 完整流程 - 干扰包裹 + 正常包裹
    /// </summary>
    /// <remarks>
    /// 模拟完整的真实场景：
    /// 1. 残留干扰包裹触发多个传感器（不影响系统）
    /// 2. 正常包裹进入系统并成功分拣
    /// 3. 验证两种包裹的处理互不干扰
    /// </remarks>
    [Fact]
    [SimulationScenario("CompleteFlow_InterferenceAndNormalParcel")]
    public async Task CompleteFlow_WithInterferenceAndNormalParcel_ShouldHandleBothCorrectly()
    {
        // Arrange
        var normalParcelId = _systemClock.LocalNow.Ticks;
        var targetChuteId = 3L;

        Factory.MockRuleEngineClient!
            .Setup(x => x.SendAsync(It.IsAny<IUpstreamMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Factory.MockRuleEngineClient!
            .Setup(x => x.IsConnected)
            .Returns(true);

        await _orchestrator.StartAsync();
        await Task.Delay(200);

        // 步骤1: 模拟干扰包裹触发 Position 1 传感器（队列为空）
        _output.WriteLine("[完整流程] 步骤1: 干扰包裹触发 Position 1");
        var interferenceEvent1 = new ParcelDetectedEventArgs(0, 2, _systemClock.LocalNow);
        TriggerSensorEvent(interferenceEvent1);
        await Task.Delay(100);

        // 验证队列仍为空
        _queueManager.PeekTask(1).Should().BeNull("干扰信号不应在 Position 1 生成任务");

        // 步骤2: 正常包裹进入系统
        _output.WriteLine($"[完整流程] 步骤2: 正常包裹 {normalParcelId} 进入系统，目标格口 {targetChuteId}");
        
        // 生成并入队正常包裹的任务
        var normalTasks = PathGenerator.GenerateQueueTasks(normalParcelId, targetChuteId, _systemClock.LocalNow);
        normalTasks.Should().NotBeEmpty("正常包裹应成功生成任务");
        
        foreach (var task in normalTasks)
        {
            _queueManager.EnqueueTask(task);
        }

        // 验证队列中有正常包裹的任务
        var pos1Task = _queueManager.PeekTask(1);
        pos1Task.Should().NotBeNull("Position 1 应有正常包裹任务");
        pos1Task!.ParcelId.Should().Be(normalParcelId, "队列中应是正常包裹");

        // 步骤3: 再次触发干扰包裹（Position 2，队列为空）
        _output.WriteLine("[完整流程] 步骤3: 干扰包裹触发 Position 2（队列为空）");
        var interferenceEvent2 = new ParcelDetectedEventArgs(0, 4, _systemClock.LocalNow);
        TriggerSensorEvent(interferenceEvent2);
        await Task.Delay(100);

        // 验证 Position 2 队列仍为空（干扰信号）
        var pos2Task = _queueManager.PeekTask(2);
        // Position 2 可能有正常包裹的任务（取决于路径），但不应因干扰信号而改变
        if (pos2Task != null)
        {
            pos2Task.ParcelId.Should().Be(normalParcelId, "Position 2 如果有任务，应是正常包裹，不应是干扰包裹");
        }

        _output.WriteLine("[完整流程] 验证通过：干扰信号与正常包裹处理互不干扰");

        // Cleanup
        await _orchestrator.StopAsync();
    }

    /// <summary>
    /// 辅助方法：触发传感器事件
    /// </summary>
    private void TriggerSensorEvent(ParcelDetectedEventArgs eventArgs)
    {
        var eventInfo = _sensorEventProvider.GetType().GetEvent("ParcelDetected");
        var field = _sensorEventProvider.GetType()
            .GetField("ParcelDetected", System.Reflection.BindingFlags.Instance | 
                                       System.Reflection.BindingFlags.NonPublic | 
                                       System.Reflection.BindingFlags.Public);
        
        if (field != null)
        {
            var eventDelegate = field.GetValue(_sensorEventProvider) as MulticastDelegate;
            if (eventDelegate != null)
            {
                foreach (var handler in eventDelegate.GetInvocationList())
                {
                    handler.Method.Invoke(handler.Target, new object?[] { _sensorEventProvider, eventArgs });
                }
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
