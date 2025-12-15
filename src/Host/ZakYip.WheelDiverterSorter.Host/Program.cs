using NLog;
using NLog.Web;
using Prometheus;
using System.Reflection;
using Microsoft.OpenApi.Models;
using ZakYip.WheelDiverterSorter.Host.Swagger;
using ZakYip.WheelDiverterSorter.Host.Services.Extensions;

// Early init of NLog to allow startup and shutdown logging
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure NLog for ASP.NET Core
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

#if RELEASE
    // 在 Release 模式下启用 Windows Service 支持
    // Enable Windows Service support in Release mode
    // 这允许应用程序作为 Windows Service 运行，同时仍然支持控制台模式
    // This allows the application to run as a Windows Service while still supporting console mode
    builder.Host.UseWindowsService();
#endif

    // 添加服务到容器
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // 配置枚举序列化为字符串，使Swagger和API调用更友好
            // allowIntegerValues: true 允许反序列化时接受数字（向后兼容）
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter(
                    namingPolicy: null,
                    allowIntegerValues: true));
        });

    // PR-H1: 使用 Host 层薄包装的 DI 入口注册所有 WheelDiverterSorter 服务
    // 此方法内部调用 Application 层的 AddWheelDiverterSorter()，然后添加 Host 特定服务
    // （健康检查、系统状态管理、健康状态提供器、命令处理器、后台工作服务）
    builder.Services.AddWheelDiverterSorterHost(builder.Configuration);

    // 注意：健康检查端点通过 HealthController 中的自定义 Action 方法实现，
    // 无需注册 ASP.NET Core 标准健康检查服务

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

        // 启用注解支持 - 支持 [SwaggerOperation]、[SwaggerSchema] 等注解
        options.EnableAnnotations();

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

        // 添加IO驱动配置的Schema过滤器，根据当前厂商动态显示配置参数
        options.SchemaFilter<IoDriverConfigurationSchemaFilter>();

        // 添加摆轮配置的Schema过滤器，根据当前厂商动态显示配置参数
        options.SchemaFilter<WheelDiverterConfigurationSchemaFilter>();

        // 添加摆轮配置的文档过滤器，根据当前厂商动态显示/隐藏API端点
        options.DocumentFilter<WheelDiverterControllerDocumentFilter>();
    });

    var app = builder.Build();

    // 配置Prometheus指标中间件
    // Configure Prometheus metrics middleware
    app.UseHttpMetrics(); // 自动收集HTTP请求指标
    app.MapMetrics(); // 暴露 /metrics 端点

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

    app.Run();
}
catch (Exception exception)
{
    // NLog: catch setup errors
    logger.Error(exception, "Stopped program because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    LogManager.Shutdown();
}
