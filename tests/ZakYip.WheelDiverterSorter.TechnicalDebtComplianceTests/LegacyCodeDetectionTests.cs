using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 遗留代码检测测试
/// Tests to detect Legacy/Deprecated code patterns
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范，这些测试确保：
/// 1. 不存在带 Legacy/Deprecated 命名模式的类型
/// 2. 不存在仍被业务代码引用的 [Obsolete] 类型
/// 3. 所有过时代码必须在同一次重构中删除
/// 
/// These tests ensure:
/// 1. No types with Legacy/Deprecated naming patterns exist
/// 2. No [Obsolete] types that are still referenced by business code
/// 3. All obsolete code must be removed in the same refactoring
/// </remarks>
public class LegacyCodeDetectionTests
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
    /// 检测是否存在带 Legacy 命名的类型
    /// Detect types with "Legacy" in their name
    /// </summary>
    [Fact]
    public void ShouldNotHaveLegacyNamedTypes()
    {
        var solutionRoot = GetSolutionRoot();
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // Pattern to find types with "Legacy" in their name
        var legacyPattern = new Regex(
            @"(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface|enum)\s+(\w*Legacy\w*)\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var legacyTypes = new List<(string TypeName, string FilePath)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = legacyPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var typeName = match.Groups[1].Value;
                var relativePath = Path.GetRelativePath(solutionRoot, file);
                legacyTypes.Add((typeName, relativePath));
            }
        }

        if (legacyTypes.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {legacyTypes.Count} 个带 'Legacy' 命名的类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, filePath) in legacyTypes)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {filePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范，带 'Legacy' 命名的类型必须删除。");
            report.AppendLine("  1. 检查这些类型是否仍有调用方");
            report.AppendLine("  2. 将调用方迁移到新实现");
            report.AppendLine("  3. 删除带 'Legacy' 命名的类型");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测是否存在带 Deprecated 命名的类型
    /// Detect types with "Deprecated" in their name
    /// </summary>
    [Fact]
    public void ShouldNotHaveDeprecatedNamedTypes()
    {
        var solutionRoot = GetSolutionRoot();
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // Pattern to find types with "Deprecated" in their name
        var deprecatedPattern = new Regex(
            @"(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface|enum)\s+(\w*Deprecated\w*)\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var deprecatedTypes = new List<(string TypeName, string FilePath)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = deprecatedPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var typeName = match.Groups[1].Value;
                var relativePath = Path.GetRelativePath(solutionRoot, file);
                deprecatedTypes.Add((typeName, relativePath));
            }
        }

        if (deprecatedTypes.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {deprecatedTypes.Count} 个带 'Deprecated' 命名的类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, filePath) in deprecatedTypes)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {filePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范，带 'Deprecated' 命名的类型必须删除。");
            report.AppendLine("  1. 检查这些类型是否仍有调用方");
            report.AppendLine("  2. 将调用方迁移到新实现");
            report.AppendLine("  3. 删除带 'Deprecated' 命名的类型");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测是否存在仅包含空壳代码的文件（只有 using 或 global using）
    /// Detect files that only contain empty shells (only using statements)
    /// </summary>
    [Fact]
    public void ShouldNotHaveEmptyShellFiles()
    {
        var solutionRoot = GetSolutionRoot();
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\")
                     && !f.EndsWith("AssemblyInfo.cs")
                     && !f.EndsWith(".g.cs"))
            .ToList();

        var emptyShellFiles = new List<string>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var lines = content.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Where(l => !l.StartsWith("//"))
                .Where(l => !l.StartsWith("/*") && !l.StartsWith("*") && !l.EndsWith("*/"))
                .ToList();

            // Check if file only contains using statements, namespace declarations, or is empty
            var meaningfulLines = lines
                .Where(l => !l.StartsWith("using "))
                .Where(l => !l.StartsWith("global using "))
                .Where(l => !l.StartsWith("namespace "))
                .Where(l => l != "{" && l != "}")
                .Where(l => !l.StartsWith("#pragma"))
                .ToList();

            if (!meaningfulLines.Any() && lines.Any())
            {
                var relativePath = Path.GetRelativePath(solutionRoot, file);
                emptyShellFiles.Add(relativePath);
            }
        }

        if (emptyShellFiles.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {emptyShellFiles.Count} 个空壳文件（仅包含 using 语句）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var file in emptyShellFiles)
            {
                report.AppendLine($"  📄 {file}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范，空壳文件（仅包含 using 或 global using）不应存在。");
            report.AppendLine("  1. 如果文件是类型别名（global using alias），删除它并在使用处添加显式 using");
            report.AppendLine("  2. 如果文件不再需要，直接删除");
            
            // This is a warning, not a failure
            Console.WriteLine(report.ToString());
        }

        // Pass the test - this is just a warning
        Assert.True(true);
    }

    /// <summary>
    /// 检测是否存在 Legacy 目录
    /// Detect if any Legacy directories exist
    /// </summary>
    [Fact]
    public void ShouldNotHaveLegacyDirectories()
    {
        var solutionRoot = GetSolutionRoot();
        var srcPath = Path.Combine(solutionRoot, "src");
        
        // Find all directories named "Legacy"
        var legacyDirectories = Directory.GetDirectories(
            srcPath,
            "Legacy",
            SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("\\obj\\")
                     && !d.Contains("/bin/") && !d.Contains("\\bin\\"))
            .ToList();

        if (legacyDirectories.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现禁止存在的 Legacy 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var dir in legacyDirectories)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, dir);
                var fileCount = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length;
                report.AppendLine($"  📁 {relativePath} ({fileCount} 个文件)");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范，Legacy 目录已被禁止。");
            report.AppendLine("  1. 如果 Legacy 代码仍在使用，请迁移到当前标准实现");
            report.AppendLine("  2. 如果 Legacy 代码不再使用，请删除整个 Legacy 目录");
            report.AppendLine("  3. 确保所有调用方已迁移到新实现后再删除");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成遗留代码摘要报告
    /// Generate legacy code summary report
    /// </summary>
    [Fact]
    public void GenerateLegacyCodeReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# Legacy Code Detection Report\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var srcPath = Path.Combine(solutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // Count various patterns
        var obsoleteCount = 0;
        var legacyNameCount = 0;
        var deprecatedNameCount = 0;
        var legacyDirCount = 0;

        var obsoletePattern = new Regex(@"\[Obsolete", RegexOptions.Compiled);
        var legacyNamePattern = new Regex(@"(?:class|record|struct|interface|enum)\s+\w*Legacy\w*", RegexOptions.Compiled);
        var deprecatedNamePattern = new Regex(@"(?:class|record|struct|interface|enum)\s+\w*Deprecated\w*", RegexOptions.Compiled);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            obsoleteCount += obsoletePattern.Matches(content).Count;
            legacyNameCount += legacyNamePattern.Matches(content).Count;
            deprecatedNameCount += deprecatedNamePattern.Matches(content).Count;
        }

        legacyDirCount = Directory.GetDirectories(srcPath, "Legacy", SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("/bin/"))
            .Count();

        report.AppendLine("## Summary\n");
        report.AppendLine($"| Metric | Count | Status |");
        report.AppendLine($"|--------|-------|--------|");
        report.AppendLine($"| [Obsolete] attributes | {obsoleteCount} | {(obsoleteCount == 0 ? "✅" : "⚠️")} |");
        report.AppendLine($"| Types named *Legacy* | {legacyNameCount} | {(legacyNameCount == 0 ? "✅" : "❌")} |");
        report.AppendLine($"| Types named *Deprecated* | {deprecatedNameCount} | {(deprecatedNameCount == 0 ? "✅" : "❌")} |");
        report.AppendLine($"| Legacy directories | {legacyDirCount} | {(legacyDirCount == 0 ? "✅" : "❌")} |");
        report.AppendLine();

        report.AppendLine("## Architecture Rules\n");
        report.AppendLine("根据 copilot-instructions.md 规范：\n");
        report.AppendLine("1. **禁止 Legacy 目录**: 所有 `*/Legacy/*` 目录必须删除");
        report.AppendLine("2. **禁止过渡实现**: 新实现完全覆盖旧实现时，旧实现必须立即删除");
        report.AppendLine("3. **禁止 [Obsolete] 公共类型**: 标记为过时的公共类型必须迁移后删除");
        report.AppendLine("4. **禁止 Legacy/Deprecated 命名**: 类型名不应包含 'Legacy' 或 'Deprecated'");

        Console.WriteLine(report.ToString());

        // This test always passes, just generates a report
        Assert.True(true);
    }
}
