using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Events;


using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// 路径失败检测和恢复集成测试
/// </summary>
public class PathFailureIntegrationTests
{
    [Fact]
    public async Task PathExecution_WhenFails_HandlerReceivesFailureEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var targetChuteId = 101;
        var exceptionChuteId = 999;
        
        var pathGeneratorMock = new Mock<ISwitchingPathGenerator>();
        var path = CreateTestPath(targetChuteId, exceptionChuteId);
        var backupPath = CreateTestPath(exceptionChuteId, exceptionChuteId);
        
        pathGeneratorMock.Setup(g => g.GeneratePath(targetChuteId)).Returns(path);
        pathGeneratorMock.Setup(g => g.GeneratePath(exceptionChuteId)).Returns(backupPath);
        
        var failureHandler = new PathFailureHandler(
            pathGeneratorMock.Object,
            NullLogger<PathFailureHandler>.Instance);
        
        // 创建一个会失败的执行器
        var executor = new FailingMockExecutor();
        
        PathExecutionFailedEventArgs? capturedFailureEvent = null;
        PathSwitchedEventArgs? capturedSwitchEvent = null;
        
        failureHandler.PathExecutionFailed += (sender, args) => capturedFailureEvent = args;
        failureHandler.PathSwitched += (sender, args) => capturedSwitchEvent = args;
        
        // Act
        var result = await executor.ExecuteAsync(path);
        
        // 模拟失败处理
        if (!result.IsSuccess)
        {
            failureHandler.HandlePathFailure(
                parcelId,
                path,
                result.FailureReason ?? "未知错误",
                result.FailedSegment);
        }
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(exceptionChuteId, result.ActualChuteId);
        Assert.NotNull(capturedFailureEvent);
        Assert.Equal(parcelId, capturedFailureEvent.Value.ParcelId);
        Assert.Equal(path, capturedFailureEvent.Value.OriginalPath);
        Assert.NotNull(capturedSwitchEvent);
        Assert.Equal(backupPath, capturedSwitchEvent.Value.BackupPath);
    }
    
    [Fact]
    public async Task PathExecution_WhenSegmentFails_HandlerReceivesSegmentFailureEvent()
    {
        // Arrange
        var parcelId = 12345L;
        var targetChuteId = 101;
        var exceptionChuteId = 999;
        
        var pathGeneratorMock = new Mock<ISwitchingPathGenerator>();
        var path = CreateTestPath(targetChuteId, exceptionChuteId);
        var backupPath = CreateTestPath(exceptionChuteId, exceptionChuteId);
        
        pathGeneratorMock.Setup(g => g.GeneratePath(targetChuteId)).Returns(path);
        pathGeneratorMock.Setup(g => g.GeneratePath(exceptionChuteId)).Returns(backupPath);
        
        var failureHandler = new PathFailureHandler(
            pathGeneratorMock.Object,
            NullLogger<PathFailureHandler>.Instance);
        
        var executor = new FailingMockExecutor();
        
        PathSegmentExecutionFailedEventArgs? capturedSegmentFailureEvent = null;
        failureHandler.SegmentExecutionFailed += (sender, args) => capturedSegmentFailureEvent = args;
        
        // Act
        var result = await executor.ExecuteAsync(path);
        
        // 模拟段失败处理
        if (!result.IsSuccess && result.FailedSegment != null)
        {
            failureHandler.HandleSegmentFailure(
                parcelId,
                path,
                result.FailedSegment,
                result.FailureReason ?? "未知错误");
        }
        
        // Assert
        Assert.NotNull(capturedSegmentFailureEvent);
        Assert.Equal(parcelId, capturedSegmentFailureEvent.Value.ParcelId);
        Assert.Equal(result.FailedSegment, capturedSegmentFailureEvent.Value.FailedSegment);
        Assert.Equal(targetChuteId, capturedSegmentFailureEvent.Value.OriginalTargetChuteId);
    }
    
    [Fact]
    public async Task PathExecution_WhenSucceeds_NoFailureEventsRaised()
    {
        // Arrange
        var parcelId = 12345L;
        var targetChuteId = 101;
        var exceptionChuteId = 999;
        
        var pathGeneratorMock = new Mock<ISwitchingPathGenerator>();
        var path = CreateTestPath(targetChuteId, exceptionChuteId);
        
        pathGeneratorMock.Setup(g => g.GeneratePath(targetChuteId)).Returns(path);
        
        var failureHandler = new PathFailureHandler(
            pathGeneratorMock.Object,
            NullLogger<PathFailureHandler>.Instance);
        
        var executor = new MockSwitchingPathExecutor();
        
        var failureEventRaised = false;
        failureHandler.PathExecutionFailed += (sender, args) => failureEventRaised = true;
        
        // Act
        var result = await executor.ExecuteAsync(path);
        
        // 成功时不调用失败处理
        if (!result.IsSuccess)
        {
            failureHandler.HandlePathFailure(parcelId, path, result.FailureReason ?? "未知错误");
        }
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(targetChuteId, result.ActualChuteId);
        Assert.False(failureEventRaised);
    }
    
    private static SwitchingPath CreateTestPath(int targetChuteId, int fallbackChuteId)
    {
        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            FallbackChuteId = fallbackChuteId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Segments = new List<SwitchingPathSegment>
            {
                new SwitchingPathSegment
                {
                    SequenceNumber = 1,
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    TtlMilliseconds = 5000
                },
                new SwitchingPathSegment
                {
                    SequenceNumber = 2,
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Left,
                    TtlMilliseconds = 5000
                }
            }.AsReadOnly()
        };
    }
    
    /// <summary>
    /// 模拟失败的执行器，用于测试失败场景
    /// </summary>
    private class FailingMockExecutor : ISwitchingPathExecutor
    {
        public Task<PathExecutionResult> ExecuteAsync(
            SwitchingPath path,
            CancellationToken cancellationToken = default)
        {
            // 模拟第一段执行失败
            var failedSegment = path.Segments.First();
            
            return Task.FromResult(new PathExecutionResult
            {
                IsSuccess = false,
                ActualChuteId = path.FallbackChuteId,
                FailureReason = "模拟段执行失败",
                FailedSegment = failedSegment,
                FailureTime = DateTimeOffset.UtcNow
            });
        }
    }
}
