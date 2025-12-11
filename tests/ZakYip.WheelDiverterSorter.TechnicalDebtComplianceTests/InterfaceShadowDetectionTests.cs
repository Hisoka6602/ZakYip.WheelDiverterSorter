using System.Reflection;
using Xunit;
using FluentAssertions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 接口影分身检测测试
/// </summary>
/// <remarks>
/// <para><b>核心目标</b>：防止系统中出现功能重复或高度相似的接口定义（接口影分身）。</para>
/// 
/// <para><b>什么是接口影分身？</b></para>
/// <list type="bullet">
///   <item><description>两个接口定义了相同或高度相似的方法签名</description></item>
///   <item><description>两个接口表达相同的业务职责但命名不同</description></item>
///   <item><description>多个接口都继承或实现相同的基础行为（如 StartAsync/StopAsync）</description></item>
/// </list>
/// 
/// <para><b>已发现的接口影分身案例</b>：</para>
/// <list type="number">
///   <item><description>
///     <b>ISensorEventProvider vs IParcelDetectionService</b>：
///     - 都定义了 ParcelDetected 事件
///     - 都定义了 DuplicateTriggerDetected 事件  
///     - 都有 StartAsync/StopAsync 方法
///     - 相似度 100%
///   </description></item>
///   <item><description>
///     <b>ISensorEventProvider vs ISensor</b>：
///     - 都有 StartAsync/StopAsync 方法
///     - 名称高度相似（都包含 Sensor）
///   </description></item>
/// </list>
/// 
/// <para><b>为什么会产生接口影分身？</b></para>
/// <list type="bullet">
///   <item><description>在不同层级重复定义抽象（Core/Abstractions vs Ingress）</description></item>
///   <item><description>为了"解耦"而过度抽象，创建多个功能相同的接口</description></item>
///   <item><description>缺乏统一的接口设计规范和审查机制</description></item>
/// </list>
/// 
/// <para><b>检测策略</b>：</para>
/// <list type="number">
///   <item><description><b>方法签名重叠检测</b>：检查接口间方法签名的重叠度</description></item>
///   <item><description><b>事件定义重叠检测</b>：检查接口间事件定义的重叠度</description></item>
///   <item><description><b>名称相似度检测</b>：检查接口名称的相似程度</description></item>
///   <item><description><b>跨层级抽象检测</b>：检查 Core/Abstractions 与其他层的接口重复</description></item>
/// </list>
/// 
/// <para><b>PR-FIX-INTERFACE-SHADOW</b>：接口影分身的最终防线</para>
/// </remarks>
public class InterfaceShadowDetectionTests
{
    private readonly Assembly _coreAssembly;
    private readonly Assembly _executionAssembly;
    private readonly Assembly _driversAssembly;
    private readonly Assembly _ingressAssembly;
    private readonly Assembly _hostAssembly;
    private readonly Assembly _applicationAssembly;

    public InterfaceShadowDetectionTests()
    {
        _coreAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Core");
        _executionAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Execution");
        _driversAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Drivers");
        _ingressAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Ingress");
        _hostAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Host");
        _applicationAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Application");
    }

    [Fact(DisplayName = "应该不存在方法签名高度重叠的接口影分身")]
    public void ShouldNotHaveInterfacesWithOverlappingMethods()
    {
        // Arrange
        var assemblies = new[] { _coreAssembly, _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly };
        var allInterfaces = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsInterface && t.IsPublic)
            .ToList();

        var shadowPairs = new List<(Type Interface1, Type Interface2, double Similarity, string[] CommonMethods)>();

        // Act: 检测方法签名重叠
        for (int i = 0; i < allInterfaces.Count; i++)
        {
            for (int j = i + 1; j < allInterfaces.Count; j++)
            {
                var interface1 = allInterfaces[i];
                var interface2 = allInterfaces[j];

                var methods1 = GetMethodSignatures(interface1);
                var methods2 = GetMethodSignatures(interface2);

                if (methods1.Count == 0 || methods2.Count == 0)
                    continue;

                var commonMethods = methods1.Intersect(methods2, StringComparer.OrdinalIgnoreCase).ToArray();
                var totalMethods = methods1.Count + methods2.Count - commonMethods.Length;
                var similarity = totalMethods > 0 ? (double)commonMethods.Length / Math.Min(methods1.Count, methods2.Count) : 0;

                // 重叠度超过 60% 视为可疑
                if (similarity > 0.6 && commonMethods.Length >= 2)
                {
                    if (!IsInMethodWhitelist(interface1, interface2))
                    {
                        shadowPairs.Add((interface1, interface2, similarity, commonMethods));
                    }
                }
            }
        }

