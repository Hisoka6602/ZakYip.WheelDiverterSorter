using ZakYip.WheelDiverterSorter.Core.Sorting.Events;

namespace ZakYip.WheelDiverterSorter.Core.Tests.Sorting.Events;

/// <summary>
/// 分拣事件参数测试
/// 测试所有 Sorting.Events 命名空间下的事件参数类
/// </summary>
public class SortingEventsTests
{
    #region ParcelCreatedEventArgs Tests

    [Fact]
    public void ParcelCreatedEventArgs_WithAllProperties_ShouldCreateSuccessfully()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new ParcelCreatedEventArgs
        {
            ParcelId = 12345,
            Barcode = "BC-001",
            CreatedAt = timestamp,
            SensorId = "SENSOR-01"
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal("BC-001", args.Barcode);
        Assert.Equal(timestamp, args.CreatedAt);
        Assert.Equal("SENSOR-01", args.SensorId);
    }

    [Fact]
    public void ParcelCreatedEventArgs_WithoutOptionalProperties_ShouldCreateSuccessfully()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new ParcelCreatedEventArgs
        {
            ParcelId = 12345,
            CreatedAt = timestamp
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Null(args.Barcode);
        Assert.Equal(timestamp, args.CreatedAt);
        Assert.Null(args.SensorId);
    }

    #endregion

    #region RoutePlannedEventArgs Tests

    [Fact]
    public void RoutePlannedEventArgs_WithAllProperties_ShouldCreateSuccessfully()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new RoutePlannedEventArgs
        {
            ParcelId = 12345,
            TargetChuteId = 10,
            PlannedAt = timestamp,
            SegmentCount = 5,
            EstimatedTimeMs = 1500.5,
            IsHealthy = true,
            UnhealthyNodes = null
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(10, args.TargetChuteId);
        Assert.Equal(timestamp, args.PlannedAt);
        Assert.Equal(5, args.SegmentCount);
        Assert.Equal(1500.5, args.EstimatedTimeMs);
        Assert.True(args.IsHealthy);
        Assert.Null(args.UnhealthyNodes);
    }

    [Fact]
    public void RoutePlannedEventArgs_WithUnhealthyNodes_ShouldStoreUnhealthyInfo()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new RoutePlannedEventArgs
        {
            ParcelId = 12345,
            TargetChuteId = 10,
            PlannedAt = timestamp,
            SegmentCount = 5,
            EstimatedTimeMs = 1500.5,
            IsHealthy = false,
            UnhealthyNodes = "DIV-01,DIV-03"
        };

