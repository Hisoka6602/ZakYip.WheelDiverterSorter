using System.Text.Json.Serialization;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 统一API响应包装器
/// </summary>
/// <typeparam name="T">响应数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    /// <summary>
    /// 业务状态码
    /// </summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>
    /// 响应消息（中文/可读）
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// 响应数据负载
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>
    /// 响应时间戳
    /// </summary>
    [JsonPropertyName("timestamp")]
#pragma warning disable ZAKYIP004 // API响应时间戳用于外部协议交互，使用UTC时间
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
#pragma warning restore ZAKYIP004

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ApiResponse<T> Ok(T data, string message = "操作成功 - Operation successful")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = "Ok",
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 创建错误响应
    /// </summary>
    public static ApiResponse<T> Error(string code, string message, T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 创建参数验证错误响应
    /// </summary>
    public static ApiResponse<T> BadRequest(string message = "请求参数无效 - Invalid request parameters", T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = "BadRequest",
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 创建未找到错误响应
    /// </summary>
    public static ApiResponse<T> NotFound(string message = "未找到资源 - Resource not found", T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = "NotFound",
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 创建服务器错误响应
    /// </summary>
    public static ApiResponse<T> ServerError(string message = "服务器内部错误 - Internal server error", T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = "ServerError",
            Message = message,
            Data = data
        };
    }
}

