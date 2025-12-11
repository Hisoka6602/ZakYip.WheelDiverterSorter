using System.Reflection;
using System.Text;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 公共类型短名重复检测测试
/// Tests to detect duplicate public type short names across assemblies
/// </summary>
/// <remarks>
/// 通过反射扫描所有非测试程序集，检测短名重复的公共类型。
/// 
/// 目标：
/// 1. 收集所有 public 类型的 Name（不含命名空间）
/// 2. 按短名分组，组内类型数 &gt; 1 的视为"疑似影分身"
/// 3. 明确白名单（允许重复的类型，如 Program 等）
/// 4. 除白名单外，一律测试失败并输出详细信息
/// </remarks>
public class DuplicateTypeDetectionTests_PublicTypes
{
    /// <summary>
    /// 白名单：允许短名重复的公共类型
    /// 这些类型由于约定俗成的命名或特殊原因允许存在多个
    /// </summary>
    private static readonly HashSet<string> AllowedDuplicateTypeNames = new(StringComparer.Ordinal)
    {
        // 框架/入口类型
        "Program",
        
        // 常见内部枚举/常量类型名称（可能在多个上下文中独立使用）
        "Status",
        "State",
        "Mode",
        "Type",
        "Kind",
        "Direction",
        "Result",
        
        // 泛型类型参数标记
        "T",
        "TKey",
        "TValue",
        
        // 测试/模拟类型前缀
        // (实际检测时已排除测试程序集)
    };

    /// <summary>
    /// 需要扫描的程序集名称前缀
    /// Assemblies with these prefixes will be scanned
    /// </summary>
    private static readonly string[] AssemblyPrefixes = 
    {
        "ZakYip.WheelDiverterSorter.Core",
        "ZakYip.WheelDiverterSorter.Execution",
        "ZakYip.WheelDiverterSorter.Drivers",
        "ZakYip.WheelDiverterSorter.Ingress",
        "ZakYip.WheelDiverterSorter.Communication",
        "ZakYip.WheelDiverterSorter.Application",
        "ZakYip.WheelDiverterSorter.Observability",
        "ZakYip.WheelDiverterSorter.Simulation"
    };

    /// <summary>
    /// 排除的程序集名称模式（测试程序集等）
    /// Assemblies matching these patterns will be excluded
    /// </summary>
    private static readonly string[] ExcludedAssemblyPatterns = 
    {
        ".Tests",
        ".Benchmarks",
        ".Analyzers"
    };

    /// <summary>
    /// PR-SD8: 验证非测试项目中公共类型短名没有重复（除白名单外）
    /// Verify that public type short names are unique across non-test assemblies
    /// </summary>
    [Fact]
    public void ShouldNotHaveDuplicatePublicTypeNames()
    {
        // 加载所有非测试程序集
        var assemblies = LoadNonTestAssemblies();
        
        // 收集所有公共类型
        var typesByShortName = new Dictionary<string, List<PublicTypeInfo>>(StringComparer.Ordinal);
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var publicTypes = assembly.GetExportedTypes()
                    .Where(t => t.IsPublic && !t.IsNested)
                    .Where(t => !IsCompilerGenerated(t))
                    .ToList();

                foreach (var type in publicTypes)
                {
                    var shortName = type.Name;
                    
                    // 处理泛型类型名称（移除 `1 等后缀）
                    var backtickIndex = shortName.IndexOf('`');
                    if (backtickIndex > 0)
                    {
                        shortName = shortName.Substring(0, backtickIndex);
                    }
                    
                    if (!typesByShortName.ContainsKey(shortName))
                    {
                        typesByShortName[shortName] = new List<PublicTypeInfo>();
                    }
                    
                    typesByShortName[shortName].Add(new PublicTypeInfo
                    {
                        ShortName = shortName,
                        FullName = type.FullName ?? type.Name,
                        AssemblyName = assembly.GetName().Name ?? "Unknown",
                        Namespace = type.Namespace ?? "Global"
                    });
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 某些类型可能无法加载，记录但继续
                Console.WriteLine($"Warning: Could not load all types from {assembly.GetName().Name}: {ex.Message}");
            }
        }

