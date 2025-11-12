using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Configuration;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Sensors;
using ZakYip.WheelDiverterSorter.Ingress.Services;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();

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

// 注册摆轮分拣相关服务
builder.Services.AddSingleton<ISwitchingPathGenerator, DefaultSwitchingPathGenerator>();
builder.Services.AddSingleton<ISwitchingPathExecutor, MockSwitchingPathExecutor>();
builder.Services.AddSingleton<DebugSortService>();

// 注册传感器和包裹检测服务
builder.Services.AddSingleton<ISensor>(sp => new MockPhotoelectricSensor("SENSOR_PE_01"));
builder.Services.AddSingleton<ISensor>(sp => new MockLaserSensor("SENSOR_LASER_01"));
builder.Services.AddSingleton<IParcelDetectionService, ParcelDetectionService>();

// 注册传感器监听后台服务（可选）
// 取消注释以下行以启用自动传感器监听
// builder.Services.AddHostedService<SensorMonitoringWorker>();

var app = builder.Build();

app.MapControllers();

/// <summary>
/// 调试分拣接口
/// </summary>
/// <remarks>
/// 这是调试入口，用于手动触发包裹分拣流程，测试直线摆轮方案。
/// 正式环境可改成由扫码触发或供包台触发。
/// </remarks>
/// <param name="request">包含包裹ID和目标格口ID的请求</param>
/// <param name="debugService">调试服务</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>包含执行结果和实际落格ID的响应</returns>
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
.WithTags("调试接口");

app.Run();