        // Assert
        shadowPairs.Should().BeEmpty(
            because: "不应存在方法签名高度重叠的接口，这表示接口影分身问题。" +
                     Environment.NewLine +
                     "已知案例：ISensorEventProvider vs IParcelDetectionService（相似度100%，都有StartAsync/StopAsync和相同事件）" +
                     Environment.NewLine +
                     "发现的影分身接口对：" + Environment.NewLine +
                     string.Join(Environment.NewLine, shadowPairs.Select(p =>
                         $"  - {p.Interface1.FullName}" + Environment.NewLine +
                         $"    vs {p.Interface2.FullName}" + Environment.NewLine +
                         $"    相似度: {p.Similarity:P0}, 共同方法: {string.Join(", ", p.CommonMethods.Take(5))}")));
    }

    [Fact(DisplayName = "Core/Abstractions 的接口不应在其他层重复定义")]
    public void AbstractionInterfacesShouldNotBeDuplicatedInOtherLayers()
    {
        // Arrange
        var abstractionInterfaces = _coreAssembly.GetTypes()
            .Where(t => t.IsInterface && t.IsPublic && t.Namespace != null && t.Namespace.Contains("Abstractions"))
            .ToList();

        var otherLayerInterfaces = new[] { _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsInterface && t.IsPublic)
            .ToList();

        var duplicates = new List<(Type AbstractionInterface, Type OtherInterface, double Similarity)>();

        // Act: 检查 Abstractions 接口是否在其他层重复定义
        foreach (var abstractionInterface in abstractionInterfaces)
        {
            var abstractionMethods = GetMethodSignatures(abstractionInterface);
            var abstractionEvents = GetEventSignatures(abstractionInterface);

            foreach (var otherInterface in otherLayerInterfaces)
            {
                var otherMethods = GetMethodSignatures(otherInterface);
                var otherEvents = GetEventSignatures(otherInterface);

                var commonMethods = abstractionMethods.Intersect(otherMethods, StringComparer.OrdinalIgnoreCase).Count();
                var commonEvents = abstractionEvents.Intersect(otherEvents, StringComparer.OrdinalIgnoreCase).Count();

                var totalCommon = commonMethods + commonEvents;
                var totalAbstraction = abstractionMethods.Count + abstractionEvents.Count;

                if (totalAbstraction > 0 && totalCommon >= 2)
                {
                    var similarity = (double)totalCommon / totalAbstraction;
                    
                    if (similarity > 0.5 && !IsInAbstractionWhitelist(abstractionInterface, otherInterface))
                    {
                        duplicates.Add((abstractionInterface, otherInterface, similarity));
                    }
                }
            }
        }

        // Assert
        duplicates.Should().BeEmpty(
            because: "Core/Abstractions 中的接口不应在其他层重复定义，这违反了抽象层的唯一性原则。" +
                     Environment.NewLine +
                     "正确做法：其他层的服务应实现 Abstractions 中的接口，而不是重新定义相似接口。" +
                     Environment.NewLine +
                     "发现的重复定义：" + Environment.NewLine +
                     string.Join(Environment.NewLine, duplicates.Select(d =>
                         $"  - Abstraction: {d.AbstractionInterface.FullName}" + Environment.NewLine +
                         $"    Duplicate in: {d.OtherInterface.FullName}" + Environment.NewLine +
                         $"    相似度: {d.Similarity:P0}")));
    }

    [Fact(DisplayName = "应该不存在只包含StartAsync/StopAsync的多余接口")]
    public void ShouldNotHaveRedundantStartStopInterfaces()
    {
        // Arrange
        var assemblies = new[] { _coreAssembly, _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly };
        var allInterfaces = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsInterface && t.IsPublic)
            .ToList();

        var startStopOnlyInterfaces = new List<Type>();

        // Act: 查找只有 StartAsync/StopAsync 的接口
        foreach (var interfaceType in allInterfaces)
        {
            var methods = GetMethodSignatures(interfaceType);
            
            // 接口只有 StartAsync 和 StopAsync，没有其他方法或事件
            if (methods.Count == 2 &&
                methods.Any(m => m.Contains("StartAsync", StringComparison.OrdinalIgnoreCase)) &&
                methods.Any(m => m.Contains("StopAsync", StringComparison.OrdinalIgnoreCase)) &&
                GetEventSignatures(interfaceType).Count == 0)
            {
                startStopOnlyInterfaces.Add(interfaceType);
            }
        }

        // Assert
        startStopOnlyInterfaces.Should().BeEmpty(
            because: "只包含 StartAsync/StopAsync 的接口通常是过度抽象的信号。" +
                     Environment.NewLine +
                     "建议：使用 IHostedService 或继承通用的生命周期接口。" +
                     Environment.NewLine +
                     "发现的冗余接口：" + Environment.NewLine +
                     string.Join(Environment.NewLine, startStopOnlyInterfaces.Select(i => $"  - {i.FullName}")));
    }

    [Fact(DisplayName = "已知的接口影分身必须已被合并或删除")]
    public void KnownInterfaceShadowsMustBeResolved()
    {
        // Arrange: 已知的接口影分身列表
        var knownShadows = new[]
        {
            // TD-066: ISensorEventProvider vs IParcelDetectionService
            // 期望：保留 ISensorEventProvider，IParcelDetectionService 继承它或被删除
            ("ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress.ISensorEventProvider",
             "ZakYip.WheelDiverterSorter.Ingress.IParcelDetectionService"),
        };

        var assemblies = new[] { _coreAssembly, _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly };
        var allTypes = assemblies.SelectMany(a => a.GetTypes()).ToList();

        var unresolvedShadows = new List<string>();

        // Act: 检查已知影分身是否已解决
        foreach (var (interface1Name, interface2Name) in knownShadows)
        {
            var interface1 = allTypes.FirstOrDefault(t => t.FullName == interface1Name);
            var interface2 = allTypes.FirstOrDefault(t => t.FullName == interface2Name);

            if (interface1 != null && interface2 != null)
            {
                // 两个接口都还存在，检查是否有继承关系
                if (!interface2.GetInterfaces().Contains(interface1))
                {
                    unresolvedShadows.Add($"{interface1Name} vs {interface2Name} - 仍然是独立的接口");
                }
            }
        }

        // Assert
        unresolvedShadows.Should().BeEmpty(
            because: "已知的接口影分身必须已被解决（合并、继承或删除）。" +
                     Environment.NewLine +
                     "未解决的影分身：" + Environment.NewLine +
                     string.Join(Environment.NewLine, unresolvedShadows.Select(s => $"  - {s}")));
    }

    /// <summary>
    /// 获取接口的方法签名列表
    /// </summary>
    private HashSet<string> GetMethodSignatures(Type interfaceType)
    {
        var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        return methods
            .Where(m => !m.IsSpecialName) // 排除事件的 add/remove 方法
            .Select(m => $"{m.ReturnType.Name}_{m.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取接口的事件签名列表
    /// </summary>
    private HashSet<string> GetEventSignatures(Type interfaceType)
    {
        var events = interfaceType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        return events
            .Select(e => $"Event_{e.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 方法重叠白名单（允许合理共存）
    /// </summary>
    private bool IsInMethodWhitelist(Type interface1, Type interface2)
    {
        // 白名单：这些接口对虽然方法相似，但有合理的业务理由共存
        var whitelist = new[]
        {
            // 例子：Repository vs Service 接口 - 虽然方法相似但职责不同
            // 暂无白名单，发现即为影分身
        };

        return whitelist.Any(w =>
            (interface1.FullName?.Contains(w.Item1) == true && interface2.FullName?.Contains(w.Item2) == true) ||
            (interface1.FullName?.Contains(w.Item2) == true && interface2.FullName?.Contains(w.Item1) == true));
    }

    /// <summary>
    /// Abstractions 白名单（允许抽象与实现层接口并存）
    /// </summary>
    private bool IsInAbstractionWhitelist(Type abstractionInterface, Type otherInterface)
    {
        // 如果实现层接口继承了抽象层接口，这是正确的，不算影分身
        if (otherInterface.GetInterfaces().Contains(abstractionInterface))
        {
            return true;
        }

        // 其他白名单情况暂无
        return false;
    }
}
