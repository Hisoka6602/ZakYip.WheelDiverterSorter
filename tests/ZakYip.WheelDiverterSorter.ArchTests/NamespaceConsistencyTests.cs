using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// PR-RS12: 命名空间与物理路径一致性架构测试
/// Architecture tests for namespace and physical path consistency
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范（第 8 条）：
/// 1. 所有 C# 文件的命名空间必须与其所在的文件夹结构完全匹配
/// 2. 命名空间应基于项目根命名空间加上文件相对于项目根目录的路径
/// 
/// 这些测试作为架构防线，确保命名空间与物理路径保持一致，防止回归。
/// 与 TechnicalDebtComplianceTests.NamespaceLocationTests 配合使用，
/// 本测试类专注于架构约束验证。
/// </remarks>
public class NamespaceConsistencyTests
{
    private static readonly string SolutionRoot = GetSolutionRoot();

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
    /// 项目命名空间前缀
    /// </summary>
    private const string ProjectNamespacePrefix = "ZakYip.WheelDiverterSorter.";

    /// <summary>
    /// 验证所有项目的命名空间与物理路径一致
    /// All project namespaces should match their physical paths
    /// </summary>
    /// <remarks>
    /// 这是 TD-016 的架构防线测试，确保命名空间与物理路径完全对齐。
    /// 与 TechnicalDebtComplianceTests.NamespaceLocationTests.AllFileNamespacesShouldMatchFolderStructure 配合使用。
    /// </remarks>
    [Fact]
    public void AllSourceFiles_ShouldHaveNamespaceMatchingPhysicalPath()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        var violations = new List<NamespaceMismatch>();

        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(SolutionRoot, file).Replace("\\", "/");
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
            report.AppendLine($"\n❌ PR-RS12/TD-016 违规: 发现 {violations.Count} 个命名空间与物理路径不匹配:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations.Take(10).OrderBy(v => v.FilePath))
            {
                report.AppendLine($"\n❌ {violation.FilePath}");
                report.AppendLine($"   期望命名空间: {violation.ExpectedNamespace}");
                report.AppendLine($"   实际命名空间: {violation.ActualNamespace}");
            }

            if (violations.Count > 10)
            {
                report.AppendLine($"\n... 还有 {violations.Count - 10} 处不匹配");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 copilot-instructions.md 第 8 条规范:");
            report.AppendLine("  所有 C# 文件的命名空间必须与其所在的文件夹结构完全匹配。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 修改文件中的命名空间声明，使其与文件夹结构匹配");
            report.AppendLine("  2. 更新所有引用该命名空间的 using 语句");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证所有项目根命名空间以 ZakYip.WheelDiverterSorter 开头
    /// All project root namespaces should start with ZakYip.WheelDiverterSorter
    /// </summary>
    [Fact]
    public void AllSourceFiles_ShouldHaveCorrectRootNamespace()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        var violations = new List<string>();

        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var actual = GetActualNamespace(file);

            if (actual != null && !actual.StartsWith(ProjectNamespacePrefix, StringComparison.Ordinal))
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, file).Replace("\\", "/");
                violations.Add($"{relativePath} → namespace: {actual}");
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个文件的命名空间未以 {ProjectNamespacePrefix} 开头:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations.Take(10))
            {
                report.AppendLine($"  ❌ {violation}");
            }

