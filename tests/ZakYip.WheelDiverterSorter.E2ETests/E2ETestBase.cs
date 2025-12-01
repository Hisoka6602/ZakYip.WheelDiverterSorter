using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// E2E测试的基类，提供通用设置和工具方法
/// </summary>
public class E2ETestBase : IClassFixture<E2ETestFactory>, IDisposable
{
    protected readonly E2ETestFactory Factory;
    protected readonly HttpClient Client;
    protected readonly IServiceScope Scope;

    // 通用服务
    protected readonly ISwitchingPathGenerator PathGenerator;
    protected readonly ISwitchingPathExecutor PathExecutor;
    protected readonly IRouteConfigurationRepository RouteRepository;
    protected readonly ISystemConfigurationRepository SystemRepository;

    public E2ETestBase(E2ETestFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Scope = factory.Services.CreateScope();

        // 重置模拟调用记录，确保此测试具有干净的状态
        Factory.ResetMockInvocations();

        // 获取通用服务
        PathGenerator = Scope.ServiceProvider.GetRequiredService<ISwitchingPathGenerator>();
        PathExecutor = Scope.ServiceProvider.GetRequiredService<ISwitchingPathExecutor>();
        RouteRepository = Scope.ServiceProvider.GetRequiredService<IRouteConfigurationRepository>();
        SystemRepository = Scope.ServiceProvider.GetRequiredService<ISystemConfigurationRepository>();
    }

    /// <summary>
    /// 为测试设置默认路由配置的辅助方法
    /// </summary>
    protected void SetupDefaultRouteConfiguration()
    {
        try
        {
            // 清除现有配置
            var allConfigs = RouteRepository.GetAllEnabled();
            foreach (var config in allConfigs)
            {
                RouteRepository.Delete(config.ChuteId);
            }

            // 添加测试配置
            RouteRepository.InitializeDefaultData();
        }
        catch (Exception)
        {
            // 如果初始化失败（例如由于重复键），
            // 数据已经存在，我们可以继续
            // 这可能发生在多个测试共享同一个工厂/数据库时
        }
    }

    /// <summary>
    /// 创建测试路由配置的辅助方法
    /// </summary>
    protected ChuteRouteConfiguration CreateTestRouteConfig(int chuteId, params (int diverterId, DiverterDirection direction, int sequence)[] diverters)
    {
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            ChuteName = $"Test Chute {chuteId}",
            DiverterConfigurations = diverters.Select(d => new DiverterConfigurationEntry
            {
                DiverterId = d.diverterId,
                TargetDirection = d.direction,
                SequenceNumber = d.sequence
            }).ToList(),
            BeltSpeedMmPerSecond = 1000.0,
            BeltLengthMm = 10000.0,
            ToleranceTimeMs = 2000,
            IsEnabled = true
        };

        RouteRepository.Upsert(config);
        return config;
    }

    public virtual void Dispose()
    {
        Scope?.Dispose();
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
