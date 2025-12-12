using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Reflection;
using Xunit;
using FluentAssertions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 影分身枚举检测测试
/// </summary>
/// <remarks>
/// <para><b>核心目标</b>：防止系统中出现语义重复或重叠的枚举定义（影分身枚举）。</para>
/// 
/// <para><b>什么是影分身枚举？</b></para>
/// <list type="bullet">
///   <item><description>两个枚举表达相同或高度相似的业务概念（如 SystemState vs SystemState）</description></item>
///   <item><description>两个枚举的值集合有显著重叠（如都有 Running/Paused/Faulted 等值）</description></item>
///   <item><description>名称相似度高，容易混淆（如 State vs OperatingState, Status vs StatusType）</description></item>
/// </list>
/// 
/// <para><b>为什么需要这个测试？</b></para>
/// <para>
/// SystemState vs SystemState 影分身问题说明了之前的防线测试不够全面：
/// - 只检查了 DTO、Options、Utilities 等类型的影分身
/// - **没有检查枚举影分身**
/// - 导致两个状态枚举长期并存，状态不同步，引发包裹创建失败
/// </para>
/// 
/// <para><b>检测策略</b>：</para>
/// <list type="number">
///   <item><description><b>名称相似度检测</b>：识别名称高度相似的枚举对</description></item>
///   <item><description><b>值集合重叠检测</b>：识别值名称重叠超过阈值的枚举对</description></item>
///   <item><description><b>语义关键词检测</b>：识别使用相同业务关键词的枚举</description></item>
///   <item><description><b>白名单机制</b>：允许合理的枚举共存（如厂商协议枚举 vs 通用枚举）</description></item>
/// </list>
/// 
/// <para><b>PR-FIX-SHADOW-ENUM</b>：本测试是影分身问题的最终防线</para>
/// </remarks>
public class ShadowEnumDetectionTests
{
    private readonly Assembly _coreAssembly;
    private readonly Assembly _executionAssembly;
    private readonly Assembly _driversAssembly;
    private readonly Assembly _ingressAssembly;
    private readonly Assembly _hostAssembly;
    private readonly Assembly _applicationAssembly;

    public ShadowEnumDetectionTests()
    {
        _coreAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Core");
        _executionAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Execution");
        _driversAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Drivers");
        _ingressAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Ingress");
        _hostAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Host");
        _applicationAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Application");
    }

    [Fact(DisplayName = "应该不存在名称相似的影分身枚举")]
    public void ShouldNotHaveSimilarNamedEnums()
    {
        // Arrange
        var assemblies = new[] { _coreAssembly, _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly };
        var allEnums = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsEnum && t.IsPublic)
            .ToList();

        var similarPairs = new List<(Type Enum1, Type Enum2, double Similarity)>();

        // Act: 检测名称相似度
        for (int i = 0; i < allEnums.Count; i++)
        {
            for (int j = i + 1; j < allEnums.Count; j++)
            {
                var enum1 = allEnums[i];
                var enum2 = allEnums[j];
                
                var similarity = CalculateNameSimilarity(enum1.Name, enum2.Name);
                
                // 相似度超过 60% 视为可疑
                if (similarity > 0.6)
                {
                    // 检查是否在白名单中
                    if (!IsInWhitelist(enum1, enum2))
                    {
                        similarPairs.Add((enum1, enum2, similarity));
                    }
                }
            }
        }

