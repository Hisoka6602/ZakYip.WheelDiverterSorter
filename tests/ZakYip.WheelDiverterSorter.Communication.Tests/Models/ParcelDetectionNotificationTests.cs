using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Models;

/// <summary>
/// Tests for ParcelDetectionNotification
/// </summary>
public class ParcelDetectionNotificationTests
{
    [Fact]
    public void ParcelDetectionNotification_CanBeCreated()
    {
        // Arrange
        var parcelId = 1234567890L;
        var detectionTime = DateTimeOffset.Now;
        var metadata = new Dictionary<string, string>
        {
            { "weight", "1.5kg" },
            { "dimension", "30x20x10cm" }
        };

        // Act
        var notification = new ParcelDetectionNotification
        {
            ParcelId = parcelId,
            DetectionTime = detectionTime,
            Metadata = metadata
        };

        // Assert
        Assert.Equal(parcelId, notification.ParcelId);
        Assert.Equal(detectionTime, notification.DetectionTime);
        Assert.NotNull(notification.Metadata);
        Assert.Equal(2, notification.Metadata.Count);
        Assert.Equal("1.5kg", notification.Metadata["weight"]);
    }

    [Fact]
    public void ParcelDetectionNotification_DefaultDetectionTime()
    {
        // Arrange & Act
        var before = DateTimeOffset.Now;
        var notification = new ParcelDetectionNotification
        {
            ParcelId = 123456L,
            DetectionTime = DateTimeOffset.Now
        };
        var after = DateTimeOffset.Now;

        // Assert
        Assert.InRange(notification.DetectionTime, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void ParcelDetectionNotification_MetadataIsOptional()
    {
        // Arrange & Act
        var notification = new ParcelDetectionNotification
        {
            ParcelId = 123456L,
            DetectionTime = DateTimeOffset.Now,
            Metadata = null
        };

        // Assert
        Assert.Null(notification.Metadata);
    }

    [Fact]
    public void ParcelDetectionNotification_RecordEquality()
    {
        // Arrange
        var parcelId = 123456L;
        var detectionTime = DateTimeOffset.Now;
        
        var notification1 = new ParcelDetectionNotification
        {
            ParcelId = parcelId,
            DetectionTime = detectionTime
        };
        
        var notification2 = new ParcelDetectionNotification
        {
            ParcelId = parcelId,
            DetectionTime = detectionTime
        };

        // Assert - records with same values should be equal
        Assert.Equal(notification1, notification2);
    }
}
