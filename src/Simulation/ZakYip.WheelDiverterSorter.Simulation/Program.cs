using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Observability;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Services;
using ZakYip.WheelDiverterSorter.Simulation.Demo;

// 检查是否运行策略实验演示 / Check if running strategy experiment demo
if (args.Length > 0 && args[0] == "strategy-experiment-demo")
{
    await StrategyExperimentDemo.RunDemoAsync();
    return 0;
}

// 创建Host
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        // 添加仿真专用配置文件
        config.AddJsonFile("appsettings.Simulation.json", optional: false, reloadOnChange: false);
        
        // 支持命令行参数覆盖配置
        config.AddCommandLine(args);
    })
    .ConfigureServices((context, services) =>
    {
        // 注册仿真配置
        services.Configure<SimulationOptions>(context.Configuration.GetSection("Simulation"));

        // 注册线体拓扑配置提供者
        var topologyConfigPath = context.Configuration["Topology:ConfigPath"] ?? "simulation-config/topology.json";
        var fullTopologyPath = Path.Combine(AppContext.BaseDirectory, topologyConfigPath);
        
        if (File.Exists(fullTopologyPath))
        {
            services.AddSingleton<ILineTopologyConfigProvider>(new JsonLineTopologyConfigProvider(fullTopologyPath));
        }
        else
        {
            // 如果找不到配置文件，使用默认配置
            services.AddSingleton<ILineTopologyConfigProvider>(new DefaultLineTopologyConfigProvider());
        }

        // 注册路由配置仓储（内存版本，用于路径生成）
        services.AddSingleton<IRouteConfigurationRepository>(sp =>
        {
            var repository = new InMemoryRouteConfigurationRepository();
            repository.InitializeDefaultData();
            return repository;
        });

        // 注册路径生成器（使用默认实现）
        services.AddSingleton<ISwitchingPathGenerator, DefaultSwitchingPathGenerator>();

        // 注册模拟路径执行器
        services.AddSingleton<ISwitchingPathExecutor, MockSwitchingPathExecutor>();

        // 注册 IO 联动服务（仿真模式）
        services.AddSingleton<ZakYip.WheelDiverterSorter.Drivers.Abstractions.IIoLinkageDriver, 
            ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated.SimulatedIoLinkageDriver>();
        services.AddSingleton<IIoLinkageCoordinator, DefaultIoLinkageCoordinator>();

        // 注册模拟RuleEngineClient
        services.AddSingleton<IRuleEngineClient>(sp =>
        {
            var logger = sp.GetService<ILogger<InMemoryRuleEngineClient>>();
            var options = sp.GetRequiredService<IOptions<SimulationOptions>>().Value;
            
            // 根据分拣模式创建不同的格口分配函数
            Func<long, int> chuteAssignmentFunc = options.SortingMode switch
            {
                "FixedChute" => CreateFixedChuteAssignmentFunc(options),
                "RoundRobin" => CreateRoundRobinAssignmentFunc(options),
                "Formal" => CreateFormalAssignmentFunc(options),
                _ => throw new InvalidOperationException($"不支持的分拣模式: {options.SortingMode}")
            };

            return new InMemoryRuleEngineClient(chuteAssignmentFunc, logger);
        });

        // 注册Prometheus指标服务
        services.AddSingleton<PrometheusMetrics>();

        // 注册仿真服务
        services.AddSingleton<ParcelTimelineFactory>();
        services.AddSingleton<SimulationReportPrinter>();
        services.AddSingleton<SimulationRunner>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        
        // 根据配置设置日志级别
        var simulationOptions = context.Configuration.GetSection("Simulation").Get<SimulationOptions>();
        if (simulationOptions?.IsEnableVerboseLogging == true)
        {
            logging.SetMinimumLevel(LogLevel.Debug);
        }
        else
        {
            logging.SetMinimumLevel(LogLevel.Information);
        }
    })
    .Build();

// 启动Prometheus metrics端点（后台线程）
var options = host.Services.GetRequiredService<IOptions<SimulationOptions>>().Value;
Task? metricsServerTask = null;
CancellationTokenSource? metricsCts = null;

if (options.IsLongRunMode)
{
    metricsCts = new CancellationTokenSource();
    metricsServerTask = Task.Run(() => StartMetricsServer(metricsCts.Token), metricsCts.Token);
    Console.WriteLine("Prometheus metrics 端点已启动: http://localhost:9091/metrics");
}