        // Assert
        similarPairs.Should().BeEmpty(
            because: "不应存在名称高度相似的枚举，这可能表示影分身问题。" +
                     "例如：SystemState vs SystemState（相似度82%）。" +
                     Environment.NewLine +
                     "发现的相似枚举对：" + Environment.NewLine +
                     string.Join(Environment.NewLine, similarPairs.Select(p =>
                         $"  - {p.Enum1.FullName} vs {p.Enum2.FullName} (相似度: {p.Similarity:P0})")));
    }

    [Fact(DisplayName = "应该不存在值集合高度重叠的影分身枚举")]
    public void ShouldNotHaveEnumsWithOverlappingValues()
    {
        // Arrange
        var assemblies = new[] { _coreAssembly, _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly };
        var allEnums = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsEnum && t.IsPublic)
            .ToList();

        var overlappingPairs = new List<(Type Enum1, Type Enum2, double OverlapRatio, string[] CommonValues)>();

        // Act: 检测值集合重叠
        for (int i = 0; i < allEnums.Count; i++)
        {
            for (int j = i + 1; j < allEnums.Count; j++)
            {
                var enum1 = allEnums[i];
                var enum2 = allEnums[j];
                
                var values1 = Enum.GetNames(enum1).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var values2 = Enum.GetNames(enum2).ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                var commonValues = values1.Intersect(values2, StringComparer.OrdinalIgnoreCase).ToArray();
                var totalValues = values1.Count + values2.Count - commonValues.Length;
                var overlapRatio = totalValues > 0 ? (double)commonValues.Length / Math.Min(values1.Count, values2.Count) : 0;
                
                // 重叠度超过 50% 视为可疑
                if (overlapRatio > 0.5 && commonValues.Length >= 3)
                {
                    if (!IsInWhitelist(enum1, enum2))
                    {
                        overlappingPairs.Add((enum1, enum2, overlapRatio, commonValues));
                    }
                }
            }
        }

        // Assert
        overlappingPairs.Should().BeEmpty(
            because: "不应存在值集合高度重叠的枚举，这可能表示影分身问题。" +
                     "例如：SystemState vs SystemState 都有 Running/Paused/Faulted 等值。" +
                     Environment.NewLine +
                     "发现的重叠枚举对：" + Environment.NewLine +
                     string.Join(Environment.NewLine, overlappingPairs.Select(p =>
                         $"  - {p.Enum1.FullName} vs {p.Enum2.FullName}" + Environment.NewLine +
                         $"    重叠度: {p.OverlapRatio:P0}, 共同值: {string.Join(", ", p.CommonValues)}")));
    }

    [Fact(DisplayName = "已知的影分身枚举必须已被删除")]
    public void KnownShadowEnumsMustBeDeleted()
    {
        // Arrange: 已知的影分身枚举列表
        var knownShadowEnums = new[]
        {
            "ZakYip.WheelDiverterSorter.Core.Enums.System.SystemState", // 已删除
        };

        var assemblies = new[] { _coreAssembly, _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly };
        var allTypes = assemblies.SelectMany(a => a.GetTypes()).ToList();

        var foundShadowEnums = new List<string>();

        // Act: 检查这些影分身是否还存在
        foreach (var shadowEnumFullName in knownShadowEnums)
        {
            var type = allTypes.FirstOrDefault(t => t.FullName == shadowEnumFullName);
            if (type != null)
            {
                foundShadowEnums.Add(shadowEnumFullName);
            }
        }

        // Assert
        foundShadowEnums.Should().BeEmpty(
            because: "已知的影分身枚举必须已被删除。" +
                     Environment.NewLine +
                     "以下影分身仍然存在：" + Environment.NewLine +
                     string.Join(Environment.NewLine, foundShadowEnums.Select(e => $"  - {e}")));
    }

    [Fact(DisplayName = "应该不存在以相同关键词结尾的状态类枚举")]
    public void ShouldNotHaveMultipleStateEnumsWithSameKeyword()
    {
        // Arrange: 关键状态类枚举后缀
        var stateKeywords = new[] { "State", "Status", "Mode", "Phase", "Stage" };
        
        var assemblies = new[] { _coreAssembly, _executionAssembly, _driversAssembly, _ingressAssembly, _hostAssembly, _applicationAssembly };
        var allEnums = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsEnum && t.IsPublic)
            .ToList();

        var suspiciousGroups = new List<(string Keyword, List<Type> Enums)>();

        // Act: 按关键词分组
        foreach (var keyword in stateKeywords)
        {
            var matchingEnums = allEnums
                .Where(e => e.Name.EndsWith(keyword, StringComparison.OrdinalIgnoreCase))
                .Where(e => !e.Name.StartsWith("Shu") && !e.Name.StartsWith("Modi") && !e.Name.StartsWith("S7")) // 排除厂商枚举
                .ToList();
            
            // 如果同一关键词有多个枚举，且它们的值有重叠，则可疑
            if (matchingEnums.Count > 1)
            {
                for (int i = 0; i < matchingEnums.Count; i++)
                {
                    for (int j = i + 1; j < matchingEnums.Count; j++)
                    {
                        var values1 = Enum.GetNames(matchingEnums[i]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var values2 = Enum.GetNames(matchingEnums[j]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var commonValues = values1.Intersect(values2, StringComparer.OrdinalIgnoreCase).Count();
                        
                        if (commonValues >= 2)
                        {
                            if (!suspiciousGroups.Any(g => g.Keyword == keyword))
                            {
                                suspiciousGroups.Add((keyword, new List<Type> { matchingEnums[i], matchingEnums[j] }));
                            }
                        }
                    }
                }
            }
        }

        // Assert
        suspiciousGroups.Should().BeEmpty(
            because: "相同关键词结尾的枚举如果值重叠，可能是影分身。" +
                     "例如：SystemState vs SystemState（都以State结尾，且值重叠）。" +
                     Environment.NewLine +
                     "发现的可疑枚举组：" + Environment.NewLine +
                     string.Join(Environment.NewLine, suspiciousGroups.Select(g =>
                         $"  - 关键词'{g.Keyword}': {string.Join(", ", g.Enums.Select(e => e.Name))}")));
    }

    /// <summary>
    /// 计算两个字符串的相似度（基于 Levenshtein 距离）
    /// </summary>
    private double CalculateNameSimilarity(string name1, string name2)
    {
        if (string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2))
            return 0;

        var distance = LevenshteinDistance(name1.ToLower(), name2.ToLower());
        var maxLength = Math.Max(name1.Length, name2.Length);
        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// 计算 Levenshtein 距离
    /// </summary>
    private int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    /// <summary>
    /// 检查枚举对是否在白名单中（允许合理共存）
    /// </summary>
    private bool IsInWhitelist(Type enum1, Type enum2)
    {
        
        // 白名单：这些枚举对虽然相似，但有合理的业务理由共存
        var whitelist = new[]
        {
            // 例子：WheelDiverterState（位置状态） vs WheelDeviceState（运行状态）- 语义不同
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterState",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDeviceState"),
            
            // 厂商协议枚举 vs 通用枚举 - 允许共存
            // ShuDiNiaoDeviceState vs WheelDeviceState - 厂商特定协议枚举
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDeviceState",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors.ShuDiNiaoDeviceState"),
            
            // 包裹状态相关 - 不同阶段的状态
            ("ZakYip.WheelDiverterSorter.Core.Enums.Parcel.ParcelFinalStatus",
             "ZakYip.WheelDiverterSorter.Core.Enums.Parcel.ParcelSimulationStatus"),
            
            // 摆轮方向 vs 侧面 - 语义不同但值重叠
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.DiverterDirection",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.DiverterSide"),
            
            // 厂商类型枚举 - 不同设备类型的厂商
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.DriverVendorType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorVendorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.DriverVendorType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.VendorId"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorVendorType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.VendorId"),
            
            // 通讯协议相关
            ("ZakYip.WheelDiverterSorter.Core.Enums.Communication.CommunicationMode",
             "ZakYip.WheelDiverterSorter.Core.Enums.Communication.UpstreamProtocolType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Communication.CommunicationMode",
             "ZakYip.WheelDiverterSorter.Core.Enums.Communication.ConnectionMode"),
            
            // 硬件相关 - 不同类型但名称相似
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.ActuatorBindingType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorBindingType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.DriverVendorType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterVendorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorBindingType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorIoType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorBindingType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorVendorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorFaultType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorIoType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorFaultType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorFaultType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorVendorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorIoType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorIoType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorVendorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.SensorVendorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterCommand",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterState"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterCommand",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterVendorType"),
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterState",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDiverterVendorType"),
            
            // 厂商协议消息类型
            ("ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors.ShuDiNiaoMessageType",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.Vendors.ShuDiNiaoResponseCode"),
            
            // 系统状态相关
            ("ZakYip.WheelDiverterSorter.Core.Enums.System.SystemState",
             "ZakYip.WheelDiverterSorter.Core.Enums.Hardware.WheelDeviceState"),
            
            // 步骤状态 vs 包裹状态 - 不同领域
            ("ZakYip.WheelDiverterSorter.Core.Enums.Execution.StepStatus",
             "ZakYip.WheelDiverterSorter.Core.Enums.Parcel.ParcelFinalStatus"),
            
            // 运行模式相关
            ("ZakYip.WheelDiverterSorter.Core.Enums.System.EnvironmentMode",
             "ZakYip.WheelDiverterSorter.Core.Enums.System.RuntimeMode"),
        };

        return whitelist.Any(w =>
            (w.Item1 == enum1.FullName && w.Item2 == enum2.FullName) ||
            (w.Item1 == enum2.FullName && w.Item2 == enum1.FullName));
    }
}
