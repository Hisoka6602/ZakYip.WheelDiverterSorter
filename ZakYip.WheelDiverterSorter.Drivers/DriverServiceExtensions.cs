using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Leadshine;
using ZakYip.WheelDiverterSorter.Drivers.Simulated;
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
                var drivers = CreateLeadshineWheelDiverterDrivers(sp, options.Leadshine);
                return new HardwareSwitchingPathExecutor(logger, drivers);
            });

            // 注册雷赛 IO 联动驱动
            services.AddSingleton<IIoLinkageDriver>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<LeadshineIoLinkageDriver>>();
                var emcController = CreateEmcController(sp, options.Leadshine.CardNo);
                return new LeadshineIoLinkageDriver(logger, emcController);
            });
        }
        else
        {
            // 使用模拟驱动器
            services.AddSingleton<ISwitchingPathExecutor, MockSwitchingPathExecutor>();

            // 注册仿真 IO 联动驱动
            services.AddSingleton<IIoLinkageDriver, SimulatedIoLinkageDriver>();
        }

        // 注册 IO 联动协调器（不管是硬件还是仿真模式都需要）
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();

        return services;
    }

    /// <summary>
    /// 创建雷赛 EMC 控制器
    /// </summary>
    private static IEmcController CreateEmcController(
        IServiceProvider sp,
        ushort cardNo)
    {
        var logger = sp.GetRequiredService<ILogger<LeadshineEmcController>>();
        var controller = new LeadshineEmcController(logger, cardNo);
        
        // 初始化控制器（同步执行）
        var initTask = controller.InitializeAsync();
        initTask.Wait();
        
        if (!initTask.Result)
        {
            logger.LogWarning("EMC 控制器初始化失败，IO 联动功能可能无法正常工作");
        }
        
        return controller;
    }

    /// <summary>
    /// 创建雷赛摆轮驱动器列表（封装底层控制器）
    /// </summary>
    private static List<IWheelDiverterDriver> CreateLeadshineWheelDiverterDrivers(
        IServiceProvider sp,
        LeadshineOptions options)
    {
        var drivers = new List<IWheelDiverterDriver>();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        foreach (var configDto in options.Diverters)
        {
            var config = new LeadshineDiverterConfig
            {
                DiverterId = configDto.DiverterId,
                DiverterName = configDto.DiverterName,
                ConnectedConveyorLengthMm = configDto.ConnectedConveyorLengthMm,
                ConnectedConveyorSpeedMmPerSec = configDto.ConnectedConveyorSpeedMmPerSec,
                DiverterSpeedMmPerSec = configDto.DiverterSpeedMmPerSec,
                OutputStartBit = configDto.OutputStartBit,
                FeedbackInputBit = configDto.FeedbackInputBit
            };

            // 创建底层控制器
            var controllerLogger = loggerFactory.CreateLogger<LeadshineDiverterController>();
            var controller = new LeadshineDiverterController(controllerLogger, options.CardNo, config);

            // 封装为高层驱动器
            var driverLogger = loggerFactory.CreateLogger<RelayWheelDiverterDriver>();
            var driver = new RelayWheelDiverterDriver(driverLogger, controller);
            drivers.Add(driver);
        }

        return drivers;
    }
}
