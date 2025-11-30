using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 命名空间与文件夹结构匹配测试
/// Tests to ensure namespaces match folder structure
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. 所有 C# 文件的命名空间必须与其所在的文件夹结构完全匹配
/// 2. 命名空间应基于项目根命名空间加上文件相对于项目根目录的路径
/// </remarks>
public class NamespaceLocationTests
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
    /// 验证所有文件的命名空间与文件夹结构匹配
    /// All file namespaces should match folder structure
    /// </summary>
    [Fact]
    public void AllFileNamespacesShouldMatchFolderStructure()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<NamespaceMismatch>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
            var expected = GetExpectedNamespace(relativePath);
            var actual = GetActualNamespace(file);

            if (expected != null && actual != null && expected != actual)
            {
                violations.Add(new NamespaceMismatch
                {
                    FilePath = relativePath,
                    ExpectedNamespace = expected,
                    ActualNamespace = actual
                });
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个命名空间与文件夹结构不匹配:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                report.AppendLine($"\n❌ {violation.FilePath}");
                report.AppendLine($"   期望命名空间: {violation.ExpectedNamespace}");
                report.AppendLine($"   实际命名空间: {violation.ActualNamespace}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 copilot-instructions.md 规范:");
            report.AppendLine("  所有 C# 文件的命名空间必须与其所在的文件夹结构完全匹配。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 修改文件中的命名空间声明，使其与文件夹结构匹配");
            report.AppendLine("  2. 更新所有引用该命名空间的 using 语句");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成命名空间与文件夹结构对照报告
    /// Generate namespace vs folder structure audit report
    /// </summary>
    [Fact]
    public void GenerateNamespaceFolderStructureAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 命名空间与文件夹结构对照报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var stats = new Dictionary<string, (int Total, int Matched, int Mismatched)>();
        var mismatches = new List<(string File, string Expected, string Actual)>();

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
            var projectName = GetProjectName(relativePath);
            var expected = GetExpectedNamespace(relativePath);
            var actual = GetActualNamespace(file);

            if (!stats.ContainsKey(projectName))
            {
                stats[projectName] = (0, 0, 0);
            }

            var current = stats[projectName];
            current.Total++;

            if (expected != null && actual != null)
            {
                if (expected == actual)
                {
                    current.Matched++;
                }
                else
                {
                    current.Mismatched++;
                    mismatches.Add((relativePath, expected, actual));
                }
            }
            else
            {
                current.Matched++; // 无法解析的视为匹配
            }

            stats[projectName] = current;
        }

        report.AppendLine("## 统计摘要\n");
        report.AppendLine("| 项目 | 总文件数 | 匹配 | 不匹配 |");
        report.AppendLine("|------|----------|------|--------|");

        foreach (var (project, (total, matched, mismatched)) in stats.OrderBy(kv => kv.Key))
        {
            var status = mismatched > 0 ? "❌" : "✅";
            report.AppendLine($"| {status} {project} | {total} | {matched} | {mismatched} |");
        }

        if (mismatches.Any())
        {
            report.AppendLine("\n## 不匹配详情\n");
            foreach (var (file, expected, actual) in mismatches.Take(20))
            {
                report.AppendLine($"### {file}");
                report.AppendLine($"- 期望: `{expected}`");
                report.AppendLine($"- 实际: `{actual}`");
                report.AppendLine();
            }

            if (mismatches.Count > 20)
            {
                report.AppendLine($"\n... 还有 {mismatches.Count - 20} 处不匹配");
            }
        }

        Console.WriteLine(report);
        Assert.True(true, "Report generated successfully");
    }

    #region Helper Methods

    /// <summary>
    /// 项目命名空间前缀
    /// </summary>
    private const string ProjectNamespacePrefix = "ZakYip.WheelDiverterSorter.";

    /// <summary>
    /// 特殊结构中，从项目根目录开始的子目录起始索引
    /// 例如: src/<ProjectFolder>/SubDir1/File.cs -> 子目录从索引 1 开始
    /// </summary>
    private const int SpecialStructureSubDirStartIndex = 1;

    /// <summary>
    /// 标准结构中，项目文件夹在路径中的索引
    /// 例如: src/<Category>/<ProjectFolder>/SubDir/File.cs -> 项目文件夹在索引 1
    /// </summary>
    private const int StandardStructureProjectFolderIndex = 1;

    /// <summary>
    /// 标准结构中，子目录的起始索引
    /// 例如: src/<Category>/<ProjectFolder>/SubDir/File.cs -> 子目录从索引 2 开始
    /// </summary>
    private const int StandardStructureSubDirStartIndex = 2;

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    /// <summary>
    /// 从文件路径获取期望的命名空间
    /// </summary>
    private static string? GetExpectedNamespace(string relativePath)
    {
        // 移除 src/ 前缀
        if (!relativePath.StartsWith("src/"))
        {
            return null;
        }
        
        var pathWithoutSrc = relativePath.Substring(4); // 移除 "src/"
        var parts = pathWithoutSrc.Split('/');
        
        if (parts.Length < 2)
        {
            return null;
        }

        // 处理两种结构：
        // 1. 标准结构: src/<Category>/<ProjectFolder>/[SubDirs/]File.cs
        //    例如: src/Execution/ZakYip.WheelDiverterSorter.Execution/Extensions/NodeHealthServiceExtensions.cs
        // 2. 特殊结构（Analyzers）: src/<ProjectFolder>/File.cs
        //    例如: src/ZakYip.WheelDiverterSorter.Analyzers/DateTimeNowUsageAnalyzer.cs

        string projectFolder;
        string[] subDirs;

        // 检查第一个部分是否是完整的项目命名空间（以 ZakYip 开头）
        if (parts[0].StartsWith(ProjectNamespacePrefix, StringComparison.Ordinal))
        {
            // 特殊结构：项目直接在 src 下
            projectFolder = parts[0];
            // 获取子目录（排除文件名）：Skip(1) 跳过项目文件夹，Take(length-2) 排除项目文件夹和文件名
            subDirs = parts.Skip(SpecialStructureSubDirStartIndex).Take(parts.Length - 2).ToArray();
        }
        else
        {
            // 标准结构: parts[0] = Category, parts[1] = ProjectFolder
            if (parts.Length < 2)
            {
                return null;
            }
            projectFolder = parts[StandardStructureProjectFolderIndex];
            // 获取子目录（排除文件名）：Skip(2) 跳过 Category 和项目文件夹，Take(length-3) 排除 Category、项目文件夹和文件名
            subDirs = parts.Skip(StandardStructureSubDirStartIndex).Take(parts.Length - 3).ToArray();
        }
        
        if (subDirs.Length > 0)
        {
            return $"{projectFolder}.{string.Join(".", subDirs)}";
        }
        else
        {
            return projectFolder;
        }
    }

    /// <summary>
    /// 从文件内容获取实际命名空间
    /// </summary>
    private static string? GetActualNamespace(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            
            // 匹配命名空间声明（支持文件范围和块范围）
            var match = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        catch
        {
            // 忽略读取错误
        }
        
        return null;
    }

    /// <summary>
    /// 从相对路径获取项目名称
    /// </summary>
    private static string GetProjectName(string relativePath)
    {
        var parts = relativePath.Replace("\\", "/").Split('/');
        if (parts.Length >= 3)
        {
            return parts[2]; // src/<Category>/<ProjectName>/...
        }
        return "Unknown";
    }

    #endregion

    private class NamespaceMismatch
    {
        public required string FilePath { get; init; }
        public required string ExpectedNamespace { get; init; }
        public required string ActualNamespace { get; init; }
    }
}
