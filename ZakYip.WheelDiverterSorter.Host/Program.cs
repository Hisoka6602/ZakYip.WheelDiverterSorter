using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
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
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 配置枚举序列化为字符串，使Swagger和API调用更友好
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// 添加性能监控和优化服务
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // 最多缓存1000个路径
});
builder.Services.AddMetrics();
builder.Services.AddSingleton<SorterMetrics>();

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
    
    // 配置枚举在Swagger中显示为字符串而不是数字
    options.UseInlineDefinitionsForEnums();
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

// 注册驱动器配置仓储为单例
builder.Services.AddSingleton<IDriverConfigurationRepository>(serviceProvider =>
{
    var repository = new LiteDbDriverConfigurationRepository(fullDatabasePath);
    // 初始化默认配置
    repository.InitializeDefault();
    return repository;
});

// 注册传感器配置仓储为单例
builder.Services.AddSingleton<ISensorConfigurationRepository>(serviceProvider =>
{
    var repository = new LiteDbSensorConfigurationRepository(fullDatabasePath);
    // 初始化默认配置
    repository.InitializeDefault();
    return repository;
});

// 注册通信配置仓储为单例
builder.Services.AddSingleton<ICommunicationConfigurationRepository>(serviceProvider =>
{
    var repository = new LiteDbCommunicationConfigurationRepository(fullDatabasePath);
    // 初始化默认配置
    repository.InitializeDefault();
    return repository;
});

// 注册摆轮分拣相关服务
// 首先注册基础路径生成器
builder.Services.AddSingleton<DefaultSwitchingPathGenerator>();

// 使用装饰器模式添加缓存功能（可选，通过配置启用）
var enablePathCaching = builder.Configuration.GetValue<bool>("Performance:EnablePathCaching", true);
if (enablePathCaching)
{
    builder.Services.AddSingleton<ISwitchingPathGenerator>(serviceProvider =>
    {
        var innerGenerator = serviceProvider.GetRequiredService<DefaultSwitchingPathGenerator>();
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<CachedSwitchingPathGenerator>>();
        return new CachedSwitchingPathGenerator(innerGenerator, cache, logger);
    });
}
else
{
    builder.Services.AddSingleton<ISwitchingPathGenerator>(serviceProvider =>
        serviceProvider.GetRequiredService<DefaultSwitchingPathGenerator>());
}

// 注册优化的分拣服务
builder.Services.AddSingleton<OptimizedSortingService>();

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

// 注册通信统计服务
builder.Services.AddSingleton<CommunicationStatsService>();

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

app.MapPost("/api/debug/sort", async (
    DebugSortRequest request,
    DebugSortService debugService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ParcelId))
    {
        return Results.BadRequest(new { message = "包裹ID不能为空" });
    }

    if (request.TargetChuteId <= 0)
    {
        return Results.BadRequest(new { message = "目标格口ID必须大于0" });
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