        // Assert
        Assert.False(args.IsHealthy);
        Assert.Equal("DIV-01,DIV-03", args.UnhealthyNodes);
    }

    #endregion

    #region ParcelDivertedEventArgs Tests

    [Fact]
    public void ParcelDivertedEventArgs_WhenActualMatchesTarget_ShouldBeSuccessful()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new ParcelDivertedEventArgs
        {
            ParcelId = 12345,
            DivertedAt = timestamp,
            ActualChuteId = 10,
            TargetChuteId = 10,
            TotalTimeMs = 2500.5
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(timestamp, args.DivertedAt);
        Assert.Equal(10, args.ActualChuteId);
        Assert.Equal(10, args.TargetChuteId);
        Assert.Equal(2500.5, args.TotalTimeMs);
        Assert.True(args.IsSuccess);
    }

    [Fact]
    public void ParcelDivertedEventArgs_WhenActualDiffersFromTarget_ShouldBeUnsuccessful()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new ParcelDivertedEventArgs
        {
            ParcelId = 12345,
            DivertedAt = timestamp,
            ActualChuteId = 999, // Exception chute
            TargetChuteId = 10,
            TotalTimeMs = 2500.5
        };

        // Assert
        Assert.Equal(999, args.ActualChuteId);
        Assert.Equal(10, args.TargetChuteId);
        Assert.False(args.IsSuccess);
    }

    [Fact]
    public void ParcelDivertedEventArgs_WhenTargetIsZero_ShouldBeSuccessful()
    {
        // Arrange & Act - Target 0 means "any chute is fine"
        var timestamp = DateTimeOffset.Now;
        var args = new ParcelDivertedEventArgs
        {
            ParcelId = 12345,
            DivertedAt = timestamp,
            ActualChuteId = 5,
            TargetChuteId = 0,
            TotalTimeMs = 2500.5
        };

        // Assert
        Assert.Equal(5, args.ActualChuteId);
        Assert.Equal(0, args.TargetChuteId);
        Assert.True(args.IsSuccess);
    }

    #endregion

    #region ParcelDivertedToExceptionEventArgs Tests

    [Fact]
    public void ParcelDivertedToExceptionEventArgs_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new ParcelDivertedToExceptionEventArgs
        {
            ParcelId = 12345,
            DivertedAt = timestamp,
            ExceptionChuteId = 999,
            Reason = "Timeout",
            OriginalTargetChuteId = 10,
            TotalTimeMs = 3000.0
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(timestamp, args.DivertedAt);
        Assert.Equal(10, args.OriginalTargetChuteId);
        Assert.Equal(999, args.ExceptionChuteId);
        Assert.Equal("Timeout", args.Reason);
        Assert.Equal(3000.0, args.TotalTimeMs);
    }

    #endregion

    #region UpstreamAssignedEventArgs Tests

    [Fact]
    public void UpstreamAssignedEventArgs_WhenSuccessful_ShouldStoreAssignment()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new UpstreamAssignedEventArgs
        {
            ParcelId = 12345,
            ChuteId = 10,
            AssignedAt = timestamp,
            LatencyMs = 150.5,
            Status = "Success",
            Source = "Upstream"
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(10, args.ChuteId);
        Assert.Equal(timestamp, args.AssignedAt);
        Assert.Equal(150.5, args.LatencyMs);
        Assert.Equal("Success", args.Status);
        Assert.Equal("Upstream", args.Source);
    }

    [Fact]
    public void UpstreamAssignedEventArgs_WhenFailed_ShouldStoreError()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new UpstreamAssignedEventArgs
        {
            ParcelId = 12345,
            ChuteId = 0,
            AssignedAt = timestamp,
            LatencyMs = 5000.0,
            Status = "Timeout",
            Source = "Upstream"
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(0, args.ChuteId);
        Assert.Equal("Timeout", args.Status);
        Assert.Equal(5000.0, args.LatencyMs);
    }

    #endregion

    #region EjectPlannedEventArgs Tests

    [Fact]
    public void EjectPlannedEventArgs_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new EjectPlannedEventArgs
        {
            ParcelId = 12345,
            PlannedAt = timestamp,
            NodeId = "DIV-05",
            Direction = "Left",
            TargetChuteId = 10
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(timestamp, args.PlannedAt);
        Assert.Equal("DIV-05", args.NodeId);
        Assert.Equal("Left", args.Direction);
        Assert.Equal(10, args.TargetChuteId);
    }

    #endregion

    #region EjectIssuedEventArgs Tests

    [Fact]
    public void EjectIssuedEventArgs_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new EjectIssuedEventArgs
        {
            ParcelId = 12345,
            IssuedAt = timestamp,
            NodeId = "DIV-05",
            Direction = "Right",
            CommandSequence = 42
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(timestamp, args.IssuedAt);
        Assert.Equal("DIV-05", args.NodeId);
        Assert.Equal("Right", args.Direction);
        Assert.Equal(42, args.CommandSequence);
    }

    #endregion

    #region OverloadEvaluatedEventArgs Tests

    [Fact]
    public void OverloadEvaluatedEventArgs_WhenNotOverloaded_ShouldStoreDecision()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new OverloadEvaluatedEventArgs
        {
            ParcelId = 12345,
            EvaluatedAt = timestamp,
            Stage = "Entry",
            ShouldForceException = false,
            ShouldMarkAsOverflow = false,
            Reason = "Normal load",
            RemainingTtlMs = 5000.0,
            InFlightParcels = 10
        };

        // Assert
        Assert.Equal(12345, args.ParcelId);
        Assert.Equal(timestamp, args.EvaluatedAt);
        Assert.Equal("Entry", args.Stage);
        Assert.False(args.ShouldForceException);
        Assert.False(args.ShouldMarkAsOverflow);
        Assert.Equal("Normal load", args.Reason);
        Assert.Equal(5000.0, args.RemainingTtlMs);
        Assert.Equal(10, args.InFlightParcels);
    }

    [Fact]
    public void OverloadEvaluatedEventArgs_WhenOverloaded_ShouldStoreDecision()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.Now;
        var args = new OverloadEvaluatedEventArgs
        {
            ParcelId = 12345,
            EvaluatedAt = timestamp,
            Stage = "RoutePlanning",
            ShouldForceException = true,
            ShouldMarkAsOverflow = true,
            Reason = "Capacity exceeded",
            RemainingTtlMs = 500.0,
            InFlightParcels = 50
        };

        // Assert
        Assert.True(args.ShouldForceException);
        Assert.True(args.ShouldMarkAsOverflow);
        Assert.Equal("Capacity exceeded", args.Reason);
        Assert.Equal(500.0, args.RemainingTtlMs);
        Assert.Equal(50, args.InFlightParcels);
    }

    #endregion
}
