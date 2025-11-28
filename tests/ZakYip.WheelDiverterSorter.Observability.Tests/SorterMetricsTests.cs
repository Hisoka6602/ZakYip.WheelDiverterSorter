using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZakYip.WheelDiverterSorter.Application.Services;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// 测试SorterMetrics的指标收集功能
/// Tests for metrics collection in SorterMetrics
/// </summary>
public class SorterMetricsTests
{
    [Fact]
    public void RecordSortingRequest_ShouldIncrementCounter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");
        var sorterMetrics = new SorterMetrics(meterFactory);

        // Act
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordSortingRequest();

        // Assert
        var measurements = metricsHelper.GetLongMeasurements();
        var totalRequests = measurements.Where(m => m.Value == 1).Count();
        Assert.Equal(3, totalRequests);
    }

    [Fact]
    public void RecordSortingSuccess_ShouldIncrementSuccessCounterAndRecordDuration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");

        // Act
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordSortingSuccess(100.5);
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordSortingSuccess(200.3);

        // Assert - Check success counter (should have 2 increments of 1)
        var longMeasurements = metricsHelper.GetLongMeasurements();
        var successCount = longMeasurements.Where(m => m.Value == 1).Count();
        Assert.True(successCount >= 2, $"Expected at least 2 success counts but got {successCount}");

        // Assert - Check duration histogram (should have 2 measurements)
        var doubleMeasurements = metricsHelper.GetDoubleMeasurements();
        Assert.True(doubleMeasurements.Count >= 2, $"Expected at least 2 duration measurements but got {doubleMeasurements.Count}");
        Assert.Contains(doubleMeasurements, m => m.Value == 100.5);
        Assert.Contains(doubleMeasurements, m => m.Value == 200.3);
    }

    [Fact]
    public void RecordSortingFailure_ShouldIncrementFailureCounterAndRecordDuration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");

        // Act
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordSortingFailure(50.2);

        // Assert - Check failure counter
        var longMeasurements = metricsHelper.GetLongMeasurements();
        Assert.Contains(longMeasurements, m => m.Value == 1);

        // Assert - Check duration histogram
        var doubleMeasurements = metricsHelper.GetDoubleMeasurements();
        Assert.Contains(doubleMeasurements, m => m.Value == 50.2);
    }

    [Fact]
    public void RecordPathGeneration_ShouldIncrementCounterAndRecordDuration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");

        // Act - Record successful path generation
        sorterMetrics.RecordPathGeneration(15.5, true);
        // Act - Record failed path generation
        sorterMetrics.RecordPathGeneration(8.3, false);

        // Assert - Check generation counter (should have 2 increments)
        var longMeasurements = metricsHelper.GetLongMeasurements();
        var generationCount = longMeasurements.Where(m => m.Value == 1).Count();
        Assert.True(generationCount >= 2, $"Expected at least 2 generation counts but got {generationCount}");

        // Assert - Check duration histogram
        var doubleMeasurements = metricsHelper.GetDoubleMeasurements();
        Assert.Contains(doubleMeasurements, m => m.Value == 15.5);
        Assert.Contains(doubleMeasurements, m => m.Value == 8.3);
    }

    [Fact]
    public void RecordPathExecution_ShouldIncrementCounterAndRecordDuration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");

        // Act - Record successful path execution
        sorterMetrics.RecordPathExecution(120.8, true);
        // Act - Record failed path execution  
        sorterMetrics.RecordPathExecution(95.2, false);

        // Assert - Check execution counter (should have 2 increments)
        var longMeasurements = metricsHelper.GetLongMeasurements();
        var executionCount = longMeasurements.Where(m => m.Value == 1).Count();
        Assert.True(executionCount >= 2, $"Expected at least 2 execution counts but got {executionCount}");

        // Assert - Check duration histogram
        var doubleMeasurements = metricsHelper.GetDoubleMeasurements();
        Assert.Contains(doubleMeasurements, m => m.Value == 120.8);
        Assert.Contains(doubleMeasurements, m => m.Value == 95.2);
    }

    [Fact]
    public void PerformanceMetrics_ShouldCaptureDurationAccurately()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");

        // Act - Record various durations
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordPathGeneration(10.5, true);
        sorterMetrics.RecordPathExecution(150.8, true);
        sorterMetrics.RecordSortingSuccess(161.3); // Total = gen + exec

        // Assert - Verify all durations are captured
        var doubleMeasurements = metricsHelper.GetDoubleMeasurements();
        
        Assert.Contains(doubleMeasurements, m => m.Value == 161.3); // Sorting duration
        Assert.Contains(doubleMeasurements, m => m.Value == 10.5);  // Path generation duration
        Assert.Contains(doubleMeasurements, m => m.Value == 150.8); // Path execution duration
    }

    [Fact]
    public void BusinessMetrics_ShouldTrackOperationOutcomes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");

        // Act - Simulate business operations
        // Successful sorting flow
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordPathGeneration(10.0, true);
        sorterMetrics.RecordPathExecution(100.0, true);
        sorterMetrics.RecordSortingSuccess(110.0);

        // Failed sorting flow (path generation failed)
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordPathGeneration(5.0, false);
        sorterMetrics.RecordSortingFailure(5.0);

        // Failed sorting flow (path execution failed)
        sorterMetrics.RecordSortingRequest();
        sorterMetrics.RecordPathGeneration(8.0, true);
        sorterMetrics.RecordPathExecution(50.0, false);
        sorterMetrics.RecordSortingFailure(58.0);

        // Assert - Verify business metrics were recorded
        var longMeasurements = metricsHelper.GetLongMeasurements();
        var doubleMeasurements = metricsHelper.GetDoubleMeasurements();
        
        // We should have recorded multiple operations
        Assert.True(longMeasurements.Count >= 8, $"Expected at least 8 counter measurements but got {longMeasurements.Count}");
        Assert.True(doubleMeasurements.Count >= 6, $"Expected at least 6 histogram measurements but got {doubleMeasurements.Count}");
    }

    [Fact]
    public void SorterMetrics_ShouldUseCorrectMeterName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        // Act
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");
        sorterMetrics.RecordSortingRequest();

        // Assert - Verify meter is created with correct name
        sorterMetrics.RecordSortingRequest();
        
        var measurements = metricsHelper.GetLongMeasurements();
        Assert.NotEmpty(measurements); // Should have captured metrics from the correct meter
    }

    [Fact]
    public void MultipleMetricsTypes_ShouldWorkTogether()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        
        var sorterMetrics = new SorterMetrics(meterFactory);
        using var metricsHelper = new MetricsTestHelper("ZakYip.WheelDiverterSorter");

        // Act - Simulate a workflow
        // 5 requests come in
        for (int i = 0; i < 5; i++)
        {
            sorterMetrics.RecordSortingRequest();
        }

        // 3 succeed, 2 fail
        for (int i = 0; i < 3; i++)
        {
            sorterMetrics.RecordSortingSuccess(100.0 + i * 10);
        }
        for (int i = 0; i < 2; i++)
        {
            sorterMetrics.RecordSortingFailure(50.0 + i * 5);
        }

        // Assert - Verify all metrics were recorded
        var longMeasurements = metricsHelper.GetLongMeasurements();
        var doubleMeasurements = metricsHelper.GetDoubleMeasurements();
        
        // Should have multiple counter increments
        Assert.True(longMeasurements.Count >= 10, $"Expected at least 10 counter measurements but got {longMeasurements.Count}");
        
        // Should have 5 duration measurements (3 success + 2 failure)
        Assert.True(doubleMeasurements.Count >= 5, $"Expected at least 5 duration measurements but got {doubleMeasurements.Count}");
    }
}
