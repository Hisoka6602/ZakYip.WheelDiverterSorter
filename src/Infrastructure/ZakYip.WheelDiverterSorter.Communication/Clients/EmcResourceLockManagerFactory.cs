using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.Communication.Clients;

/// <summary>
/// EMC资源锁管理器工厂
/// </summary>
public class EmcResourceLockManagerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EmcLockOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public EmcResourceLockManagerFactory(
        IServiceProvider serviceProvider,
        IOptions<EmcLockOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    /// <summary>
    /// 创建EMC资源锁管理器
    /// </summary>
    /// <returns>EMC资源锁管理器实例</returns>
    public IEmcResourceLockManager CreateLockManager()
    {
        return _options.CommunicationMode switch
        {
            CommunicationMode.Tcp => _serviceProvider.GetRequiredService<TcpEmcResourceLockManager>(),
            CommunicationMode.SignalR => _serviceProvider.GetRequiredService<SignalREmcResourceLockManager>(),
            CommunicationMode.Mqtt => _serviceProvider.GetRequiredService<MqttEmcResourceLockManager>(),
            _ => throw new NotSupportedException($"不支持的通信方式: {_options.CommunicationMode}")
        };
    }
}
