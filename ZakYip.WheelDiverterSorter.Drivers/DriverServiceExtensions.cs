using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Leadshine;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器服务注册扩展
/// </summary>
public static class DriverServiceExtensions
{
    /// <summary>
    /// 添加驱动器服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDriverServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 绑定配置
        var options = new DriverOptions();
        configuration.GetSection("Driver").Bind(options);
        services.AddSingleton(options);

        if (options.UseHardwareDriver)
        {
            // 使用硬件驱动器
            services.AddSingleton<ISwitchingPathExecutor>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<HardwareSwitchingPathExecutor>>();
                var diverters = CreateLeadshineDiverters(sp, options.Leadshine);
                return new HardwareSwitchingPathExecutor(logger, diverters);
            });
        }
        else
        {
            // 使用模拟驱动器
            services.AddSingleton<ISwitchingPathExecutor, MockSwitchingPathExecutor>();
        }

        return services;
    }

    /// <summary>
    /// 创建雷赛摆轮控制器列表
    /// </summary>
    private static List<IDiverterController> CreateLeadshineDiverters(
        IServiceProvider sp,
        LeadshineOptions options)
    {
        var diverters = new List<IDiverterController>();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        foreach (var configDto in options.Diverters)
        {
            var config = new LeadshineDiverterConfig
            {
                DiverterId = configDto.DiverterId,
                OutputStartBit = configDto.OutputStartBit,
                FeedbackInputBit = configDto.FeedbackInputBit
            };

            var logger = loggerFactory.CreateLogger<LeadshineDiverterController>();
            var controller = new LeadshineDiverterController(logger, options.CardNo, config);
            diverters.Add(controller);
        }

        return diverters;
    }
}
