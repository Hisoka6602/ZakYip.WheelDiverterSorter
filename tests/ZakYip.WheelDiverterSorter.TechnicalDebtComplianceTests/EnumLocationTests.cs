using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD10: 枚举位置合规性测试
/// Tests to ensure enums are not defined inside interfaces or DTOs
/// </summary>
/// <remarks>
/// 根据规范，禁止在以下位置定义枚举：
/// 1. interface 内部定义 enum
/// 2. 名字以 Dto 结尾的类型内部定义 enum
/// 
/// 所有枚举应该集中在 Core/Enums 目录下。
/// </remarks>
public class EnumLocationTests
{
    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// PR-SD10: 禁止在接口或DTO内定义枚举
    /// Should not define enums inside interfaces or DTOs
    /// </summary>
    /// <remarks>
    /// 扫描所有非测试项目，检测：
    /// 1. interface 内定义的 enum
    /// 2. 名字以 Dto 结尾的类型内部定义的 enum
    /// </remarks>
    [Fact]
    public void ShouldNotDefineEnumsInsideInterfacesOrDtos()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<EnumInlineViolation>();

        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var fileViolations = DetectInlineEnums(file, solutionRoot);
            violations.AddRange(fileViolations);
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD10 违规: 发现 {violations.Count} 个枚举定义在接口或DTO内部:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"\n❌ {violation.EnumName}:");
                report.AppendLine($"   位置: {relativePath}:{violation.LineNumber}");
                report.AppendLine($"   容器类型: {violation.ContainerType} ({violation.ContainerKind})");
                report.AppendLine($"   命名空间: {violation.Namespace}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD10 规范:");
            report.AppendLine("  禁止在接口或DTO内定义枚举。所有枚举应集中在 Core/Enums 目录下。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将枚举提取到 src/Core/ZakYip.WheelDiverterSorter.Core/Enums/[子目录]/");
            report.AppendLine("  2. 更新命名空间为 ZakYip.WheelDiverterSorter.Core.Enums.[子命名空间]");
            report.AppendLine("  3. 在原接口/DTO文件中添加 using 语句引用新的枚举");

            Assert.Fail(report.ToString());
        }
    }

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    /// <summary>
    /// 检测文件中是否存在内联枚举定义
    /// </summary>
    private static List<EnumInlineViolation> DetectInlineEnums(string filePath, string solutionRoot)
    {
        var violations = new List<EnumInlineViolation>();

        try
        {
            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');

            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 跟踪当前所在的类型上下文
            int braceDepth = 0;
            string? currentContainerType = null;
            string? currentContainerKind = null;
            int containerStartLine = 0;
            int containerBraceDepth = 0;

            // 正则表达式匹配
            var interfacePattern = new Regex(@"^\s*(?:public|internal|private|protected)\s+(?:partial\s+)?interface\s+(?<name>\w+)", RegexOptions.Compiled);
            var dtoClassPattern = new Regex(@"^\s*(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+)(?<name>\w+Dto)\b", RegexOptions.Compiled);
            var enumPattern = new Regex(@"^\s*(?:public|internal|private|protected)\s+enum\s+(?<name>\w+)", RegexOptions.Compiled);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // 检查是否进入接口定义
                var interfaceMatch = interfacePattern.Match(line);
                if (interfaceMatch.Success && currentContainerType == null)
                {
                    currentContainerType = interfaceMatch.Groups["name"].Value;
                    currentContainerKind = "interface";
                    containerStartLine = i + 1;
                    containerBraceDepth = braceDepth;
                }

                // 检查是否进入DTO类定义
                var dtoMatch = dtoClassPattern.Match(line);
                if (dtoMatch.Success && currentContainerType == null)
                {
                    currentContainerType = dtoMatch.Groups["name"].Value;
                    currentContainerKind = "Dto";
                    containerStartLine = i + 1;
                    containerBraceDepth = braceDepth;
                }

                // 计算大括号深度
                braceDepth += line.Count(c => c == '{');
                braceDepth -= line.Count(c => c == '}');

                // 检查是否在容器内定义了枚举
                if (currentContainerType != null && braceDepth > containerBraceDepth)
                {
                    var enumMatch = enumPattern.Match(line);
                    if (enumMatch.Success)
                    {
                        violations.Add(new EnumInlineViolation
                        {
                            EnumName = enumMatch.Groups["name"].Value,
                            ContainerType = currentContainerType,
                            ContainerKind = currentContainerKind ?? "unknown",
                            FilePath = filePath,
                            LineNumber = i + 1,
                            Namespace = ns
                        });
                    }
                }

                // 检查是否离开容器
                if (currentContainerType != null && braceDepth <= containerBraceDepth)
                {
                    currentContainerType = null;
                    currentContainerKind = null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting inline enums from {filePath}: {ex.Message}");
        }

        return violations;
    }

    #endregion
}

/// <summary>
/// 内联枚举违规信息
/// </summary>
public record EnumInlineViolation
{
    /// <summary>
    /// 枚举名称
    /// </summary>
    public required string EnumName { get; init; }

    /// <summary>
    /// 容器类型名称
    /// </summary>
    public required string ContainerType { get; init; }

    /// <summary>
    /// 容器类型种类（interface/Dto）
    /// </summary>
    public required string ContainerKind { get; init; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 行号
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// 命名空间
    /// </summary>
    public required string Namespace { get; init; }
}
