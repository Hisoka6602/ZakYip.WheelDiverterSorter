using NLog;
using NLog.Web;
using Prometheus;
using System.Reflection;
using Microsoft.OpenApi.Models;
using ZakYip.WheelDiverterSorter.Host.Swagger;
using ZakYip.WheelDiverterSorter.Host.Services.Extensions;
using ZakYip.WheelDiverterSorter.Host.Models;

// 日志刷新超时时间（秒）- 确保异常日志在进程终止前写入磁盘
const int LogFlushTimeoutSeconds = 5;

// PR-FIX-1053: 确保日志目录存在（Windows Service 可能没有权限自动创建）
var baseDirectory = AppContext.BaseDirectory;
var logsDirectory = Path.Combine(baseDirectory, "logs");
try
{
    if (!Directory.Exists(logsDirectory))
    {
        Directory.CreateDirectory(logsDirectory);
        Console.WriteLine($"[Startup] 创建日志目录: {logsDirectory}");
    }
    Console.WriteLine($"[Startup] 日志目录: {logsDirectory}");
    Console.WriteLine($"[Startup] 工作目录: {Environment.CurrentDirectory}");
    Console.WriteLine($"[Startup] 基础目录: {baseDirectory}");
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] 警告: 无法创建日志目录 {logsDirectory}: {ex.Message}");
    // 继续执行，让 NLog 尝试使用备用位置
}

// Early init of NLog to allow startup and shutdown logging
// PR-FIX-1053: 检查 nlog.config 是否存在，避免文件缺失导致程序崩溃
var nlogConfigPath = Path.Combine(baseDirectory, "nlog.config");
if (!File.Exists(nlogConfigPath))
{
    Console.WriteLine($"[FATAL] nlog.config 文件不存在: {nlogConfigPath}");
    Console.WriteLine($"[FATAL] 程序无法启动，请确保 nlog.config 文件存在于应用程序目录中");
    throw new FileNotFoundException($"nlog.config 文件不存在: {nlogConfigPath}");
}

var logger = LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath).GetCurrentClassLogger();

// 记录启动开始，包含关键环境信息
logger.Info("========== 应用程序启动开始 ==========");
logger.Info($"应用程序版本: {Assembly.GetEntryAssembly()?.GetName().Version}");
logger.Info($"基础目录: {baseDirectory}");
logger.Info($"工作目录: {Environment.CurrentDirectory}");
logger.Info($"日志目录: {logsDirectory}");
logger.Info($"进程 ID: {Environment.ProcessId}");
logger.Info($".NET 版本: {Environment.Version}");
logger.Info($"操作系统: {Environment.OSVersion}");
logger.Info($"是否 64 位进程: {Environment.Is64BitProcess}");
logger.Info($"是否自包含部署: {string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT"))}");
logger.Info("=========================================");

// 强制输出到控制台以便诊断（即使在 Windows Service 模式下）
Console.WriteLine($"[Startup] NLog 已初始化，日志文件应该在: {logsDirectory}");
Console.WriteLine($"[Startup] 检查以下位置的日志文件:");
Console.WriteLine($"  - {Path.Combine(logsDirectory, $"startup-{DateTime.Now:yyyy-MM-dd}.log")}");
Console.WriteLine($"  - {Path.Combine(logsDirectory, $"error-{DateTime.Now:yyyy-MM-dd}.log")}");
Console.WriteLine($"  - {Path.Combine(logsDirectory, $"internal-nlog-{DateTime.Now:yyyy-MM-dd}.log")}");

try
{
    logger.Info("开始构建 WebApplication Builder...");
    var builder = WebApplication.CreateBuilder(args);

    logger.Info("WebApplication Builder 构建成功");
    
    // 读取 MiniApi 配置 - Load MiniApi configuration
    logger.Info("加载 MiniApi 配置...");
    var miniApiOptions = builder.Configuration.GetSection("MiniApi").Get<MiniApiOptions>() ?? new MiniApiOptions();
    logger.Info($"MiniApi 配置加载完成 - EnableSwagger: {miniApiOptions.EnableSwagger}");
    
    // 配置服务监听地址 - Configure service listen addresses
    // 如果配置中没有指定URL，将使用MiniApiOptions的默认值（http://localhost:5000）
    // If no URLs are specified in configuration, the default value from MiniApiOptions will be used (http://localhost:5000)
    if (miniApiOptions.Urls.Length > 0)
    {
        builder.WebHost.UseUrls(miniApiOptions.Urls);
        logger.Info($"API服务将监听以下地址 - API service will listen on: {string.Join(", ", miniApiOptions.Urls)}");
    }

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

    logger.Info("开始注册 WheelDiverterSorter 服务...");
    // PR-H1: 使用 Host 层薄包装的 DI 入口注册所有 WheelDiverterSorter 服务
    // 此方法内部调用 Application 层的 AddWheelDiverterSorter()，然后添加 Host 特定服务
    // （健康检查、系统状态管理、健康状态提供器、命令处理器、后台工作服务）
    builder.Services.AddWheelDiverterSorterHost(builder.Configuration);
    logger.Info("WheelDiverterSorter 服务注册完成");

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

    logger.Info("开始构建 WebApplication...");
    var app = builder.Build();
    logger.Info("WebApplication 构建成功，准备配置中间件...");

    // 配置Prometheus指标中间件
    // Configure Prometheus metrics middleware
    app.UseHttpMetrics(); // 自动收集HTTP请求指标
    app.MapMetrics(); // 暴露 /metrics 端点

    // 根据配置决定是否启用Swagger - Enable Swagger based on configuration
    if (miniApiOptions.EnableSwagger)
    {
        logger.Info("Swagger文档已启用 - Swagger documentation enabled");
        
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
    }
    else
    {
        logger.Info("Swagger文档已禁用 - Swagger documentation disabled");
    }

    app.MapControllers();
    
    logger.Info("中间件配置完成，开始运行应用程序...");
    logger.Info($"监听地址: {string.Join(", ", miniApiOptions.Urls)}");
    logger.Info("========== 应用程序启动完成 ==========");

    app.Run();
}
catch (Exception exception)
{
    // NLog: catch setup errors
    // 记录详细的异常信息（NLog 配置会自动包含所有内部异常和堆栈跟踪）
    logger.Fatal(exception, "========== 应用程序启动失败 ==========");
    logger.Fatal("请检查上述错误信息，并参考 docs/WINDOWS_SERVICE_DEPLOYMENT.md 故障排查章节");
    
    // 确保日志刷新到磁盘
    LogManager.Flush(TimeSpan.FromSeconds(LogFlushTimeoutSeconds));
    
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    LogManager.Shutdown();
}

// 使 Program 类对集成测试可见
// Make Program class accessible to integration tests
// 参考：https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
public partial class Program { }