            if (violations.Count > 10)
            {
                report.AppendLine($"\n... 还有 {violations.Count - 10} 处违规");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine($"\n💡 所有业务代码命名空间必须以 {ProjectNamespacePrefix} 开头。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证命名空间不跳级（不能跨层）
    /// Namespaces should not skip levels (no cross-layer jumps)
    /// </summary>
    /// <remarks>
    /// 例如：src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/Foo.cs
    /// 命名空间应该是 ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models
    /// 而不是 ZakYip.WheelDiverterSorter.Core.Configuration.Models（跳过 LineModel）
    /// </remarks>
    [Fact]
    public void Namespaces_ShouldNotSkipDirectoryLevels()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        var violations = new List<(string FilePath, string Expected, string Actual)>();

        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(SolutionRoot, file).Replace("\\", "/");
            var expected = GetExpectedNamespace(relativePath);
            var actual = GetActualNamespace(file);

            if (expected != null && actual != null && expected != actual)
            {
                // 检查是否是跳级情况（命名空间部分匹配但缺少中间层级）
                var expectedParts = expected.Split('.');
                var actualParts = actual.Split('.');

                // 如果实际命名空间比期望少，并且不是完全不同的命名空间
                if (actualParts.Length < expectedParts.Length &&
                    actual.StartsWith(ProjectNamespacePrefix) &&
                    expected.StartsWith(actual))
                {
                    violations.Add((relativePath, expected, actual));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个命名空间跳级（缺少中间目录层级）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (filePath, expected, actual) in violations.Take(10))
            {
                report.AppendLine($"\n❌ {filePath}");
                report.AppendLine($"   期望: {expected}");
                report.AppendLine($"   实际: {actual}（缺少中间层级）");
            }

            if (violations.Count > 10)
            {
                report.AppendLine($"\n... 还有 {violations.Count - 10} 处跳级");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 命名空间不能跳过目录层级，必须反映完整的物理路径。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成命名空间一致性报告
    /// Generate namespace consistency report
    /// </summary>
    [Fact]
    public void GenerateNamespaceConsistencyReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# PR-RS12: 命名空间与物理路径一致性报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var srcPath = Path.Combine(SolutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var stats = new Dictionary<string, (int Total, int Matched, int Mismatched)>();
        var mismatches = new List<(string File, string Expected, string Actual)>();

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(SolutionRoot, file).Replace("\\", "/");
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
        report.AppendLine("| 项目 | 总文件数 | 匹配 | 不匹配 | 对齐率 |");
        report.AppendLine("|------|----------|------|--------|--------|");

        var totalFiles = 0;
        var totalMatched = 0;
        var totalMismatched = 0;

        foreach (var (project, (total, matched, mismatched)) in stats.OrderBy(kv => kv.Key))
        {
            var status = mismatched > 0 ? "❌" : "✅";
            var rate = total > 0 ? (matched * 100.0 / total).ToString("F1") + "%" : "N/A";
            report.AppendLine($"| {status} {project} | {total} | {matched} | {mismatched} | {rate} |");
            totalFiles += total;
            totalMatched += matched;
            totalMismatched += mismatched;
        }

        var overallRate = totalFiles > 0 ? (totalMatched * 100.0 / totalFiles).ToString("F1") + "%" : "N/A";
        report.AppendLine($"| **总计** | **{totalFiles}** | **{totalMatched}** | **{totalMismatched}** | **{overallRate}** |");

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
        else
        {
            report.AppendLine("\n## ✅ 所有文件命名空间与物理路径完全一致\n");
            report.AppendLine("TD-016 技术债已解决，命名空间与物理路径 100% 对齐。");
        }

        Console.WriteLine(report);
        Assert.True(true, "Report generated successfully");
    }

    #region Helper Methods

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

        // 最少需要 2 个部分（项目文件夹 + 文件名）
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

        // 检查第一个部分是否以项目命名空间前缀 "ZakYip.WheelDiverterSorter." 开头
        if (parts[0].StartsWith(ProjectNamespacePrefix, StringComparison.Ordinal))
        {
            // 特殊结构：项目直接在 src 下
            // parts = [ProjectFolder, ...SubDirs..., File.cs]
            projectFolder = parts[0];
            // 获取子目录（排除文件名）：Skip(1) 跳过项目文件夹，Take(n-2) 排除项目文件夹和文件名
            var subDirCount = Math.Max(0, parts.Length - 2);
            subDirs = parts.Skip(1).Take(subDirCount).ToArray();
        }
        else
        {
            // 标准结构: parts[0] = Category, parts[1] = ProjectFolder, ...
            // parts = [Category, ProjectFolder, ...SubDirs..., File.cs]
            // 最少需要 3 个部分（Category + ProjectFolder + File.cs）
            if (parts.Length < 3)
            {
                // 只有 Category + 文件名，没有项目文件夹
                return null;
            }
            projectFolder = parts[1];
            // 获取子目录（排除文件名）：Skip(2) 跳过 Category 和项目文件夹，Take(n-3) 排除 Category、项目文件夹和文件名
            var subDirCount = Math.Max(0, parts.Length - 3);
            subDirs = parts.Skip(2).Take(subDirCount).ToArray();
        }

        return subDirs.Length > 0
            ? $"{projectFolder}.{string.Join(".", subDirs)}"
            : projectFolder;
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
        
        // 检查是否是特殊结构（Analyzers）
        if (parts.Length >= 2 && parts[1].StartsWith(ProjectNamespacePrefix, StringComparison.Ordinal))
        {
            return parts[1];
        }
        
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
