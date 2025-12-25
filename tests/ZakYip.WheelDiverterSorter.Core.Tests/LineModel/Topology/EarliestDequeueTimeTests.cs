using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel.Topology;

/// <summary>
/// 测试 EarliestDequeueTime 字段的计算和应用
/// </summary>
public class EarliestDequeueTimeTests
{
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<IChutePathTopologyRepository> _mockTopologyRepository;
    private readonly Mock<IConveyorSegmentRepository> _mockSegmentRepository;
    private readonly Mock<ILogger<DefaultSwitchingPathGenerator>> _mockLogger;

    public EarliestDequeueTimeTests()
    {
        _mockClock = new Mock<ISystemClock>();
        _mockTopologyRepository = new Mock<IChutePathTopologyRepository>();
        _mockSegmentRepository = new Mock<IConveyorSegmentRepository>();
        _mockLogger = new Mock<ILogger<DefaultSwitchingPathGenerator>>();
        
        // 使用固定时间确保测试可重复
        _mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2025, 12, 25, 10, 0, 0));
    }

    [Fact]
    public void GenerateQueueTasks_ShouldCalculateEarliestDequeueTime_NotEarlierThanCreatedAt()
    {
        // Arrange
        var parcelId = 1001L;
        var targetChuteId = 1L;
        var createdAt = _mockClock.Object.LocalNow;
        
        var topology = new ChutePathTopologyConfig
        {
            TopologyId = "test-topology",
            TopologyName = "Test Topology",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNode>
            {
                new DiverterPathNode
                {
                    DiverterId = 1,
                    SegmentId = 1,
                    PositionIndex = 1,
                    FrontSensorId = 2,
                    LeftChuteIds = new List<long> { 1 },
                    RightChuteIds = new List<long> { 2 }
                }
            },
            ExceptionChuteId = 999
        };
        
        var segment = new ConveyorSegmentConfiguration
        {
            SegmentId = 1,
            SegmentName = "Segment 1",
            LengthMm = 3000,
            SpeedMmps = 1000,
            TimeToleranceMs = 2000
        };
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSegmentRepository.Setup(r => r.GetById(1)).Returns(segment);
        
        var generator = new DefaultSwitchingPathGenerator(
            _mockTopologyRepository.Object,
            _mockClock.Object,
            _mockSegmentRepository.Object,
            _mockLogger.Object);
        
        // Act
        var tasks = generator.GenerateQueueTasks(parcelId, targetChuteId, createdAt);
        
        // Assert
        Assert.NotEmpty(tasks);
        var task = tasks[0];
        
        Assert.NotNull(task.EarliestDequeueTime);
        
        // EarliestDequeueTime = Max(CreatedAt, ExpectedArrivalTime - TimeoutThresholdMs)
        var expectedEarliest = task.ExpectedArrivalTime.AddMilliseconds(-task.TimeoutThresholdMs);
        if (expectedEarliest < createdAt)
        {
            // Should be clamped to CreatedAt
            Assert.Equal(createdAt, task.EarliestDequeueTime.Value);
        }
        else
        {
            Assert.Equal(expectedEarliest, task.EarliestDequeueTime.Value);
        }
        
        // Earliest dequeue time should never be earlier than created time
        Assert.True(task.EarliestDequeueTime.Value >= createdAt);
    }
}
