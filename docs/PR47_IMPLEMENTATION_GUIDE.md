# PR-47 Implementation Guide: Controller Standardization

## Overview

This guide provides step-by-step instructions for standardizing Host controllers to comply with repository constraints. The main requirement is to use `ApiResponse<T>` wrapper for all responses and extend `ApiControllerBase`.

## Current vs. Target Pattern

### ❌ Current Pattern (Non-Compliant)
```csharp
[ApiController]
[Route("api/config")]
public class ConfigurationController : ControllerBase
{
    [HttpGet("exception-policy")]
    public ActionResult<ExceptionRoutingPolicy> GetExceptionPolicy()
    {
        try
        {
            var config = _systemConfigRepository.Get();
            return Ok(new ExceptionRoutingPolicy { /* ... */ });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取异常路由策略失败");
            return StatusCode(500, new { message = "获取异常路由策略失败" });
        }
    }
}
```

### ✅ Target Pattern (Compliant)
```csharp
[ApiController]
[Route("api/config")]
public class ConfigurationController : ApiControllerBase  // ✅ Extend ApiControllerBase
{
    [HttpGet("exception-policy")]
    public ActionResult<ApiResponse<ExceptionRoutingPolicy>> GetExceptionPolicy()  // ✅ Wrap in ApiResponse
    {
        try
        {
            var config = _systemConfigRepository.Get();
            var policy = new ExceptionRoutingPolicy { /* ... */ };
            return Success(policy, "获取异常路由策略成功");  // ✅ Use base class helper
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取异常路由策略失败");
            return ServerError<ExceptionRoutingPolicy>("获取异常路由策略失败");  // ✅ Use base class helper
        }
    }
}
```

## Step-by-Step Refactoring Process

### Step 1: Change Base Class

```csharp
// Before
public class ConfigurationController : ControllerBase

// After  
public class ConfigurationController : ApiControllerBase
```

### Step 2: Update Return Types

```csharp
// Before
public ActionResult<ExceptionRoutingPolicy> GetExceptionPolicy()

// After
public ActionResult<ApiResponse<ExceptionRoutingPolicy>> GetExceptionPolicy()
```

### Step 3: Update Return Statements

#### Success Responses
```csharp
// Before
return Ok(data);

// After
return Success(data);  // Uses default "操作成功" message
// OR
return Success(data, "Custom success message");
```

#### Error Responses
```csharp
// Before
return BadRequest(new { message = "参数无效" });

// After
return ValidationError<YourType>("参数无效");
```

```csharp
// Before
return NotFound(new { message = "未找到资源" });

// After
return NotFoundError<YourType>("未找到资源");
```

```csharp
// Before
return StatusCode(500, new { message = "服务器错误" });

// After
return ServerError<YourType>("服务器错误");
```

### Step 4: Update Swagger Annotations

```csharp
// Before
[SwaggerResponse(200, "成功返回异常策略", typeof(ExceptionRoutingPolicy))]
[ProducesResponseType(typeof(ExceptionRoutingPolicy), 200)]

// After
[SwaggerResponse(200, "成功返回异常策略", typeof(ApiResponse<ExceptionRoutingPolicy>))]
[ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 200)]
```

## ApiControllerBase Helper Methods Reference

```csharp
// Success responses
Success<T>(T data, string message = "操作成功")
Success(string message = "操作成功")  // For responses without data

// Error responses  
Error<T>(string code, string message, T? data = default)
ValidationError<T>(string message = "请求参数无效", T? data = default)
ValidationError()  // Auto-extracts from ModelState
NotFoundError<T>(string message = "未找到资源", T? data = default)
ServerError<T>(string message = "服务器内部错误", T? data = default)
ServerError(string message = "服务器内部错误")  // For responses without data
```

## Complete Example: ConfigurationController GET Endpoint

