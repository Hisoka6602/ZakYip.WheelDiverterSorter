using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 配置仓储服务扩展方法
/// </summary>
/// <remarks>
/// 统一配置数据库路径并注册所有配置仓储服务
/// </remarks>
public static class ConfigurationRepositoryServiceExtensions
{
    /// <summary>
    /// 注册所有配置仓储服务（路由、系统、驱动器、传感器、通信）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddConfigurationRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置路由数据库路径
        var databasePath = configuration["RouteConfiguration:DatabasePath"] ?? "Data/routes.db";
        var fullDatabasePath = Path.Combine(AppContext.BaseDirectory, databasePath);

        // 确保数据目录存在
        var dataDirectory = Path.GetDirectoryName(fullDatabasePath);
        if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        // 注册路由配置仓储为单例
        services.AddSingleton<IRouteConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbRouteConfigurationRepository(fullDatabasePath);
            // 初始化默认数据
            repository.InitializeDefaultData();
            return repository;
        });

        // 注册系统配置仓储为单例
        services.AddSingleton<ISystemConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbSystemConfigurationRepository(fullDatabasePath, clock);
            // 初始化默认配置，使用本地时间
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册驱动器配置仓储为单例
        services.AddSingleton<IDriverConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbDriverConfigurationRepository(fullDatabasePath);
            // 初始化默认配置
            repository.InitializeDefault();
            return repository;
        });

        // 注册摆轮配置仓储为单例
        services.AddSingleton<IWheelDiverterConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbWheelDiverterConfigurationRepository(fullDatabasePath);
            // 初始化默认配置
            repository.InitializeDefault();
            return repository;
        });

        // 注册传感器配置仓储为单例
        services.AddSingleton<ISensorConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbSensorConfigurationRepository(fullDatabasePath);
            // 初始化默认配置
            repository.InitializeDefault();
            return repository;
        });

        // 注册通信配置仓储为单例
        services.AddSingleton<ICommunicationConfigurationRepository>(serviceProvider =>
        {
            var repository = new LiteDbCommunicationConfigurationRepository(fullDatabasePath);
            // 初始化默认配置
            repository.InitializeDefault();
            return repository;
        });

        // 注册面板配置仓储为单例
        services.AddSingleton<IPanelConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbPanelConfigurationRepository(fullDatabasePath);
            // 初始化默认配置，使用本地时间
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册 IO 联动配置仓储为单例
        services.AddSingleton<IIoLinkageConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbIoLinkageConfigurationRepository(fullDatabasePath);
            // 初始化默认配置，使用本地时间
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // PR-1: 注册线体拓扑配置仓储为单例
        services.AddSingleton<ILineTopologyRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbLineTopologyRepository(fullDatabasePath, clock);
            // 初始化默认配置，使用本地时间
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // PR-1: 注册摆轮硬件绑定配置仓储为单例
        services.AddSingleton<IWheelBindingsRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbWheelBindingsRepository(fullDatabasePath, clock);
            // 初始化默认配置，使用本地时间
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // PR-1: 注册路径时间预估服务为单例
        services.AddSingleton<IRouteTimingEstimator>(serviceProvider =>
        {
            var topologyRepository = serviceProvider.GetRequiredService<ILineTopologyRepository>();
            return new RouteTimingEstimator(topologyRepository);
        });

        // 注册日志配置仓储为单例
        services.AddSingleton<ILoggingConfigurationRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbLoggingConfigurationRepository(fullDatabasePath, clock);
            // 初始化默认配置，使用本地时间
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        // 注册格口路径拓扑配置仓储为单例
        services.AddSingleton<IChutePathTopologyRepository>(serviceProvider =>
        {
            var clock = serviceProvider.GetRequiredService<ISystemClock>();
            var repository = new LiteDbChutePathTopologyRepository(fullDatabasePath, clock);
            // 初始化默认配置，使用本地时间
            repository.InitializeDefault(clock.LocalNow);
            return repository;
        });

        return services;
    }
}
