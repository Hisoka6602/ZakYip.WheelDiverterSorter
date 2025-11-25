using NLog;
using NLog.Web;
using Prometheus;
using System.Reflection;
using Microsoft.OpenApi.Models;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Ingress;
using Microsoft.Extensions.Caching.Memory;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Commands;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using ZakYip.WheelDiverterSorter.Execution.Concurrency;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Host.Swagger;

// Early init of NLog to allow startup and shutdown logging
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure NLog for ASP.NET Core
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

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

    // PR-37: 添加基础设施服务（安全执行器、系统时钟、日志去重）
    builder.Services.AddInfrastructureServices();

    // 添加性能监控和优化服务
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000; // 最多缓存1000个路径
    });
    builder.Services.AddMetrics();
    builder.Services.AddSingleton<SorterMetrics>();

    // 添加Prometheus指标服务
    builder.Services.AddPrometheusMetrics();

    // 添加告警服务
    builder.Services.AddAlarmService();

    // PR-33: 添加告警接收器（IAlertSink）
    builder.Services.AddAlertSinks();

    // 添加包裹生命周期日志记录服务
    builder.Services.AddParcelLifecycleLogger();

    // PR-10: 添加包裹追踪日志服务
    builder.Services.AddParcelTraceLogging();

    // PR-10: 添加日志清理服务
    builder.Services.Configure<ZakYip.WheelDiverterSorter.Observability.Tracing.LogCleanupOptions>(
        builder.Configuration.GetSection(ZakYip.WheelDiverterSorter.Observability.Tracing.LogCleanupOptions.SectionName));
    builder.Services.AddLogCleanup();

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

        // 添加驱动配置的Schema过滤器，根据当前厂商动态显示配置参数
        options.SchemaFilter<DriverConfigurationSchemaFilter>();
        
        // 添加摆轮配置的Schema过滤器，根据当前厂商动态显示配置参数
        options.SchemaFilter<WheelDiverterConfigurationSchemaFilter>();
    });

    // 注册所有配置仓储（路由、系统、驱动器、传感器、通信）
    builder.Services.AddConfigurationRepositories(builder.Configuration);

    // PR-1: 注册应用层服务
    builder.Services.AddScoped<ZakYip.WheelDiverterSorter.Host.Application.Services.ISystemConfigService,
        ZakYip.WheelDiverterSorter.Host.Application.Services.SystemConfigService>();

    // 注册分拣相关服务（路径生成、路径执行、分拣编排）
    builder.Services.AddSortingServices(builder.Configuration);

    // 使用新的驱动器服务注册（支持硬件和模拟驱动器切换）
    builder.Services.AddDriverServices(builder.Configuration);

    // 注册仿真模式提供者（用于判断当前是否为仿真模式）
    builder.Services.AddScoped<ISimulationModeProvider, SimulationModeProvider>();

    // 注册健康检查和自检服务（PR-09）
    // 默认启用自检功能，可通过配置禁用
    var enableHealthCheck = builder.Configuration.GetValue<bool>("HealthCheck:Enabled", true);
    if (enableHealthCheck)
    {
        builder.Services.AddHealthCheckServices();
        // 注册系统状态管理服务（带自检）
        builder.Services.AddSystemStateManagement(
            ZakYip.WheelDiverterSorter.Core.Enums.System.SystemState.Booting,
            enableSelfTest: true);
    }
    else
    {
        // 注册系统状态管理服务（不带自检，向后兼容）
        builder.Services.AddSystemStateManagement(ZakYip.WheelDiverterSorter.Core.Enums.System.SystemState.Ready);
    }

    // PR-33: 注册健康状态提供器
    builder.Services.AddSingleton<ZakYip.WheelDiverterSorter.Observability.Runtime.Health.IHealthStatusProvider,
        ZakYip.WheelDiverterSorter.Host.Health.HostHealthStatusProvider>();

    // 注册并发控制服务
    builder.Services.AddConcurrencyControl(builder.Configuration);

    // 装饰现有的路径执行器，添加并发控制功能
    builder.Services.DecorateWithConcurrencyControl();

    // PR-14: 注册节点健康服务
    builder.Services.AddNodeHealthServices();

    // 注册传感器服务（使用工厂模式，支持多厂商）
    builder.Services.AddSensorServices(builder.Configuration);

    // 注册RuleEngine通信服务（支持TCP/SignalR/MQTT/HTTP）
    builder.Services.AddRuleEngineCommunication(builder.Configuration);

    // 注册上游连接管理服务（自动启动Client模式连接或Server模式监听）
    builder.Services.AddUpstreamConnectionManagement(builder.Configuration);

    // 注册通信统计服务
    builder.Services.AddSingleton<CommunicationStatsService>();

    // 注册线体拓扑配置提供者
    builder.Services.AddTopologyConfiguration(builder.Configuration);

    // 注册改口功能相关服务
    builder.Services.AddSingleton<IRoutePlanRepository, InMemoryRoutePlanRepository>();
    builder.Services.AddSingleton<IRouteReplanner, RouteReplanner>();
    builder.Services.AddSingleton<ChangeParcelChuteCommandHandler>();

    // 注册路由导入导出服务
    builder.Services.AddScoped<IRouteImportExportService, RouteImportExportService>();

    // 注册中段皮带 IO 联动服务
    builder.Services.AddMiddleConveyorServices(builder.Configuration);

    // 注册仿真服务（用于 API 触发仿真）
    builder.Services.AddSimulationServices(builder.Configuration);

    // 注册告警监控后台服务
    builder.Services.AddHostedService<AlarmMonitoringWorker>();

    // 注册路由-拓扑一致性检查后台服务（启动时执行）
    builder.Services.AddHostedService<RouteTopologyConsistencyCheckWorker>();

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

// Make Program class accessible to integration tests
public partial class Program
{ }