```csharp
/// <summary>
/// 获取异常路由策略
/// </summary>
/// <returns>异常路由策略</returns>
/// <response code="200">成功返回异常策略</response>
/// <response code="500">服务器内部错误</response>
[HttpGet("exception-policy")]
[SwaggerOperation(
    Summary = "获取异常路由策略",
    Description = "返回系统当前的异常路由策略配置",
    OperationId = "GetExceptionPolicy",
    Tags = new[] { "配置管理" }
)]
[SwaggerResponse(200, "成功返回异常策略", typeof(ApiResponse<ExceptionRoutingPolicy>))]
[SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<ExceptionRoutingPolicy>))]
[ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 200)]
[ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 500)]
public ActionResult<ApiResponse<ExceptionRoutingPolicy>> GetExceptionPolicy()
{
    try
    {
        var config = _systemConfigRepository.Get();
#pragma warning disable CS0618 // 向后兼容
        var policy = new ExceptionRoutingPolicy
        {
            ExceptionChuteId = config.ExceptionChuteId,
            UpstreamTimeoutMs = config.ChuteAssignmentTimeoutMs,
            RetryOnTimeout = config.RetryCount > 0,
            RetryCount = config.RetryCount,
            RetryDelayMs = config.RetryDelayMs,
            UseExceptionOnTopologyUnreachable = true,
            UseExceptionOnTtlFailure = true
        };
#pragma warning restore CS0618
        
        return Success(policy, "获取异常路由策略成功");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "获取异常路由策略失败");
        return ServerError<ExceptionRoutingPolicy>("获取异常路由策略失败");
    }
}
```

## Complete Example: ConfigurationController PUT Endpoint with Validation

```csharp
/// <summary>
/// 更新异常路由策略
/// </summary>
/// <param name="policy">异常路由策略</param>
/// <returns>更新后的异常策略</returns>
/// <response code="200">更新成功</response>
/// <response code="400">请求参数无效</response>
/// <response code="500">服务器内部错误</response>
[HttpPut("exception-policy")]
[SwaggerOperation(
    Summary = "更新异常路由策略",
    Description = "更新系统异常路由策略，配置立即生效",
    OperationId = "UpdateExceptionPolicy",
    Tags = new[] { "配置管理" }
)]
[SwaggerResponse(200, "更新成功", typeof(ApiResponse<ExceptionRoutingPolicy>))]
[SwaggerResponse(400, "请求参数无效", typeof(ApiResponse<ExceptionRoutingPolicy>))]
[SwaggerResponse(500, "服务器内部错误", typeof(ApiResponse<ExceptionRoutingPolicy>))]
[ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 200)]
[ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 400)]
[ProducesResponseType(typeof(ApiResponse<ExceptionRoutingPolicy>), 500)]
public ActionResult<ApiResponse<ExceptionRoutingPolicy>> UpdateExceptionPolicy(
    [FromBody] ExceptionRoutingPolicy policy)
{
    try
    {
        // Use ModelState validation first
        if (!ModelState.IsValid)
        {
            return ValidationError<ExceptionRoutingPolicy>();  // ✅ Auto-extracts errors
        }

        // Business validation
        if (policy.ExceptionChuteId <= 0)
        {
            return ValidationError<ExceptionRoutingPolicy>("异常格口ID必须大于0");
        }

        if (policy.UpstreamTimeoutMs < 1000 || policy.UpstreamTimeoutMs > 60000)
        {
            return ValidationError<ExceptionRoutingPolicy>("上游超时时间必须在1000-60000毫秒之间");
        }

        // TODO: Move validation to separate validator class or request attributes
        // See "Validation Extraction" section below

        // Update configuration
        var config = _systemConfigRepository.Get();
        config.ExceptionChuteId = policy.ExceptionChuteId;
        config.RetryCount = policy.RetryOnTimeout ? policy.RetryCount : 0;
        config.RetryDelayMs = policy.RetryDelayMs;
        config.UpdatedAt = _clock.LocalNow;

        _systemConfigRepository.Update(config);

        _logger.LogInformation(
            "异常路由策略已更新: ExceptionChuteId={ExceptionChuteId}",
            policy.ExceptionChuteId);

        return Success(policy, "异常路由策略已更新");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "更新异常路由策略失败");
        return ServerError<ExceptionRoutingPolicy>("更新异常路由策略失败");
    }
}
```

## Validation Extraction (Recommended Next Step)

After standardizing to ApiResponse, extract complex validation:

