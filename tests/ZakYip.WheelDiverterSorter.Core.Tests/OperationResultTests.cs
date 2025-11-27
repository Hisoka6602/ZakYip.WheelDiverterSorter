using Xunit;
using ZakYip.WheelDiverterSorter.Core.Results;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// 统一 OperationResult 类型的单元测试
/// </summary>
public class OperationResultTests
{
    #region OperationResult (无数据) 测试

    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = OperationResult.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_WithErrorCodeAndMessage_ShouldCreateFailureResult()
    {
        // Arrange
        const string errorCode = ErrorCodes.UpstreamConnectionFailed;
        const string errorMessage = "无法连接到上游服务";

        // Act
        var result = OperationResult.Failure(errorCode, errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void Failure_WithMessageOnly_ShouldUseUnknownErrorCode()
    {
        // Arrange
        const string errorMessage = "发生了一个错误";

        // Act
        var result = OperationResult.Failure(errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Unknown, result.ErrorCode);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void FromException_ShouldCreateFailureResultFromException()
    {
        // Arrange
        var exception = new InvalidOperationException("测试异常");

        // Act
        var result = OperationResult.FromException(exception, ErrorCodes.ConfigurationError);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConfigurationError, result.ErrorCode);
        Assert.Equal("测试异常", result.ErrorMessage);
    }

    [Fact]
    public void ImplicitBoolConversion_ShouldReturnIsSuccess()
    {
        // Arrange
        var successResult = OperationResult.Success();
        var failureResult = OperationResult.Failure(ErrorCodes.Unknown, "错误");

        // Act & Assert
        Assert.True((bool)successResult);
        Assert.False((bool)failureResult);
    }

    #endregion

    #region OperationResult<T> (带数据) 测试

    [Fact]
    public void Generic_Success_ShouldCreateSuccessResultWithData()
    {
        // Arrange
        const int data = 42;

        // Act
        var result = OperationResult<int>.Success(data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Data);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Generic_Failure_ShouldCreateFailureResultWithoutData()
    {
        // Arrange
        const string errorCode = ErrorCodes.ParcelNotFound;
        const string errorMessage = "包裹未找到";

        // Act
        var result = OperationResult<int>.Failure(errorCode, errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Data);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void Generic_ToResult_ShouldConvertToNonGenericResult()
    {
        // Arrange
        var successResult = OperationResult<int>.Success(42);
        var failureResult = OperationResult<int>.Failure(ErrorCodes.Timeout, "超时");

        // Act
        var successNonGeneric = successResult.ToResult();
        var failureNonGeneric = failureResult.ToResult();

        // Assert
        Assert.True(successNonGeneric.IsSuccess);
        Assert.Null(successNonGeneric.ErrorCode);

        Assert.False(failureNonGeneric.IsSuccess);
        Assert.Equal(ErrorCodes.Timeout, failureNonGeneric.ErrorCode);
        Assert.Equal("超时", failureNonGeneric.ErrorMessage);
    }

    [Fact]
    public void Generic_Map_ShouldTransformSuccessData()
    {
        // Arrange
        var result = OperationResult<int>.Success(10);

        // Act
        var mappedResult = result.Map(x => x * 2);

        // Assert
        Assert.True(mappedResult.IsSuccess);
        Assert.Equal(20, mappedResult.Data);
    }

    [Fact]
    public void Generic_Map_ShouldPreserveFailure()
    {
        // Arrange
        var result = OperationResult<int>.Failure(ErrorCodes.InvalidParameter, "参数错误");

        // Act
        var mappedResult = result.Map(x => x * 2);

        // Assert
        Assert.False(mappedResult.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidParameter, mappedResult.ErrorCode);
        Assert.Equal("参数错误", mappedResult.ErrorMessage);
    }

    [Fact]
    public void Generic_ImplicitBoolConversion_ShouldReturnIsSuccess()
    {
        // Arrange
        var successResult = OperationResult<string>.Success("数据");
        var failureResult = OperationResult<string>.Failure(ErrorCodes.Unknown, "错误");

        // Act & Assert
        Assert.True((bool)successResult);
        Assert.False((bool)failureResult);
    }

    #endregion

    #region 错误码测试

    [Fact]
    public void ErrorCodes_GetDescription_ShouldReturnChineseDescription()
    {
        // Act & Assert
        Assert.Equal("上游连接失败", ErrorCodes.GetDescription(ErrorCodes.UpstreamConnectionFailed));
        Assert.Equal("摆轮命令执行超时", ErrorCodes.GetDescription(ErrorCodes.WheelCommandTimeout));
        Assert.Equal("路径生成失败", ErrorCodes.GetDescription(ErrorCodes.PathGenerationFailed));
    }

    [Fact]
    public void ErrorCodes_GetDescription_ShouldReturnCodeForUnknownCodes()
    {
        // Act
        var description = ErrorCodes.GetDescription("CUSTOM_ERROR");

        // Assert
        Assert.Equal("错误码: CUSTOM_ERROR", description);
    }

    #endregion

    #region 典型失败路径测试

    [Fact]
    public void TypicalFailurePath_UpstreamConnectionFailed()
    {
        // 模拟上游连接失败的处理流程
        // Arrange
        var result = OperationResult<long>.Failure(
            ErrorCodes.UpstreamConnectionFailed,
            "无法连接到 192.168.1.100:8000");

        // Act - 在业务逻辑中使用 switch 处理
        string handledBy = result.ErrorCode switch
        {
            ErrorCodes.UpstreamConnectionFailed => "ConnectionHandler",
            ErrorCodes.UpstreamTimeout => "TimeoutHandler",
            _ => "DefaultHandler"
        };

        // Assert
        Assert.Equal("ConnectionHandler", handledBy);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void TypicalFailurePath_WheelCommandTimeout()
    {
        // 模拟摆轮命令超时的处理流程
        // Arrange
        var result = OperationResult.Failure(
            ErrorCodes.WheelCommandTimeout,
            "摆轮 D001 命令执行超时（500ms）");

        // Act - 验证错误分类
        bool isWheelError = result.ErrorCode?.StartsWith("WHEEL_") ?? false;

        // Assert
        Assert.True(isWheelError);
        Assert.False(result.IsSuccess);
    }

    #endregion
}
