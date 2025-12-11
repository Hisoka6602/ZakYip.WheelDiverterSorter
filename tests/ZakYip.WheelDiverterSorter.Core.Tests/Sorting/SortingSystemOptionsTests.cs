using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Tests.Sorting;

/// <summary>
/// SortingSystemOptions 和 SortingSystemOptionsValidator 单元测试
/// </summary>
public class SortingSystemOptionsTests
{
    private readonly SortingSystemOptionsValidator _validator = new();

    #region 默认值测试

    [Fact]
    public void DefaultOptions_ShouldHaveValidDefaults()
    {
        // Arrange
        var options = new SortingSystemOptions();

        // Assert
        Assert.Equal(SortingMode.Formal, options.SortingMode);
        Assert.Equal(999, options.ExceptionChuteId);
        Assert.Null(options.FixedChuteId);
        Assert.Empty(options.AvailableChuteIds);
        Assert.Equal(0.9m, options.ChuteAssignmentTimeoutSafetyFactor);
        Assert.Equal(5m, options.ChuteAssignmentFallbackTimeoutSeconds);
    }

    [Fact]
    public void DefaultOptions_ShouldPassValidation()
    {
        // Arrange
        var options = new SortingSystemOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region ExceptionChuteId 校验

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithInvalidExceptionChuteId_ShouldFail(long chuteId)
    {
        // Arrange
        var options = new SortingSystemOptions { ExceptionChuteId = chuteId };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("异常格口ID", result.FailureMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    [InlineData(10000)]
    public void Validate_WithValidExceptionChuteId_ShouldPass(long chuteId)
    {
        // Arrange
        var options = new SortingSystemOptions { ExceptionChuteId = chuteId };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region FixedChute 模式校验

    [Fact]
    public void Validate_FixedChuteMode_WithoutFixedChuteId_ShouldFail()
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            SortingMode = SortingMode.FixedChute,
            FixedChuteId = null
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("FixedChuteId", result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_FixedChuteMode_WithInvalidFixedChuteId_ShouldFail(long chuteId)
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            SortingMode = SortingMode.FixedChute,
            FixedChuteId = chuteId
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("FixedChuteId", result.FailureMessage);
    }

    [Fact]
    public void Validate_FixedChuteMode_WithValidFixedChuteId_ShouldPass()
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            SortingMode = SortingMode.FixedChute,
            FixedChuteId = 5
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region RoundRobin 模式校验

    [Fact]
    public void Validate_RoundRobinMode_WithoutAvailableChuteIds_ShouldFail()
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            SortingMode = SortingMode.RoundRobin,
            AvailableChuteIds = new List<long>()
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("AvailableChuteIds", result.FailureMessage);
    }

    [Fact]
    public void Validate_RoundRobinMode_WithInvalidChuteIdInList_ShouldFail()
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            SortingMode = SortingMode.RoundRobin,
            AvailableChuteIds = new List<long> { 1, 2, 0, 4 }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("不能包含小于等于0的值", result.FailureMessage);
    }

    [Fact]
    public void Validate_RoundRobinMode_WithNegativeChuteIdInList_ShouldFail()
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            SortingMode = SortingMode.RoundRobin,
            AvailableChuteIds = new List<long> { 1, -1, 3 }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("不能包含小于等于0的值", result.FailureMessage);
    }

    [Fact]
    public void Validate_RoundRobinMode_WithValidAvailableChuteIds_ShouldPass()
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            SortingMode = SortingMode.RoundRobin,
            AvailableChuteIds = new List<long> { 1, 2, 3, 4, 5 }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region 超时配置校验

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    public void Validate_WithTooLowSafetyFactor_ShouldFail(decimal safetyFactor)
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            ChuteAssignmentTimeoutSafetyFactor = safetyFactor
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("安全系数", result.FailureMessage);
    }

    [Theory]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Validate_WithTooHighSafetyFactor_ShouldFail(decimal safetyFactor)
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            ChuteAssignmentTimeoutSafetyFactor = safetyFactor
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("安全系数", result.FailureMessage);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    public void Validate_WithValidSafetyFactor_ShouldPass(decimal safetyFactor)
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            ChuteAssignmentTimeoutSafetyFactor = safetyFactor
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(-1)]
    public void Validate_WithTooLowFallbackTimeout_ShouldFail(decimal timeout)
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            ChuteAssignmentFallbackTimeoutSeconds = timeout
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("降级超时时间", result.FailureMessage);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(100)]
    public void Validate_WithTooHighFallbackTimeout_ShouldFail(decimal timeout)
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            ChuteAssignmentFallbackTimeoutSeconds = timeout
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("降级超时时间", result.FailureMessage);
    }

    #endregion

    #region 多重错误测试

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var options = new SortingSystemOptions
        {
            ExceptionChuteId = 0,
            SortingMode = SortingMode.FixedChute,
            FixedChuteId = null,
            ChuteAssignmentTimeoutSafetyFactor = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("异常格口ID", result.FailureMessage);
        Assert.Contains("FixedChuteId", result.FailureMessage);
        Assert.Contains("安全系数", result.FailureMessage);
    }

    #endregion
}
