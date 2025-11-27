using Xunit;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Drivers;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests;

/// <summary>
/// WheelDriverException 异常类的单元测试
/// </summary>
public class WheelDriverExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        const string errorCode = ErrorCodes.WheelCommandFailed;
        const string message = "摆轮命令失败";
        const string diverterId = "D001";

        // Act
        var exception = new WheelDriverException(errorCode, message, diverterId);

        // Assert
        Assert.Equal(errorCode, exception.ErrorCode);
        Assert.Equal(message, exception.Message);
        Assert.Equal(diverterId, exception.DiverterId);
    }

    [Fact]
    public void Constructor_WithInnerException_ShouldIncludeInnerException()
    {
        // Arrange
        var innerException = new TimeoutException("连接超时");
        const string errorCode = ErrorCodes.WheelCommunicationError;
        const string message = "通信错误";

        // Act
        var exception = new WheelDriverException(errorCode, message, innerException, "D002");

        // Assert
        Assert.Equal(innerException, exception.InnerException);
        Assert.Equal(errorCode, exception.ErrorCode);
    }

    [Fact]
    public void NotFound_ShouldCreateCorrectException()
    {
        // Act
        var exception = WheelDriverException.NotFound("D003");

        // Assert
        Assert.Equal(ErrorCodes.WheelNotFound, exception.ErrorCode);
        Assert.Equal("D003", exception.DiverterId);
        Assert.Contains("D003", exception.Message);
    }

    [Fact]
    public void CommandTimeout_ShouldCreateCorrectException()
    {
        // Act
        var exception = WheelDriverException.CommandTimeout("D004", 500);

        // Assert
        Assert.Equal(ErrorCodes.WheelCommandTimeout, exception.ErrorCode);
        Assert.Equal("D004", exception.DiverterId);
        Assert.Contains("500", exception.Message);
    }

    [Fact]
    public void CommandFailed_ShouldCreateCorrectException()
    {
        // Arrange
        const string reason = "电机过热";

        // Act
        var exception = WheelDriverException.CommandFailed("D005", reason);

        // Assert
        Assert.Equal(ErrorCodes.WheelCommandFailed, exception.ErrorCode);
        Assert.Equal("D005", exception.DiverterId);
        Assert.Contains(reason, exception.Message);
    }

    [Fact]
    public void CommunicationError_ShouldWrapInnerException()
    {
        // Arrange
        var innerException = new IOException("网络断开");

        // Act
        var exception = WheelDriverException.CommunicationError("D006", innerException);

        // Assert
        Assert.Equal(ErrorCodes.WheelCommunicationError, exception.ErrorCode);
        Assert.Equal("D006", exception.DiverterId);
        Assert.Equal(innerException, exception.InnerException);
        Assert.Contains("网络断开", exception.Message);
    }

    [Fact]
    public void InvalidDirection_ShouldCreateCorrectException()
    {
        // Act
        var exception = WheelDriverException.InvalidDirection("D007", "Unknown");

        // Assert
        Assert.Equal(ErrorCodes.WheelInvalidDirection, exception.ErrorCode);
        Assert.Equal("D007", exception.DiverterId);
        Assert.Contains("Unknown", exception.Message);
    }

    [Fact]
    public void ToOperationResult_ShouldConvertToFailureResult()
    {
        // Arrange
        var exception = WheelDriverException.CommandFailed("D008", "测试失败");

        // Act
        var result = exception.ToOperationResult();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.WheelCommandFailed, result.ErrorCode);
        Assert.Equal(exception.Message, result.ErrorMessage);
    }

    [Fact]
    public void ToOperationResultGeneric_ShouldConvertToFailureResult()
    {
        // Arrange
        var exception = WheelDriverException.NotFound("D009");

        // Act
        var result = exception.ToOperationResult<int>();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.WheelNotFound, result.ErrorCode);
        Assert.Equal(exception.Message, result.ErrorMessage);
        Assert.Equal(default, result.Data);
    }
}