### Option 1: Data Annotations (Simple validation)
```csharp
public record ExceptionPolicyRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "异常格口ID必须大于0")]
    public required int ExceptionChuteId { get; init; }

    [Range(1000, 60000, ErrorMessage = "上游超时时间必须在1000-60000毫秒之间")]
    public required int UpstreamTimeoutMs { get; init; }

    [Range(0, 10, ErrorMessage = "重试次数必须在0-10之间")]
    public required int RetryCount { get; init; }

    [Range(100, 10000, ErrorMessage = "重试延迟必须在100-10000毫秒之间")]
    public required int RetryDelayMs { get; init; }
}
```

### Option 2: Custom Validation Attributes (Complex validation)
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ThresholdValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is ReleaseThrottleConfigRequest request)
        {
            if (request.SevereThresholdLatencyMs < request.WarningThresholdLatencyMs)
            {
                return new ValidationResult("严重延迟阈值必须大于警告阈值");
            }
        }
        return ValidationResult.Success;
    }
}

[ThresholdValidation]
public record ReleaseThrottleConfigRequest
{
    // ... properties
}
```

### Option 3: Application Service (Most flexible)
```csharp
public interface IConfigurationValidationService
{
    ValidationResult ValidateExceptionPolicy(ExceptionRoutingPolicy policy);
    ValidationResult ValidateReleaseThrottle(ReleaseThrottleConfigRequest request);
}

// In controller:
var validation = _validationService.ValidateExceptionPolicy(policy);
if (!validation.IsValid)
{
    return ValidationError<ExceptionRoutingPolicy>(validation.ErrorMessage);
}
```

## Controller Refactoring Checklist

For each controller, ensure:

- [ ] Extends `ApiControllerBase` instead of `ControllerBase`
- [ ] All return types use `ApiResponse<T>` wrapper
- [ ] Success responses use `Success()` method
- [ ] Error responses use appropriate helper methods (ValidationError, NotFoundError, ServerError)
- [ ] Swagger annotations updated to reflect `ApiResponse<T>` types
- [ ] No anonymous objects (`new { ... }`) in responses
- [ ] ModelState validation uses `ValidationError()` helper
- [ ] Business validation either:
  - Extracted to validation attributes
  - Extracted to validation service  
  - Documented as TODO for future extraction

## Controllers Requiring Refactoring

### Priority 1: High-Traffic / Public APIs
1. ✅ ConfigurationController - Template created above
2. RouteConfigController - 782 lines, public API
3. SystemConfigController - Already uses ApiResponse ✅
4. ChuteAssignmentTimeoutController - Already uses ApiResponse ✅

### Priority 2: Configuration APIs
5. SensorConfigController
6. DriverConfigController
7. IoLinkageController
8. PanelConfigController

### Priority 3: Operational APIs
9. AlarmsController
10. CommunicationController
11. HealthController
12. DivertsController

### Priority 4: Simulation/Testing APIs
13. SimulationController
14. SimulationConfigController
15. OverloadPolicyController
16. LineTopologyController - Already uses ApiResponse ✅

## Testing Considerations

After refactoring, update tests to:
1. Deserialize `ApiResponse<T>` wrapper
2. Check `Success` field
3. Validate `Code` and `Message` fields
4. Extract `Data` field for actual payload

```csharp
// Example test pattern
var response = await _client.GetAsync("/api/config/exception-policy");
response.EnsureSuccessStatusCode();

var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ExceptionRoutingPolicy>>();
Assert.NotNull(apiResponse);
Assert.True(apiResponse.Success);
Assert.Equal("Ok", apiResponse.Code);
Assert.NotNull(apiResponse.Data);
Assert.True(apiResponse.Data.ExceptionChuteId > 0);
```

## Estimated Effort

- **Per Controller**: 30-60 minutes
- **Simple Controllers** (< 200 lines): 30 minutes
- **Complex Controllers** (> 500 lines): 60-90 minutes
- **Total for 13 Controllers**: 8-12 hours (1-1.5 days)

## Benefits

1. ✅ Consistent API contract across all endpoints
2. ✅ Easier client integration (predictable response format)
3. ✅ Better error handling and messaging
4. ✅ Compliance with repository constraints
5. ✅ Foundation for comprehensive testing
6. ✅ Improved API documentation accuracy

---

**Document Version**: 1.0  
**Created**: 2025-11-23  
**Status**: Implementation Template Ready
