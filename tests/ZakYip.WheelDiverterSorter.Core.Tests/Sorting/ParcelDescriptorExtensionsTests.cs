using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Events.Sorting;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;

namespace ZakYip.WheelDiverterSorter.Core.Tests.Sorting;

/// <summary>
/// Test class for ParcelDescriptorExtensions
/// </summary>
public class ParcelDescriptorExtensionsTests
{
    private static readonly DateTimeOffset TestTime = new(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
    private static readonly Dictionary<string, string> TestMetadata = new() { { "key1", "value1" } };

    #region CreateParcelRequest 转换测试

    [Fact]
    public void ToParcelDescriptor_FromCreateParcelRequest_MapsAllFields()
    {
        // Arrange
        var request = new CreateParcelRequest
        {
            ParcelId = 123456789,
            Barcode = "BC001",
            DetectedAt = TestTime,
            SensorId = 1,
            Metadata = TestMetadata
        };

        // Act
        var descriptor = request.ToParcelDescriptor();

        // Assert
        Assert.Equal(request.ParcelId, descriptor.ParcelId);
        Assert.Equal(request.Barcode, descriptor.Barcode);
        Assert.Equal(request.DetectedAt, descriptor.IngressTime);
        Assert.Equal(request.SensorId, descriptor.SensorId);
        Assert.Equal(request.Metadata, descriptor.Metadata);
    }

    [Fact]
    public void ToParcelDescriptor_FromCreateParcelRequest_WithNullOptionalFields_MapsCorrectly()
    {
        // Arrange
        var request = new CreateParcelRequest
        {
            ParcelId = 123456789,
            Barcode = null,
            DetectedAt = TestTime,
            SensorId = null,
            Metadata = null
        };

        // Act
        var descriptor = request.ToParcelDescriptor();

        // Assert
        Assert.Equal(request.ParcelId, descriptor.ParcelId);
        Assert.Null(descriptor.Barcode);
        Assert.Null(descriptor.SensorId);
        Assert.Null(descriptor.Metadata);
    }

    [Fact]
    public void ToParcelDescriptor_FromCreateParcelRequest_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        CreateParcelRequest? request = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => request!.ToParcelDescriptor());
    }

    #endregion

    #region AssignChuteRequest 转换测试

    [Fact]
    public void ToParcelDescriptor_FromAssignChuteRequest_MapsAllFields()
    {
        // Arrange
        var request = new AssignChuteRequest
        {
            ParcelId = 123456789,
            Barcode = "BC002",
            RequestTime = TestTime,
            SensorId = 2,
            Metadata = TestMetadata,
            CandidateChuteIds = new[] { 1, 2, 3 }
        };

        // Act
        var descriptor = request.ToParcelDescriptor();

        // Assert
        Assert.Equal(request.ParcelId, descriptor.ParcelId);
        Assert.Equal(request.Barcode, descriptor.Barcode);
        Assert.Equal(request.RequestTime, descriptor.IngressTime);
        Assert.Equal(request.SensorId, descriptor.SensorId);
        Assert.Equal(request.Metadata, descriptor.Metadata);
    }

    [Fact]
    public void ToParcelDescriptor_FromAssignChuteRequest_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        AssignChuteRequest? request = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => request!.ToParcelDescriptor());
    }

    #endregion

    #region SortingRequest 转换测试

    [Fact]
    public void ToParcelDescriptor_FromSortingRequest_MapsAllFields()
    {
        // Arrange
        var request = new SortingRequest
        {
            ParcelId = 123456789,
            Barcode = "BC003",
            RequestTime = TestTime,
            SensorId = 3,
            Metadata = TestMetadata,
            CandidateChuteIds = new[] { 1, 2, 3 }
        };

        // Act
        var descriptor = request.ToParcelDescriptor();

        // Assert
        Assert.Equal(request.ParcelId, descriptor.ParcelId);
        Assert.Equal(request.Barcode, descriptor.Barcode);
        Assert.Equal(request.RequestTime, descriptor.IngressTime);
        Assert.Equal(request.SensorId, descriptor.SensorId);
        Assert.Equal(request.Metadata, descriptor.Metadata);
    }

    [Fact]
    public void ToParcelDescriptor_FromSortingRequest_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        SortingRequest? request = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => request!.ToParcelDescriptor());
    }

    #endregion

    #region SortingPipelineContext 转换测试

    [Fact]
    public void ToParcelDescriptor_FromSortingPipelineContext_MapsAllFields()
    {
        // Arrange
        var context = new SortingPipelineContext
        {
            ParcelId = 123456789,
            Barcode = "BC004",
            CreatedAt = TestTime
        };

        // Act
        var descriptor = context.ToParcelDescriptor();

        // Assert
        Assert.Equal(context.ParcelId, descriptor.ParcelId);
        Assert.Equal(context.Barcode, descriptor.Barcode);
        Assert.Equal(context.CreatedAt, descriptor.IngressTime);
        Assert.Null(descriptor.SensorId);  // SortingPipelineContext 不包含 SensorId
        Assert.Null(descriptor.Metadata);  // SortingPipelineContext 的扩展数据结构不同
    }

    [Fact]
    public void ToParcelDescriptor_FromSortingPipelineContext_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        SortingPipelineContext? context = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => context!.ToParcelDescriptor());
    }

    #endregion

    #region ParcelCreatedEventArgs 转换测试

    [Fact]
    public void ToParcelDescriptor_FromParcelCreatedEventArgs_MapsAllFields()
    {
        // Arrange
        var eventArgs = new ParcelCreatedEventArgs
        {
            ParcelId = 123456789,
            Barcode = "BC005",
            CreatedAt = TestTime,
            SensorId = 5
        };

        // Act
        var descriptor = eventArgs.ToParcelDescriptor();

        // Assert
        Assert.Equal(eventArgs.ParcelId, descriptor.ParcelId);
        Assert.Equal(eventArgs.Barcode, descriptor.Barcode);
        Assert.Equal(eventArgs.CreatedAt, descriptor.IngressTime);
        Assert.Equal(eventArgs.SensorId, descriptor.SensorId);
        Assert.Null(descriptor.Metadata);  // ParcelCreatedEventArgs 不包含 Metadata
    }

    #endregion

    #region 一致性测试

    [Fact]
    public void ToParcelDescriptor_FromDifferentSources_ProduceEquivalentDescriptors()
    {
        // Arrange
        var parcelId = 123456789L;
        var barcode = "BC006";
        var time = TestTime;
        var sensorId = 6L;

        var createRequest = new CreateParcelRequest
        {
            ParcelId = parcelId,
            Barcode = barcode,
            DetectedAt = time,
            SensorId = sensorId
        };

        var assignRequest = new AssignChuteRequest
        {
            ParcelId = parcelId,
            Barcode = barcode,
            RequestTime = time,
            SensorId = sensorId
        };

        var sortingRequest = new SortingRequest
        {
            ParcelId = parcelId,
            Barcode = barcode,
            RequestTime = time,
            SensorId = sensorId
        };

        var eventArgs = new ParcelCreatedEventArgs
        {
            ParcelId = parcelId,
            Barcode = barcode,
            CreatedAt = time,
            SensorId = sensorId
        };

        // Act
        var descriptor1 = createRequest.ToParcelDescriptor();
        var descriptor2 = assignRequest.ToParcelDescriptor();
        var descriptor3 = sortingRequest.ToParcelDescriptor();
        var descriptor4 = eventArgs.ToParcelDescriptor();

        // Assert - 所有描述符的核心字段应一致
        Assert.Equal(descriptor1.ParcelId, descriptor2.ParcelId);
        Assert.Equal(descriptor2.ParcelId, descriptor3.ParcelId);
        Assert.Equal(descriptor3.ParcelId, descriptor4.ParcelId);

        Assert.Equal(descriptor1.Barcode, descriptor2.Barcode);
        Assert.Equal(descriptor2.Barcode, descriptor3.Barcode);
        Assert.Equal(descriptor3.Barcode, descriptor4.Barcode);

        Assert.Equal(descriptor1.IngressTime, descriptor2.IngressTime);
        Assert.Equal(descriptor2.IngressTime, descriptor3.IngressTime);
        Assert.Equal(descriptor3.IngressTime, descriptor4.IngressTime);

        Assert.Equal(descriptor1.SensorId, descriptor2.SensorId);
        Assert.Equal(descriptor2.SensorId, descriptor3.SensorId);
        Assert.Equal(descriptor3.SensorId, descriptor4.SensorId);
    }

    #endregion
}
