using System.Reflection;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using ZakYip.WheelDiverterSorter.Communication;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();

// 配置Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "摆轮分拣系统 API",
        Description = "直线摆轮分拣系统的Web API接口文档，提供路由配置管理和分拣调试功能",
        Contact = new OpenApiContact
        {
            Name = "技术支持",
            Email = "support@example.com"
        }
    });

    // 包含XML注释
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // 添加API标签说明
    options.TagActionsBy(api =>
    {
        if (api.GroupName != null)
        {
            return new[] { api.GroupName };
        }

        if (api.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
        {
            return new[] { controllerActionDescriptor.ControllerName };
        }

        return new[] { "其他" };
    });
    
    options.DocInclusionPredicate((name, api) => true);
});

// 配置路由数据库路径
var databasePath = builder.Configuration["RouteConfiguration:DatabasePath"] ?? "Data/routes.db";
var fullDatabasePath = Path.Combine(AppContext.BaseDirectory, databasePath);

// 确保数据目录存在
var dataDirectory = Path.GetDirectoryName(fullDatabasePath);
if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
{
    Directory.CreateDirectory(dataDirectory);
}

// 注册路由配置仓储为单例
builder.Services.AddSingleton<IRouteConfigurationRepository>(serviceProvider =>
{
    var repository = new LiteDbRouteConfigurationRepository(fullDatabasePath);
    // 初始化默认数据
    repository.InitializeDefaultData();
    return repository;
});

// 注册系统配置仓储为单例
builder.Services.AddSingleton<ISystemConfigurationRepository>(serviceProvider =>
{
    var repository = new LiteDbSystemConfigurationRepository(fullDatabasePath);
    // 初始化默认配置
    repository.InitializeDefault();
    return repository;
});

// 注册摆轮分拣相关服务
builder.Services.AddSingleton<ISwitchingPathGenerator, DefaultSwitchingPathGenerator>();

// 使用新的驱动器服务注册（支持硬件和模拟驱动器切换）
builder.Services.AddDriverServices(builder.Configuration);

// 注册并发控制服务
builder.Services.AddConcurrencyControl(builder.Configuration);

// 装饰现有的路径执行器，添加并发控制功能
builder.Services.DecorateWithConcurrencyControl();

builder.Services.AddSingleton<DebugSortService>();

// 注册传感器服务（使用工厂模式，支持多厂商）
builder.Services.AddSensorServices(builder.Configuration);

// 注册包裹检测服务
builder.Services.AddSingleton<IParcelDetectionService, ParcelDetectionService>();

// 注册RuleEngine通信服务（支持TCP/SignalR/MQTT/HTTP）
builder.Services.AddRuleEngineCommunication(builder.Configuration);

// 注册包裹分拣编排服务
builder.Services.AddSingleton<ParcelSortingOrchestrator>();

// 注册后台服务（可选）
// 取消注释以下行以启用自动传感器监听和分拣编排
// builder.Services.AddHostedService<SensorMonitoringWorker>();
// builder.Services.AddHostedService<ParcelSortingWorker>();

var app = builder.Build();

// 配置Swagger中间件
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "摆轮分拣系统 API v1");
    options.RoutePrefix = "swagger"; // 设置Swagger UI访问路径为 /swagger
    options.DocumentTitle = "摆轮分拣系统 API 文档";
    options.DefaultModelsExpandDepth(2);
    options.DefaultModelExpandDepth(2);
    options.DisplayRequestDuration();
});

app.MapControllers();

/// <summary>
/// 调试分拣接口
/// </summary>
/// <remarks>
/// 这是调试入口，用于手动触发包裹分拣流程，测试直线摆轮方案。
/// 正式环境可改成由扫码触发或供包台触发。
/// 
/// 示例请求:
/// 
///     POST /api/debug/sort
///     {
///         "parcelId": "PKG001",
///         "targetChuteId": "CHUTE-01"
///     }
/// 
/// </remarks>
/// <param name="request">包含包裹ID和目标格口ID的请求</param>
/// <param name="debugService">调试服务</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>包含执行结果和实际落格ID的响应</returns>
/// <response code="200">分拣请求已处理（成功或失败）</response>
/// <response code="400">请求参数无效</response>
app.MapPost("/api/debug/sort", async (
    DebugSortRequest request,
    DebugSortService debugService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ParcelId))
    {
        return Results.BadRequest(new { message = "包裹ID不能为空" });
    }

    if (string.IsNullOrWhiteSpace(request.TargetChuteId))
    {
        return Results.BadRequest(new { message = "目标格口ID不能为空" });
    }

    var response = await debugService.ExecuteDebugSortAsync(
        request.ParcelId,
        request.TargetChuteId,
        cancellationToken);

    return response.IsSuccess ? Results.Ok(response) : Results.Ok(response);
})
.WithName("DebugSort")
.WithDescription("调试分拣接口：接收包裹ID和目标格口ID，生成并执行摆轮路径，返回执行结果。这是调试入口，正式环境可改成由扫码/供包台触发。")
.WithTags("调试接口")
.Produces<DebugSortResponse>(200)
.Produces(400);

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }