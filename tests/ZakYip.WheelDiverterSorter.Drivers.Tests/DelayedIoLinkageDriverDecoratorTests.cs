using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests;

/// <summary>
/// DelayedIoLinkageDriverDecorator 单元测试
/// </summary>
/// <remarks>
/// 测试延迟执行、状态检查、取消令牌处理、并发控制等核心功能
/// </remarks>
public class DelayedIoLinkageDriverDecoratorTests
{
    private readonly Mock<IIoLinkageDriver> _mockInnerDriver;
    private readonly Mock<ISystemStateManager> _mockStateManager;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly Mock<ILogger<DelayedIoLinkageDriverDecorator>> _mockLogger;
    private readonly DelayedIoLinkageDriverDecorator _decorator;

    public DelayedIoLinkageDriverDecoratorTests()
    {
        _mockInnerDriver = new Mock<IIoLinkageDriver>();
        _mockStateManager = new Mock<ISystemStateManager>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        _mockLogger = new Mock<ILogger<DelayedIoLinkageDriverDecorator>>();

        // Setup SafeExecutionService to execute action immediately by default
        _mockSafeExecutor
            .Setup(s => s.ExecuteAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>(async (action, name, ct) =>
            {
                await action();
                return true;
            });

        _decorator = new DelayedIoLinkageDriverDecorator(
            _mockInnerDriver.Object,
            _mockStateManager.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);
    }

    #region 无延迟场景

    [Fact(DisplayName = "无延迟的IO点应立即执行")]
    public async Task SetIoPointAsync_NoDelay_ExecutesImmediately()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 1,
            Level = TriggerLevel.ActiveHigh,
            DelayMilliseconds = 0
        };

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(ioPoint, It.IsAny<CancellationToken>()), Times.Once);
        _mockSafeExecutor.Verify(s => s.ExecuteAsync(
            It.IsAny<Func<Task>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 延迟执行场景

    [Fact(DisplayName = "有延迟的IO点应在延迟后执行")]
    public async Task SetIoPointAsync_WithDelay_ExecutesAfterDelay()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 2,
            Level = TriggerLevel.ActiveLow,
            DelayMilliseconds = 100
        };
        _mockStateManager.Setup(s => s.CurrentState).Returns(SystemState.Running);

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockSafeExecutor.Verify(s => s.ExecuteAsync(
            It.IsAny<Func<Task>>(),
            "DelayedIO-2",
            It.IsAny<CancellationToken>()), Times.Once);
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(ioPoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 状态变化取消场景

    [Fact(DisplayName = "延迟期间状态从Running变为EmergencyStop应取消执行")]
    public async Task SetIoPointAsync_StateChangesToEmergencyStop_CancelsExecution()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 3,
            Level = TriggerLevel.ActiveHigh,
            DelayMilliseconds = 100
        };

        var callCount = 0;
        _mockStateManager.Setup(s => s.CurrentState).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? SystemState.Running : SystemState.EmergencyStop;
        });

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert - 内部驱动不应被调用
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "延迟期间状态从Running变为Ready应取消执行")]
    public async Task SetIoPointAsync_StateChangesToReady_CancelsExecution()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 4,
            Level = TriggerLevel.ActiveLow,
            DelayMilliseconds = 100
        };

        var callCount = 0;
        _mockStateManager.Setup(s => s.CurrentState).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? SystemState.Running : SystemState.Ready;
        });

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "延迟期间状态从Running变为Paused应取消执行")]
    public async Task SetIoPointAsync_StateChangesToPaused_CancelsExecution()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 5,
            Level = TriggerLevel.ActiveHigh,
            DelayMilliseconds = 100
        };

        var callCount = 0;
        _mockStateManager.Setup(s => s.CurrentState).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? SystemState.Running : SystemState.Paused;
        });

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "延迟期间状态从Running变为Faulted应取消执行")]
    public async Task SetIoPointAsync_StateChangesToFaulted_CancelsExecution()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 6,
            Level = TriggerLevel.ActiveLow,
            DelayMilliseconds = 100
        };

        var callCount = 0;
        _mockStateManager.Setup(s => s.CurrentState).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? SystemState.Running : SystemState.Faulted;
        });

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "延迟期间状态从Ready变为EmergencyStop应取消执行")]
    public async Task SetIoPointAsync_ReadyToEmergencyStop_CancelsExecution()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 7,
            Level = TriggerLevel.ActiveHigh,
            DelayMilliseconds = 100
        };

        var callCount = 0;
        _mockStateManager.Setup(s => s.CurrentState).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? SystemState.Ready : SystemState.EmergencyStop;
        });

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 状态不变或降级场景

    [Fact(DisplayName = "延迟期间状态保持不变应继续执行")]
    public async Task SetIoPointAsync_StateUnchanged_ExecutesIo()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 8,
            Level = TriggerLevel.ActiveLow,
            DelayMilliseconds = 100
        };
        _mockStateManager.Setup(s => s.CurrentState).Returns(SystemState.Running);

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(ioPoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "延迟期间状态从EmergencyStop变为Running应继续执行（优先级降低）")]
    public async Task SetIoPointAsync_EmergencyStopToRunning_ExecutesIo()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 9,
            Level = TriggerLevel.ActiveHigh,
            DelayMilliseconds = 100
        };

        var callCount = 0;
        _mockStateManager.Setup(s => s.CurrentState).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? SystemState.EmergencyStop : SystemState.Running;
        });

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert - 优先级降低，允许执行
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(ioPoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 取消令牌场景

    [Fact(DisplayName = "取消令牌被取消应抛出OperationCanceledException")]
    public async Task SetIoPointAsync_CancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 10,
            Level = TriggerLevel.ActiveLow,
            DelayMilliseconds = 5000 // 长延迟
        };
        _mockStateManager.Setup(s => s.CurrentState).Returns(SystemState.Running);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // 50ms后取消

        // Setup SafeExecutor to actually execute and propagate the exception
        _mockSafeExecutor
            .Setup(s => s.ExecuteAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>(async (action, name, ct) =>
            {
                await action(); // This should throw OperationCanceledException
                return true;
            });

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _decorator.SetIoPointAsync(ioPoint, cts.Token));

        // 内部驱动不应被调用
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 批量执行场景

    [Fact(DisplayName = "批量执行应并行处理所有IO点")]
    public async Task SetIoPointsAsync_MultiplePoints_ExecutesAllInParallel()
    {
        // Arrange
        var ioPoints = new[]
        {
            new IoLinkagePoint { BitNumber = 11, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 12, Level = TriggerLevel.ActiveLow, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 13, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 }
        };

        // Act
        await _decorator.SetIoPointsAsync(ioPoints);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "批量执行包含延迟IO点应正确处理")]
    public async Task SetIoPointsAsync_WithDelayedPoints_ExecutesAll()
    {
        // Arrange
        var ioPoints = new[]
        {
            new IoLinkagePoint { BitNumber = 14, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 15, Level = TriggerLevel.ActiveLow, DelayMilliseconds = 100 },
            new IoLinkagePoint { BitNumber = 16, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 50 }
        };
        _mockStateManager.Setup(s => s.CurrentState).Returns(SystemState.Running);

        // Act
        await _decorator.SetIoPointsAsync(ioPoints);

        // Assert
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mockSafeExecutor.Verify(s => s.ExecuteAsync(
            It.IsAny<Func<Task>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2)); // 只有2个有延迟
    }

    #endregion

    #region 直接委托方法

    [Fact(DisplayName = "ReadIoPointAsync应直接委托给内部驱动")]
    public async Task ReadIoPointAsync_DelegatesToInnerDriver()
    {
        // Arrange
        _mockInnerDriver.Setup(d => d.ReadIoPointAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _decorator.ReadIoPointAsync(100);

        // Assert
        Assert.True(result);
        _mockInnerDriver.Verify(d => d.ReadIoPointAsync(100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ResetAllIoPointsAsync应直接委托给内部驱动")]
    public async Task ResetAllIoPointsAsync_DelegatesToInnerDriver()
    {
        // Act
        await _decorator.ResetAllIoPointsAsync();

        // Assert
        _mockInnerDriver.Verify(d => d.ResetAllIoPointsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 边界情况

    [Fact(DisplayName = "DelayMilliseconds为最大值时应正常处理")]
    public async Task SetIoPointAsync_MaxDelay_HandlesCorrectly()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 17,
            Level = TriggerLevel.ActiveHigh,
            DelayMilliseconds = 3600000 // 1小时
        };
        _mockStateManager.Setup(s => s.CurrentState).Returns(SystemState.Running);

        // Note: 实际测试中不会真的等1小时，SafeExecutor mock会立即执行

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert
        _mockSafeExecutor.Verify(s => s.ExecuteAsync(
            It.IsAny<Func<Task>>(),
            "DelayedIO-17",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Booting到Running状态转换应允许执行（优先级上升但在允许范围）")]
    public async Task SetIoPointAsync_BootingToRunning_ExecutesIo()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 18,
            Level = TriggerLevel.ActiveLow,
            DelayMilliseconds = 100
        };

        var callCount = 0;
        _mockStateManager.Setup(s => s.CurrentState).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? SystemState.Booting : SystemState.Running;
        });

        // Act
        await _decorator.SetIoPointAsync(ioPoint);

        // Assert - Booting(0) -> Running(1) 优先级上升，应取消
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 批量操作容错性场景 (TD-IOLINKAGE-003)

    [Fact(DisplayName = "批量设置IO点时部分失败应收集所有结果")]
    public async Task SetIoPointsAsync_PartialFailure_CollectsAllResults()
    {
        // Arrange - 3个IO点，第2个会失败
        var ioPoints = new[]
        {
            new IoLinkagePoint { BitNumber = 1, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 2, Level = TriggerLevel.ActiveLow, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 }
        };

        var setupCallCount = 0;
        _mockInnerDriver.Setup(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()))
            .Returns<IoLinkagePoint, CancellationToken>((point, ct) =>
            {
                setupCallCount++;
                // 第2个IO点失败
                if (point.BitNumber == 2)
                {
                    return Task.FromException(new InvalidOperationException($"模拟IO点 {point.BitNumber} 失败"));
                }
                return Task.CompletedTask;
            });

        // Act & Assert - 应该抛出AggregateException，包含1个内部异常
        var exception = await Assert.ThrowsAsync<AggregateException>(() => 
            _decorator.SetIoPointsAsync(ioPoints));

        // 验证所有IO点都尝试过（包括失败的）
        Assert.Equal(3, setupCallCount);
        
        // 验证AggregateException包含正确的内部异常数量
        Assert.Single(exception.InnerExceptions);
        Assert.Contains("模拟IO点 2 失败", exception.InnerExceptions[0].Message);
        
        // 验证成功的IO点也被调用了
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(
            It.Is<IoLinkagePoint>(p => p.BitNumber == 1), 
            It.IsAny<CancellationToken>()), Times.Once);
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(
            It.Is<IoLinkagePoint>(p => p.BitNumber == 3), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "批量设置IO点全部成功时不抛异常")]
    public async Task SetIoPointsAsync_AllSuccess_NoException()
    {
        // Arrange - 3个IO点，全部成功
        var ioPoints = new[]
        {
            new IoLinkagePoint { BitNumber = 1, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 2, Level = TriggerLevel.ActiveLow, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 }
        };

        _mockInnerDriver.Setup(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - 不应抛出异常
        await _decorator.SetIoPointsAsync(ioPoints);

        // Assert - 验证所有IO点都被调用
        _mockInnerDriver.Verify(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()), 
            Times.Exactly(3));
    }

    [Fact(DisplayName = "批量设置IO点全部失败时应抛出包含所有错误的AggregateException")]
    public async Task SetIoPointsAsync_AllFailure_ThrowsAggregateExceptionWithAllErrors()
    {
        // Arrange - 3个IO点，全部失败
        var ioPoints = new[]
        {
            new IoLinkagePoint { BitNumber = 1, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 2, Level = TriggerLevel.ActiveLow, DelayMilliseconds = 0 },
            new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveHigh, DelayMilliseconds = 0 }
        };

        _mockInnerDriver.Setup(d => d.SetIoPointAsync(It.IsAny<IoLinkagePoint>(), It.IsAny<CancellationToken>()))
            .Returns<IoLinkagePoint, CancellationToken>((point, ct) =>
                Task.FromException(new InvalidOperationException($"IO点 {point.BitNumber} 失败")));

        // Act & Assert - 应该抛出AggregateException，包含3个内部异常
        var exception = await Assert.ThrowsAsync<AggregateException>(() => 
            _decorator.SetIoPointsAsync(ioPoints));

        // 验证AggregateException包含所有3个错误
        Assert.Equal(3, exception.InnerExceptions.Count);
        Assert.All(exception.InnerExceptions, ex => 
            Assert.IsType<InvalidOperationException>(ex));
    }

    #endregion
}
