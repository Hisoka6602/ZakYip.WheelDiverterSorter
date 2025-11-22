using Microsoft.AspNetCore.Mvc;
using ZakYip.WheelDiverterSorter.Host.Models;

namespace ZakYip.WheelDiverterSorter.Host.Controllers;

/// <summary>
/// API 控制器基类
/// </summary>
/// <remarks>
/// 提供统一的响应封装辅助方法，确保所有 API 返回一致的响应格式
/// </remarks>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// 返回成功响应
    /// </summary>
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = "操作成功")
    {
        return Ok(ApiResponse<T>.Ok(data, message));
    }

    /// <summary>
    /// 返回成功响应（无数据）
    /// </summary>
    protected ActionResult<ApiResponse<object>> Success(string message = "操作成功")
    {
        return Ok(ApiResponse<object>.Ok(new { }, message));
    }

    /// <summary>
    /// 返回错误响应
    /// </summary>
    protected ActionResult<ApiResponse<T>> Error<T>(string code, string message, T? data = default)
    {
        return Ok(ApiResponse<T>.Error(code, message, data));
    }

    /// <summary>
    /// 返回参数验证错误响应
    /// </summary>
    protected ActionResult<ApiResponse<T>> ValidationError<T>(string message = "请求参数无效", T? data = default)
    {
        return BadRequest(ApiResponse<T>.BadRequest(message, data));
    }

    /// <summary>
    /// 返回参数验证错误响应（从ModelState）
    /// </summary>
    protected ActionResult<ApiResponse<object>> ValidationError()
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
        
        return BadRequest(ApiResponse<object>.BadRequest(
            "请求参数无效", 
            new { errors }));
    }

    /// <summary>
    /// 返回未找到错误响应
    /// </summary>
    protected ActionResult<ApiResponse<T>> NotFoundError<T>(string message = "未找到资源", T? data = default)
    {
        return NotFound(ApiResponse<T>.NotFound(message, data));
    }

    /// <summary>
    /// 返回服务器错误响应
    /// </summary>
    protected ActionResult<ApiResponse<T>> ServerError<T>(string message = "服务器内部错误", T? data = default)
    {
        return StatusCode(500, ApiResponse<T>.ServerError(message, data));
    }

    /// <summary>
    /// 返回服务器错误响应（无数据）
    /// </summary>
    protected ActionResult<ApiResponse<object>> ServerError(string message = "服务器内部错误")
    {
        return StatusCode(500, ApiResponse<object>.ServerError(message));
    }
}