        // 查找重复的短名（排除白名单）
        var duplicates = typesByShortName
            .Where(kvp => kvp.Value.Count > 1)
            .Where(kvp => !AllowedDuplicateTypeNames.Contains(kvp.Key))
            // 只有当在多个不同程序集中定义时才算重复
            .Where(kvp => kvp.Value.Select(t => t.AssemblyName).Distinct().Count() > 1)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {duplicates.Count} 个公共类型短名存在跨程序集重复:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (shortName, types) in duplicates)
            {
                report.AppendLine($"\n❌ {shortName}:");
                foreach (var typeInfo in types.OrderBy(t => t.AssemblyName))
                {
                    report.AppendLine($"   - 全名: {typeInfo.FullName}");
                    report.AppendLine($"     程序集: {typeInfo.AssemblyName}");
                    report.AppendLine($"     命名空间: {typeInfo.Namespace}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD8 规范:");
            report.AppendLine("  非测试项目中，公共类型短名应唯一（除白名单外）。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 合并语义相同的重复类型到一个位置");
            report.AppendLine("  2. 重命名类型以区分（如仿真类型使用 Simulated 前缀）");
            report.AppendLine("  3. 如果类型确实需要在多处定义，将其添加到白名单并说明原因");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成公共类型重复审计报告（信息性，不失败）
    /// Generate public type duplicate audit report (informational, does not fail)
    /// </summary>
    [Fact]
    public void GeneratePublicTypeDuplicateAuditReport()
    {
        var assemblies = LoadNonTestAssemblies();
        var typesByShortName = new Dictionary<string, List<PublicTypeInfo>>(StringComparer.Ordinal);
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var publicTypes = assembly.GetExportedTypes()
                    .Where(t => t.IsPublic && !t.IsNested)
                    .Where(t => !IsCompilerGenerated(t))
                    .ToList();

                foreach (var type in publicTypes)
                {
                    var shortName = type.Name;
                    var backtickIndex = shortName.IndexOf('`');
                    if (backtickIndex > 0)
                    {
                        shortName = shortName.Substring(0, backtickIndex);
                    }
                    
                    if (!typesByShortName.ContainsKey(shortName))
                    {
                        typesByShortName[shortName] = new List<PublicTypeInfo>();
                    }
                    
                    typesByShortName[shortName].Add(new PublicTypeInfo
                    {
                        ShortName = shortName,
                        FullName = type.FullName ?? type.Name,
                        AssemblyName = assembly.GetName().Name ?? "Unknown",
                        Namespace = type.Namespace ?? "Global"
                    });
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Ignore load errors for audit report
            }
        }

        var report = new StringBuilder();
        report.AppendLine("# 公共类型短名审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**扫描程序集数**: {assemblies.Count}\n");

        // 统计信息
        var totalTypes = typesByShortName.Values.Sum(v => v.Count);
        var duplicateGroups = typesByShortName.Where(kvp => kvp.Value.Count > 1).ToList();
        var crossAssemblyDuplicates = duplicateGroups
            .Where(kvp => kvp.Value.Select(t => t.AssemblyName).Distinct().Count() > 1)
            .ToList();

        report.AppendLine("## 统计摘要\n");
        report.AppendLine($"- 总公共类型数: {totalTypes}");
        report.AppendLine($"- 唯一短名数: {typesByShortName.Count}");
        report.AppendLine($"- 存在重复的短名数: {duplicateGroups.Count}");
        report.AppendLine($"- 跨程序集重复的短名数: {crossAssemblyDuplicates.Count}");
        report.AppendLine();

        // 跨程序集重复（需要关注）
        if (crossAssemblyDuplicates.Any())
        {
            report.AppendLine("## ⚠️ 跨程序集重复的类型（需要关注）\n");
            report.AppendLine("| 短名 | 程序集 | 全名 |");
            report.AppendLine("|------|--------|------|");
            
            foreach (var (shortName, types) in crossAssemblyDuplicates.OrderBy(kvp => kvp.Key))
            {
                var isWhitelisted = AllowedDuplicateTypeNames.Contains(shortName);
                var marker = isWhitelisted ? "✅" : "❌";
                foreach (var typeInfo in types.OrderBy(t => t.AssemblyName))
                {
                    report.AppendLine($"| {marker} {shortName} | {typeInfo.AssemblyName} | {typeInfo.FullName} |");
                }
            }
            report.AppendLine();
        }

        // 白名单类型
        report.AppendLine("## 白名单类型\n");
        report.AppendLine("以下短名允许在多个程序集中存在：\n");
        foreach (var name in AllowedDuplicateTypeNames.OrderBy(n => n))
        {
            report.AppendLine($"- `{name}`");
        }

        Console.WriteLine(report);
        
        Assert.True(true, "Audit report generated successfully");
    }

    #region Helper Methods

    private List<Assembly> LoadNonTestAssemblies()
    {
        var assemblies = new List<Assembly>();
        
        // 获取当前加载的程序集
        var loadedAssemblies = AppDomain.CurrentStateomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a.GetName().Name != null)
            .ToList();

        // 过滤出目标程序集
        foreach (var assembly in loadedAssemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null) continue;

            // 检查是否匹配目标前缀
            var matchesPrefix = AssemblyPrefixes.Any(prefix => 
                assemblyName.StartsWith(prefix, StringComparison.Ordinal));
            
            if (!matchesPrefix) continue;

            // 检查是否被排除
            var isExcluded = ExcludedAssemblyPatterns.Any(pattern => 
                assemblyName.Contains(pattern, StringComparison.Ordinal));
            
            if (isExcluded) continue;

            assemblies.Add(assembly);
        }

        // 尝试加载尚未加载的目标程序集
        var solutionRoot = GetSolutionRoot();
        if (solutionRoot != null)
        {
            var binPaths = new[]
            {
                Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "bin"),
                Path.Combine(solutionRoot, "src", "Execution", "ZakYip.WheelDiverterSorter.Execution", "bin"),
                Path.Combine(solutionRoot, "src", "Drivers", "ZakYip.WheelDiverterSorter.Drivers", "bin"),
                Path.Combine(solutionRoot, "src", "Ingress", "ZakYip.WheelDiverterSorter.Ingress", "bin"),
                Path.Combine(solutionRoot, "src", "Infrastructure", "ZakYip.WheelDiverterSorter.Communication", "bin"),
                Path.Combine(solutionRoot, "src", "Application", "ZakYip.WheelDiverterSorter.Application", "bin"),
                Path.Combine(solutionRoot, "src", "Observability", "ZakYip.WheelDiverterSorter.Observability", "bin"),
                Path.Combine(solutionRoot, "src", "Simulation", "ZakYip.WheelDiverterSorter.Simulation", "bin")
            };

            foreach (var binPath in binPaths.Where(Directory.Exists))
            {
                var dllFiles = Directory.GetFiles(binPath, "ZakYip.WheelDiverterSorter.*.dll", SearchOption.AllDirectories)
                    .Where(f => !ExcludedAssemblyPatterns.Any(p => f.Contains(p, StringComparison.Ordinal)));

                foreach (var dllFile in dllFiles)
                {
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(dllFile);
                        if (assemblies.All(a => a.GetName().FullName != assemblyName.FullName))
                        {
                            var assembly = Assembly.LoadFrom(dllFile);
                            assemblies.Add(assembly);
                        }
                    }
                    catch
                    {
                        // Ignore load errors
                    }
                }
            }
        }

        return assemblies.DistinctBy(a => a.GetName().FullName).ToList();
    }

    private static string? GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir;
    }

    private static bool IsCompilerGenerated(Type type)
    {
        // 排除编译器生成的类型（如匿名类型、迭代器状态机等）
        return type.Name.StartsWith("<") ||
               type.Name.Contains("__") ||
               type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Any();
    }

    #endregion
}

/// <summary>
/// 公共类型信息
/// Public type information
/// </summary>
public record PublicTypeInfo
{
    /// <summary>
    /// 类型短名（不含命名空间）
    /// </summary>
    public required string ShortName { get; init; }
    
    /// <summary>
    /// 类型全名（含命名空间）
    /// </summary>
    public required string FullName { get; init; }
    
    /// <summary>
    /// 所在程序集名称
    /// </summary>
    public required string AssemblyName { get; init; }
    
    /// <summary>
    /// 命名空间
    /// </summary>
    public required string Namespace { get; init; }
}
