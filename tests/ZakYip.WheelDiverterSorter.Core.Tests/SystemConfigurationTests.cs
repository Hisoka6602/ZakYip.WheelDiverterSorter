using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class SystemConfigurationTests
{
    [Fact]
    public void GetDefault_ReturnsValidConfiguration()
    {
        // Act
        var config = SystemConfiguration.GetDefault();

        // Assert
        Assert.Equal("system", config.ConfigName);
        Assert.Equal(999, config.ExceptionChuteId);
        Assert.NotNull(config.ChuteAssignmentTimeout);
        Assert.Equal(0.9m, config.ChuteAssignmentTimeout.SafetyFactor);
        Assert.Equal(5000, config.ChuteAssignmentTimeout.FallbackTimeoutMs);
        Assert.Equal(1, config.Version);
        Assert.Equal(SortingMode.Formal, config.SortingMode);
    }

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData(0, "异常格口ID必须大于0")]
    [InlineData(-1, "异常格口ID必须大于0")]
    public void Validate_WithInvalidExceptionChuteId_ReturnsFalse(int chuteId, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.ExceptionChuteId = chuteId;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Theory]
    [InlineData(0.05, "安全系数必须在0.1到1.0之间")]
    [InlineData(1.5, "安全系数必须在0.1到1.0之间")]
    public void Validate_WithInvalidChuteAssignmentTimeout_ReturnsFalse(decimal safetyFactor, string expectedError)
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.ChuteAssignmentTimeout.SafetyFactor = safetyFactor;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Fact]
    public void Validate_WithValidFixedChuteMode_ReturnsTrue()
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.SortingMode = SortingMode.FixedChute;
        config.FixedChuteId = 1;

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void Validate_WithValidRoundRobinMode_ReturnsTrue()
    {
        // Arrange
        var config = SystemConfiguration.GetDefault();
        config.SortingMode = SortingMode.RoundRobin;
        config.AvailableChuteIds = new List<long> { 1, 2, 3 };

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }
}
