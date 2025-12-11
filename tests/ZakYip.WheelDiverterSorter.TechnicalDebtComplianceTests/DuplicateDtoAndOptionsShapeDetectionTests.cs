using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Reflection;
using System.Text;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: DTO/Options/Config 结构重复检测测试
/// Tests to detect structurally duplicate DTO/Options/Config types
/// </summary>
/// <remarks>
/// 通过反射扫描所有非测试程序集，检测结构签名相同但名称不同的 DTO/Options/Config 类型。
/// 
/// 检测策略：
/// 1. 只扫描类型名以 Dto / Options / Config / Configuration 结尾的 public 类型
/// 2. 为每个类型构建"结构签名"：所有 public 可读属性的 (属性名, 属性类型) 集合
/// 3. 按"结构签名"分组，同一组里有多个不同类型名/不同命名空间的，视为"结构影分身"
/// 
/// 目标：确保不存在结构完全相同但命名不同/命名空间不同的"平行版本"。
/// </remarks>
public class DuplicateDtoAndOptionsShapeDetectionTests
{
    /// <summary>
    /// 目标类型后缀
    /// Type suffixes to scan
    /// </summary>
    private static readonly string[] TargetTypeSuffixes = 
    {
        "Dto",
        "Options",
        "Config",
        "Configuration",
        "Settings"
    };

