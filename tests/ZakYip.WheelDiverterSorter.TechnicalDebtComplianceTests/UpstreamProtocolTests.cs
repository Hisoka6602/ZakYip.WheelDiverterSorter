using System.Reflection;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-UPSTREAM01: 确保不存在 HTTP 上游协议实现
/// </summary>
/// <remarks>
/// <para>这些测试用于确保 HTTP 协议实现已被彻底移除：</para>
/// <list type="number">
///   <item>Communication/Clients 目录下不存在 Http 相关类型</item>
///   <item>CommunicationMode 枚举不包含 Http 值</item>
///   <item>RuleEngineConnectionOptions 不包含 HttpApi 属性</item>
/// </list>
/// </remarks>
public class UpstreamProtocolTests
{
    /// <summary>
    /// 获取 Communication 程序集
    /// </summary>
    private static readonly Assembly CommunicationAssembly = typeof(ZakYip.WheelDiverterSorter.Communication.CommunicationServiceExtensions).Assembly;

    /// <summary>
    /// 获取 Core 程序集
    /// </summary>
    private static readonly Assembly CoreAssembly = typeof(CommunicationMode).Assembly;

    [Fact]
    public void Communication_Clients_ShouldNotHaveHttpTypes()
    {
        // Arrange - 查找 Communication.Clients 命名空间下的所有类型
        var clientTypes = CommunicationAssembly
            .GetTypes()
            .Where(t => t.Namespace?.Contains("Communication.Clients") == true)
            .Where(t => t.Name.Contains("Http", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 Http 相关类型
        Assert.Empty(clientTypes);
    }

    [Fact]
    public void Communication_Gateways_ShouldNotHaveHttpTypes()
    {
        // Arrange - 查找 Communication.Gateways 命名空间下的所有类型
        var gatewayTypes = CommunicationAssembly
            .GetTypes()
            .Where(t => t.Namespace?.Contains("Communication.Gateways") == true)
            .Where(t => t.Name.Contains("Http", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 Http 相关类型
        Assert.Empty(gatewayTypes);
    }

    [Fact]
    public void Communication_Configuration_ShouldNotHaveHttpOptions()
    {
        // Arrange - 查找 Communication.Configuration 命名空间下的 HttpOptions 类型
        var httpOptionsTypes = CommunicationAssembly
            .GetTypes()
            .Where(t => t.Namespace?.Contains("Communication.Configuration") == true)
            .Where(t => t.Name == "HttpOptions")
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 HttpOptions 类型
        Assert.Empty(httpOptionsTypes);
    }

    [Fact]
    public void CommunicationMode_ShouldNotContainHttp()
    {
        // Arrange - 获取 CommunicationMode 枚举的所有值
        var enumValues = Enum.GetNames(typeof(CommunicationMode));

        // Assert - 不应包含 Http 值
        Assert.DoesNotContain("Http", enumValues);
    }

    [Fact]
    public void CommunicationMode_ShouldOnlyContainTcpSignalRMqtt()
    {
        // Arrange - 获取 CommunicationMode 枚举的所有值
        var enumValues = Enum.GetNames(typeof(CommunicationMode));
        var allowedValues = new[] { "Tcp", "SignalR", "Mqtt" };

        // Assert - 应只包含 Tcp, SignalR, Mqtt
        foreach (var value in enumValues)
        {
            Assert.Contains(value, allowedValues);
        }
    }

    [Fact]
    public void RuleEngineConnectionOptions_ShouldNotHaveHttpApiProperty()
    {
        // Arrange - 获取 RuleEngineConnectionOptions 类型
        var optionsType = CommunicationAssembly.GetType("ZakYip.WheelDiverterSorter.Communication.Configuration.RuleEngineConnectionOptions");
        Assert.NotNull(optionsType);

        // Assert - 不应存在 HttpApi 属性
        var httpApiProperty = optionsType.GetProperty("HttpApi");
        Assert.Null(httpApiProperty);
    }

    [Fact]
    public void RuleEngineConnectionOptions_ShouldNotHaveHttpProperty()
    {
        // Arrange - 获取 RuleEngineConnectionOptions 类型
        var optionsType = CommunicationAssembly.GetType("ZakYip.WheelDiverterSorter.Communication.Configuration.RuleEngineConnectionOptions");
        Assert.NotNull(optionsType);

        // Assert - 不应存在 Http 属性
        var httpProperty = optionsType.GetProperty("Http");
        Assert.Null(httpProperty);
    }

    [Fact]
    public void CommunicationConfiguration_ShouldNotHaveHttpApiProperty()
    {
        // Arrange - 获取 CommunicationConfiguration 类型
        var configType = CoreAssembly.GetType("ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models.CommunicationConfiguration");
        Assert.NotNull(configType);

        // Assert - 不应存在 HttpApi 属性
        var httpApiProperty = configType.GetProperty("HttpApi");
        Assert.Null(httpApiProperty);
    }

    [Fact]
    public void CommunicationConfiguration_ShouldNotHaveHttpConfigProperty()
    {
        // Arrange - 获取 CommunicationConfiguration 类型
        var configType = CoreAssembly.GetType("ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models.CommunicationConfiguration");
        Assert.NotNull(configType);

        // Assert - 不应存在 Http 属性
        var httpProperty = configType.GetProperty("Http");
        Assert.Null(httpProperty);
    }

    [Fact]
    public void Core_ShouldNotHaveHttpConfigType()
    {
        // Arrange - 查找 Core 程序集中的 HttpConfig 类型
        var httpConfigTypes = CoreAssembly
            .GetTypes()
            .Where(t => t.Name == "HttpConfig")
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 HttpConfig 类型
        Assert.Empty(httpConfigTypes);
    }
}
