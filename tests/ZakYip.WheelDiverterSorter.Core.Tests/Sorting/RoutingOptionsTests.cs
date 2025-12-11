using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Sorting.Policies;

namespace ZakYip.WheelDiverterSorter.Core.Tests.Sorting;

/// <summary>
/// RoutingOptions 和 RoutingOptionsValidator 单元测试
/// </summary>
public class RoutingOptionsTests
{
    private readonly RoutingOptionsValidator _validator = new();

    #region 默认值测试

    [Fact]
    public void DefaultOptions_ShouldHaveValidDefaults()
    {
        // Arrange
        var options = new RoutingOptions();

        // Assert
        Assert.True(options.EnablePathCaching);
        Assert.Equal(300, options.PathCacheExpirationSeconds);
        Assert.Equal(50, options.MaxPathSegments);
        Assert.Equal(30000, options.DefaultTtlMs);
        Assert.True(options.EnablePathRerouting);
        Assert.True(options.EnableNodeHealthCheck);
    }

    [Fact]
    public void DefaultOptions_ShouldPassValidation()
    {
        // Arrange
        var options = new RoutingOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region 缓存过期时间校验

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithTooLowCacheExpiration_ShouldFail(int seconds)
    {
        // Arrange
        var options = new RoutingOptions
        {
            PathCacheExpirationSeconds = seconds
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("缓存过期时间", result.FailureMessage);
    }

    [Theory]
    [InlineData(3601)]
    [InlineData(10000)]
    public void Validate_WithTooHighCacheExpiration_ShouldFail(int seconds)
    {
        // Arrange
        var options = new RoutingOptions
        {
            PathCacheExpirationSeconds = seconds
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("缓存过期时间", result.FailureMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(3600)]
    public void Validate_WithValidCacheExpiration_ShouldPass(int seconds)
    {
        // Arrange
        var options = new RoutingOptions
        {
            PathCacheExpirationSeconds = seconds
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region 最大路径段数校验

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithTooLowMaxPathSegments_ShouldFail(int segments)
    {
        // Arrange
        var options = new RoutingOptions
        {
            MaxPathSegments = segments
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("最大路径段数", result.FailureMessage);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(1000)]
    public void Validate_WithTooHighMaxPathSegments_ShouldFail(int segments)
    {
        // Arrange
        var options = new RoutingOptions
        {
            MaxPathSegments = segments
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("最大路径段数", result.FailureMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WithValidMaxPathSegments_ShouldPass(int segments)
    {
        // Arrange
        var options = new RoutingOptions
        {
            MaxPathSegments = segments
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region 默认 TTL 校验

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(-1)]
    public void Validate_WithTooLowDefaultTtlMs_ShouldFail(int ttlMs)
    {
        // Arrange
        var options = new RoutingOptions
        {
            DefaultTtlMs = ttlMs
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("DefaultTtlMs", result.FailureMessage);
    }

    [Theory]
    [InlineData(120001)]
    [InlineData(200000)]
    public void Validate_WithTooHighDefaultTtlMs_ShouldFail(int ttlMs)
    {
        // Arrange
        var options = new RoutingOptions
        {
            DefaultTtlMs = ttlMs
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("DefaultTtlMs", result.FailureMessage);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(30000)]
    [InlineData(120000)]
    public void Validate_WithValidDefaultTtlMs_ShouldPass(int ttlMs)
    {
        // Arrange
        var options = new RoutingOptions
        {
            DefaultTtlMs = ttlMs
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region 多重错误测试

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var options = new RoutingOptions
        {
            PathCacheExpirationSeconds = 0,
            MaxPathSegments = 0,
            DefaultTtlMs = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("缓存过期时间", result.FailureMessage);
        Assert.Contains("最大路径段数", result.FailureMessage);
        Assert.Contains("DefaultTtlMs", result.FailureMessage);
    }

    #endregion
}