    /// <summary>
    /// 需要扫描的程序集名称前缀
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
        "ZakYip.WheelDiverterSorter.Simulation",
        "ZakYip.WheelDiverterSorter.Host"
    };

    /// <summary>
    /// 排除的程序集名称模式
    /// </summary>
    private static readonly string[] ExcludedAssemblyPatterns = 
    {
        ".Tests",
        ".Benchmarks",
        ".Analyzers"
    };

    /// <summary>
    /// 白名单：允许结构相同的类型组
    /// 格式：以逗号分隔的类型短名列表（按字母排序）
    /// 
    /// 这些类型在 PR-SD8 之前已存在，需要后续 PR 逐步清理。
    /// 新增的结构重复不应加入此白名单。
    /// </summary>
    private static readonly HashSet<string> AllowedStructuralDuplicates = new(StringComparer.Ordinal)
    {
        // PR-CONFIG-HOTRELOAD02: 所有影分身技术债已解决，白名单已清空
    };

    /// <summary>
    /// PR-SD8: 验证不存在结构完全相同的 DTO/Options/Config 类型
    /// Verify that there are no structurally duplicate DTO/Options/Config types
    /// </summary>
    [Fact]
    public void ShouldNotHaveStructurallyDuplicatedDtosOrOptions()
    {
        // 加载所有非测试程序集
        var assemblies = LoadNonTestAssemblies();
        
        // 收集所有目标类型
        var targetTypes = new List<StructuralTypeInfo>();
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetExportedTypes()
                    .Where(t => t.IsPublic && !t.IsNested)
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => IsTargetType(t))
                    .ToList();

                foreach (var type in types)
                {
                    var signature = BuildStructuralSignature(type);
                    
                    targetTypes.Add(new StructuralTypeInfo
                    {
                        TypeName = type.Name,
                        FullName = type.FullName ?? type.Name,
                        AssemblyName = assembly.GetName().Name ?? "Unknown",
                        Namespace = type.Namespace ?? "Global",
                        StructuralSignature = signature,
                        PropertyCount = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead).Count()
                    });
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"Warning: Could not load all types from {assembly.GetName().Name}: {ex.Message}");
            }
        }

        // 按结构签名分组
        var typesBySignature = targetTypes
            .Where(t => !string.IsNullOrEmpty(t.StructuralSignature))
            .Where(t => t.PropertyCount > 0) // 排除空类型
            .GroupBy(t => t.StructuralSignature)
            .Where(g => g.Count() > 1) // 只关注有重复的
            .Where(g => g.Select(t => t.FullName).Distinct().Count() > 1) // 确保是不同类型
            .ToList();

        // 过滤出真正的违规（排除白名单）
        var violations = new List<(string Signature, List<StructuralTypeInfo> Types)>();
        
        foreach (var group in typesBySignature)
        {
            var typeNames = group.Select(t => t.TypeName).OrderBy(n => n).ToList();
            var groupKey = string.Join(",", typeNames);
            
            if (!AllowedStructuralDuplicates.Contains(groupKey))
            {
                violations.Add((group.Key, group.ToList()));
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 组结构相同的 DTO/Options/Config 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n以下类型具有完全相同的属性结构，可能是影分身：\n");

            var groupIndex = 1;
            foreach (var (signature, types) in violations)
            {
                report.AppendLine($"━━━ 组 {groupIndex} ━━━");
                report.AppendLine($"属性签名: {TruncateSignature(signature)}");
                report.AppendLine($"属性数量: {types.First().PropertyCount}");
                report.AppendLine();
                
                foreach (var typeInfo in types.OrderBy(t => t.AssemblyName).ThenBy(t => t.TypeName))
                {
                    report.AppendLine($"❌ {typeInfo.TypeName}");
                    report.AppendLine($"   全名: {typeInfo.FullName}");
                    report.AppendLine($"   程序集: {typeInfo.AssemblyName}");
                    report.AppendLine($"   命名空间: {typeInfo.Namespace}");
                    report.AppendLine();
                }
                
                groupIndex++;
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD8 规范:");
            report.AppendLine("  不应存在结构完全相同但命名不同/命名空间不同的'平行版本'。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 如果只是名字不同（如 XxxConfig vs XxxOptions）:");
            report.AppendLine("     - 选择一个规范名作为唯一版本");
            report.AppendLine("     - 删除另一份或改成 using 别名");
            report.AppendLine("  2. 如果应用层和通信层都有同结构 DTO:");
            report.AppendLine("     - 在 Core 或 Contracts 项目定义统一 DTO");
            report.AppendLine("     - 其他层只引用，不再各自拷贝");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成结构重复审计报告（信息性，不失败）
    /// Generate structural duplicate audit report (informational, does not fail)
    /// </summary>
    [Fact]
    public void GenerateStructuralDuplicateAuditReport()
    {
        var assemblies = LoadNonTestAssemblies();
        var targetTypes = new List<StructuralTypeInfo>();
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetExportedTypes()
                    .Where(t => t.IsPublic && !t.IsNested)
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => IsTargetType(t))
                    .ToList();

                foreach (var type in types)
                {
                    var signature = BuildStructuralSignature(type);
                    
                    targetTypes.Add(new StructuralTypeInfo
                    {
                        TypeName = type.Name,
                        FullName = type.FullName ?? type.Name,
                        AssemblyName = assembly.GetName().Name ?? "Unknown",
                        Namespace = type.Namespace ?? "Global",
                        StructuralSignature = signature,
                        PropertyCount = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead).Count()
                    });
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Ignore for audit
            }
        }

        var report = new StringBuilder();
        report.AppendLine("# DTO/Options/Config 结构审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**扫描程序集数**: {assemblies.Count}\n");

        // 统计
        var typesBySignature = targetTypes
            .Where(t => !string.IsNullOrEmpty(t.StructuralSignature))
            .Where(t => t.PropertyCount > 0)
            .GroupBy(t => t.StructuralSignature)
            .ToList();

        var uniqueStructures = typesBySignature.Count;
        var duplicateGroups = typesBySignature.Where(g => g.Count() > 1).ToList();

        report.AppendLine("## 统计摘要\n");
        report.AppendLine($"- 目标类型总数: {targetTypes.Count}");
        report.AppendLine($"- 唯一结构签名数: {uniqueStructures}");
        report.AppendLine($"- 存在结构重复的组数: {duplicateGroups.Count}");
        report.AppendLine();

        // 按后缀分类统计
        report.AppendLine("## 按类型后缀分类\n");
        report.AppendLine("| 后缀 | 数量 |");
        report.AppendLine("|------|------|");
        foreach (var suffix in TargetTypeSuffixes)
        {
            var count = targetTypes.Count(t => t.TypeName.EndsWith(suffix, StringComparison.Ordinal));
            if (count > 0)
            {
                report.AppendLine($"| *{suffix} | {count} |");
            }
        }
        report.AppendLine();

        // 详细的重复组
        if (duplicateGroups.Any())
        {
            report.AppendLine("## 结构重复的类型组\n");
            
            var groupIndex = 1;
            foreach (var group in duplicateGroups.OrderByDescending(g => g.Count()))
            {
                var types = group.ToList();
                report.AppendLine($"### 组 {groupIndex} (共 {types.Count} 个类型)\n");
                report.AppendLine($"**属性数**: {types.First().PropertyCount}\n");
                report.AppendLine("| 类型名 | 程序集 | 命名空间 |");
                report.AppendLine("|--------|--------|----------|");
                foreach (var typeInfo in types.OrderBy(t => t.AssemblyName))
                {
                    report.AppendLine($"| {typeInfo.TypeName} | {typeInfo.AssemblyName} | {typeInfo.Namespace} |");
                }
                report.AppendLine();
                groupIndex++;
            }
        }

        Console.WriteLine(report);
        
        Assert.True(true, "Audit report generated successfully");
    }

    #region Helper Methods

    private List<Assembly> LoadNonTestAssemblies()
    {
        var assemblies = new List<Assembly>();
        
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a.GetName().Name != null)
            .ToList();

        foreach (var assembly in loadedAssemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null) continue;

            var matchesPrefix = AssemblyPrefixes.Any(prefix => 
                assemblyName.StartsWith(prefix, StringComparison.Ordinal));
            
            if (!matchesPrefix) continue;

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
                Path.Combine(solutionRoot, "src", "Simulation", "ZakYip.WheelDiverterSorter.Simulation", "bin"),
                Path.Combine(solutionRoot, "src", "Host", "ZakYip.WheelDiverterSorter.Host", "bin")
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

    private static bool IsTargetType(Type type)
    {
        var typeName = type.Name;
        
        // 处理泛型类型名称
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex > 0)
        {
            typeName = typeName.Substring(0, backtickIndex);
        }
        
        return TargetTypeSuffixes.Any(suffix => 
            typeName.EndsWith(suffix, StringComparison.Ordinal));
    }

    /// <summary>
    /// 构建类型的结构签名
    /// 签名格式：PropertyName1:PropertyType1;PropertyName2:PropertyType2;...
    /// 属性按名称排序
    /// </summary>
    private static string BuildStructuralSignature(Type type)
    {
        try
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}:{GetSimpleTypeName(p.PropertyType)}")
                .ToList();

            return string.Join(";", properties);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取类型的简化名称（用于签名对比）
    /// </summary>
    private static string GetSimpleTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();
            var genericName = genericDef.Name;
            var backtickIndex = genericName.IndexOf('`');
            if (backtickIndex > 0)
            {
                genericName = genericName.Substring(0, backtickIndex);
            }
            var argNames = string.Join(",", genericArgs.Select(GetSimpleTypeName));
            return $"{genericName}<{argNames}>";
        }
        
        if (type.IsArray)
        {
            return $"{GetSimpleTypeName(type.GetElementType()!)}[]";
        }

        // 使用简化的类型名称
        return type.Name switch
        {
            "String" => "string",
            "Int32" => "int",
            "Int64" => "long",
            "Boolean" => "bool",
            "Double" => "double",
            "Single" => "float",
            "Decimal" => "decimal",
            "DateTime" => "DateTime",
            "TimeSpan" => "TimeSpan",
            "Guid" => "Guid",
            _ => type.Name
        };
    }

    /// <summary>
    /// 截断过长的签名用于显示
    /// </summary>
    private static string TruncateSignature(string signature)
    {
        const int maxLength = 100;
        if (signature.Length <= maxLength)
        {
            return signature;
        }
        return signature.Substring(0, maxLength) + "...";
    }

    #endregion
}

/// <summary>
/// 结构类型信息
/// Structural type information
/// </summary>
public record StructuralTypeInfo
{
    /// <summary>
    /// 类型短名
    /// </summary>
    public required string TypeName { get; init; }
    
    /// <summary>
    /// 类型全名
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
    
    /// <summary>
    /// 结构签名（属性名:属性类型 的排序列表）
    /// </summary>
    public required string StructuralSignature { get; init; }
    
    /// <summary>
    /// 属性数量
    /// </summary>
    public required int PropertyCount { get; init; }
}