// 运行仿真
try
{
    var runner = host.Services.GetRequiredService<SimulationRunner>();
    
    var statistics = await runner.RunAsync();
    
    if (options.IsPauseAtEnd)
    {
        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    
    return 0;
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "仿真执行失败");
    
    Console.WriteLine();
    Console.WriteLine($"错误: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    
    return 1;
}
finally
{
    // 停止metrics服务器
    if (metricsCts != null)
    {
        metricsCts.Cancel();
        if (metricsServerTask != null)
        {
            try
            {
                await metricsServerTask;
            }
            catch (OperationCanceledException)
            {
                // 预期的取消
            }
        }
    }
}

// 启动Prometheus metrics HTTP服务器
static void StartMetricsServer(CancellationToken cancellationToken)
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls("http://localhost:9091");
    builder.Logging.ClearProviders(); // 不打印metrics服务器的日志
    
    var app = builder.Build();
    app.UseRouting();
    app.MapMetrics(); // 暴露 /metrics 端点
    
    try
    {
        app.Run();
    }
    catch (OperationCanceledException)
    {
        // 预期的取消
    }
}

// 格口分配函数工厂方法

static Func<long, int> CreateFixedChuteAssignmentFunc(SimulationOptions options)
{
    if (options.FixedChuteIds == null || options.FixedChuteIds.Count == 0)
    {
        throw new InvalidOperationException("FixedChute 模式需要配置 FixedChuteIds");
    }

    var chuteIds = options.FixedChuteIds.ToArray();
    var random = new Random();
    
    return _ => (int)chuteIds[random.Next(chuteIds.Length)];
}

static Func<long, int> CreateRoundRobinAssignmentFunc(SimulationOptions options)
{
    if (options.FixedChuteIds == null || options.FixedChuteIds.Count == 0)
    {
        throw new InvalidOperationException("RoundRobin 模式需要配置 FixedChuteIds");
    }

    var chuteIds = options.FixedChuteIds.ToArray();
    var index = 0;
    var lockObj = new object();
    
    return _ =>
    {
        lock (lockObj)
        {
            var chuteId = (int)chuteIds[index];
            index = (index + 1) % chuteIds.Length;
            return chuteId;
        }
    };
}

static Func<long, int> CreateFormalAssignmentFunc(SimulationOptions options)
{
    // Formal 模式下，使用包裹ID的哈希来模拟规则引擎的决策
    // 在实际场景中，这里会调用真实的规则引擎
    if (options.FixedChuteIds == null || options.FixedChuteIds.Count == 0)
    {
        throw new InvalidOperationException("Formal 模式需要配置 FixedChuteIds 作为可用格口列表");
    }

    var chuteIds = options.FixedChuteIds.ToArray();
    
    return parcelId =>
    {
        // 使用包裹ID的哈希来决定格口
        var hash = parcelId.GetHashCode();
        var index = Math.Abs(hash) % chuteIds.Length;
        return (int)chuteIds[index];
    };
}

/// <summary>
/// 内存路由配置仓储（用于仿真）
/// </summary>
internal class InMemoryRouteConfigurationRepository : IRouteConfigurationRepository
{
    private readonly Dictionary<int, ChuteRouteConfiguration> _configurations = new();
    private readonly object _lockObject = new();

    public void InitializeDefaultData()
    {
        // 添加默认的路由配置用于仿真
        // 假设有10个格口，每个格口有相应的路由配置
        lock (_lockObject)
        {
            _configurations.Clear();
            
            // 不同格口使用不同的输送线长度（新需求：每个输送线的长度不一致）
            var segmentLengths = new[] { 800, 1200, 1500, 900, 1100, 1300, 1000, 1400, 950, 1250 };
            
            for (int chuteId = 1; chuteId <= 10; chuteId++)
            {
                var segmentLength = segmentLengths[(chuteId - 1) % segmentLengths.Length];
                
                _configurations[chuteId] = new ChuteRouteConfiguration
                {
                    ChuteId = chuteId,
                    ChuteName = $"仿真格口{chuteId}",
                    DiverterConfigurations = new List<DiverterConfigurationEntry>
                    {
                        new DiverterConfigurationEntry
                        {
                            DiverterId = chuteId,
                            TargetDirection = DiverterDirection.Right, // 默认右转
                            SequenceNumber = 1,
                            SegmentLengthMm = segmentLength, // 每个格口的输送线长度不同
                            SegmentSpeedMmPerSecond = 500,
                            SegmentToleranceTimeMs = 2000 // 2秒容差
                        }
                    },
                    BeltSpeedMmPerSecond = 500,
                    BeltLengthMm = segmentLength,
                    IsEnabled = true
                };
            }
        }
    }

    public ChuteRouteConfiguration? GetByChuteId(int chuteId)
    {
        lock (_lockObject)
        {
            return _configurations.TryGetValue(chuteId, out var config) ? config : null;
        }
    }

    public IEnumerable<ChuteRouteConfiguration> GetAllEnabled()
    {
        lock (_lockObject)
        {
            return _configurations.Values.Where(c => c.IsEnabled).ToList();
        }
    }

    public void Upsert(ChuteRouteConfiguration configuration)
    {
        lock (_lockObject)
        {
            _configurations[configuration.ChuteId] = configuration;
        }
    }

    public bool Delete(int chuteId)
    {
        lock (_lockObject)
        {
            return _configurations.Remove(chuteId);
        }
    }
}
