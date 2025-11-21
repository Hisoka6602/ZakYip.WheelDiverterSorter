namespace ZakYip.WheelDiverterSorter.Host.Services;

/// <summary>
/// 仿真服务扩展方法
/// </summary>
/// <remarks>
/// 统一注册仿真相关服务，包括场景运行器、时间线工厂等
/// </remarks>
public static class SimulationServiceExtensions
{
    /// <summary>
    /// 注册仿真服务（用于 API 触发仿真）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSimulationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var enableApiSimulation = configuration.GetValue<bool>("Simulation:EnableApiSimulation", false);
        
        if (!enableApiSimulation)
        {
            return services;
        }

        // 注册仿真服务
        services.AddSingleton<ZakYip.WheelDiverterSorter.Simulation.Services.SimulationRunner>();
        services.AddSingleton<ZakYip.WheelDiverterSorter.Simulation.Services.SimulationScenarioRunner>();
        services.AddSingleton<ZakYip.WheelDiverterSorter.Simulation.Services.ParcelTimelineFactory>();
        services.AddSingleton<ZakYip.WheelDiverterSorter.Simulation.Services.SimulationReportPrinter>();
        
        // 注册 ISimulationScenarioRunner 接口
        services.AddSingleton<ZakYip.WheelDiverterSorter.Simulation.Services.ISimulationScenarioRunner>(
            serviceProvider => serviceProvider.GetRequiredService<ZakYip.WheelDiverterSorter.Simulation.Services.SimulationScenarioRunner>());

        return services;
    }
}
