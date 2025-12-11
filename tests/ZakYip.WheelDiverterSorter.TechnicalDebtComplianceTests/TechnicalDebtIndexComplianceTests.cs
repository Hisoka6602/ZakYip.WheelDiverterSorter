using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-TD-ZERO02: 技术债索引合规测试 - 零技术债假设
/// Technical Debt Index Compliance Tests - Zero Technical Debt Assumption
/// </summary>
/// <remarks>
/// 此测试类验证技术债索引中不存在未完成的技术债条目。
/// 确保所有技术债务已被解决，防止静默堆积新的技术债。
/// 
/// 工作流程：
/// 1. 新增技术债时，在 TechnicalDebtLog.md 中登记（默认状态为「❌ 未开始」或「⏳ 进行中」）
/// 2. 在同一 PR 中同步更新 RepositoryStructure.md 与本测试
/// 3. 完成后更新状态为「✅ 已解决」
/// 4. 恢复「零技术债测试」通过
/// 
/// 环境变量控制：
/// - ALLOW_PENDING_TECHNICAL_DEBT=true: 允许存在未完成的技术债（用于引入新技术债时临时禁用检查）
/// </remarks>
public partial class TechnicalDebtIndexComplianceTests
{
    /// <summary>
    /// 环境变量名称：允许存在未完成的技术债
    /// </summary>
    private const string AllowPendingTechnicalDebtEnvVar = "ALLOW_PENDING_TECHNICAL_DEBT";

    /// <summary>
    /// 技术债索引表中表示"进行中"状态的标记
    /// </summary>
    private const string PendingStatusMarker = "⏳";

    /// <summary>
    /// 技术债索引表中表示"未开始"状态的标记
    /// </summary>
    private const string NotStartedStatusMarker = "❌";

    /// <summary>
    /// 技术债索引表中表示"已解决"状态的标记
    /// </summary>
    private const string ResolvedStatusMarker = "✅";

    // 编译的正则表达式用于解析技术债索引表
    [GeneratedRegex(@"^\|\s*(?<id>TD-\d+)\s*\|\s*(?<status>[⏳❌✅][^\|]*)\s*\|\s*(?<summary>[^\|]*)\s*\|", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex TechnicalDebtEntryPattern();

    // 编译的正则表达式用于解析技术债统计表
    [GeneratedRegex(@"^\|\s*(?<label>✅\s*已解决|⏳\s*进行中|❌\s*未开始)\s*\|\s*(?<count>\d+)\s*\|", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex TechnicalDebtStatsPattern();

    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }

    private static string GetRepositoryStructurePath()
    {
        return Path.Combine(GetSolutionRoot(), "docs", "RepositoryStructure.md");
    }

    private static string GetTechnicalDebtLogPath()
    {
        return Path.Combine(GetSolutionRoot(), "docs", "TechnicalDebtLog.md");
    }

    private static bool IsAllowPendingTechnicalDebtEnabled()
    {
        var envValue = Environment.GetEnvironmentVariable(AllowPendingTechnicalDebtEnvVar);
        return string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PR-TD-ZERO02: 验证技术债索引中不存在未完成的条目
    /// Verify that technical debt index does not contain pending or not-started items
    /// </summary>
    /// <remarks>
    /// 零技术债假设测试：
    /// - 断言当前环境下不存在状态为「⏳ 进行中」或「❌ 未开始」的条目
    /// - 可通过环境变量 ALLOW_PENDING_TECHNICAL_DEBT=true 临时禁用此检查
    /// 
    /// 使用场景：
    /// - 日常 CI 运行时，确保技术债务归零
    /// - 引入新技术债时，可设置环境变量临时禁用，直到技术债解决
    /// </remarks>
    [Fact]
    public void TechnicalDebtIndexShouldNotContainPendingItems()
    {
        // 检查是否允许未完成的技术债
        if (IsAllowPendingTechnicalDebtEnabled())
        {
            Console.WriteLine($"⚠️ 环境变量 {AllowPendingTechnicalDebtEnvVar}=true，跳过零技术债检查。");
            Console.WriteLine("   请在技术债解决后移除此环境变量设置。");
            return;
        }

        var repositoryStructurePath = GetRepositoryStructurePath();
        Assert.True(File.Exists(repositoryStructurePath),
            $"RepositoryStructure.md 文件不存在: {repositoryStructurePath}");

        var content = File.ReadAllText(repositoryStructurePath);
        var matches = TechnicalDebtEntryPattern().Matches(content);

        var pendingItems = new List<(string Id, string Status, string Summary)>();
        var notStartedItems = new List<(string Id, string Status, string Summary)>();

        foreach (Match match in matches)
        {
            var id = match.Groups["id"].Value.Trim();
            var status = match.Groups["status"].Value.Trim();
            var summary = match.Groups["summary"].Value.Trim();

            // 状态判断：优先检查以哪个状态标记开头
            if (status.StartsWith(PendingStatusMarker))
            {
                pendingItems.Add((id, status, summary));
            }
            else if (status.StartsWith(NotStartedStatusMarker))
            {
                notStartedItems.Add((id, status, summary));
            }
            // 以 ✅ 开头的已解决状态不需要记录
        }

        if (pendingItems.Count > 0 || notStartedItems.Count > 0)
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 零技术债假设违规: 存在未完成的技术债条目");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (pendingItems.Count > 0)
            {
                report.AppendLine($"\n⏳ 进行中的技术债 ({pendingItems.Count} 条)：");
                foreach (var (id, status, summary) in pendingItems)
                {
                    report.AppendLine($"  - {id}: {summary}");
                }
            }

            if (notStartedItems.Count > 0)
            {
                report.AppendLine($"\n❌ 未开始的技术债 ({notStartedItems.Count} 条)：");
                foreach (var (id, status, summary) in notStartedItems)
                {
                    report.AppendLine($"  - {id}: {summary}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 完成未完成的技术债，将状态更新为「✅ 已解决」");
            report.AppendLine("  2. 如果正在引入新技术债，请确保在同一 PR 中完成");
            report.AppendLine($"  3. 临时跳过检查: 设置环境变量 {AllowPendingTechnicalDebtEnvVar}=true");
            report.AppendLine("  4. 参考 docs/TechnicalDebtLog.md 了解各技术债的详细说明");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-TD-ZERO02: 验证技术债统计数据一致性
    /// Verify that technical debt statistics are consistent
    /// </summary>
    /// <remarks>
    /// 验证：
    /// - 统计表中的数量与实际条目数量一致
    /// - 已解决 + 进行中 + 未开始 = 总计
    /// </remarks>
    [Fact]
    public void TechnicalDebtStatisticsShouldBeConsistent()
    {
        var repositoryStructurePath = GetRepositoryStructurePath();
        Assert.True(File.Exists(repositoryStructurePath),
            $"RepositoryStructure.md 文件不存在: {repositoryStructurePath}");

        var content = File.ReadAllText(repositoryStructurePath);

        // 统计实际条目
        var entryMatches = TechnicalDebtEntryPattern().Matches(content);
        var actualResolved = 0;
        var actualPending = 0;
        var actualNotStarted = 0;

        foreach (Match match in entryMatches)
        {
            var status = match.Groups["status"].Value.Trim();

            // 使用 StartsWith 确保准确判断状态
            if (status.StartsWith(ResolvedStatusMarker))
            {
                actualResolved++;
            }
            else if (status.StartsWith(PendingStatusMarker))
            {
                actualPending++;
            }
            else if (status.StartsWith(NotStartedStatusMarker))
            {
                actualNotStarted++;
            }
        }

        // 解析统计表
        var statsMatches = TechnicalDebtStatsPattern().Matches(content);
        var reportedResolved = 0;
        var reportedPending = 0;
        var reportedNotStarted = 0;

        foreach (Match match in statsMatches)
        {
            var statusLabel = match.Groups["label"].Value.Trim();
            var countText = match.Groups["count"].Value.Trim();
            
            if (!int.TryParse(countText, out var count))
            {
                // 跳过无法解析的条目，继续处理其他条目
                continue;
            }

            if (statusLabel.Contains("已解决"))
            {
                reportedResolved = count;
            }
            else if (statusLabel.Contains("进行中"))
            {
                reportedPending = count;
            }
            else if (statusLabel.Contains("未开始"))
            {
                reportedNotStarted = count;
            }
        }

        var violations = new List<string>();

        if (actualResolved != reportedResolved)
        {
            violations.Add($"已解决数量不一致: 实际={actualResolved}, 统计表={reportedResolved}");
        }

        if (actualPending != reportedPending)
        {
            violations.Add($"进行中数量不一致: 实际={actualPending}, 统计表={reportedPending}");
        }

        if (actualNotStarted != reportedNotStarted)
        {
            violations.Add($"未开始数量不一致: 实际={actualNotStarted}, 统计表={reportedNotStarted}");
        }

        var actualTotal = actualResolved + actualPending + actualNotStarted;
        var reportedTotal = reportedResolved + reportedPending + reportedNotStarted;

        if (actualTotal != reportedTotal)
        {
            violations.Add($"总数不一致: 实际={actualTotal}, 统计表={reportedTotal}");
        }

        if (violations.Count > 0)
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 技术债统计数据不一致:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations)
            {
                report.AppendLine($"  ❌ {violation}");
            }

            report.AppendLine("\n实际统计:");
            report.AppendLine($"  ✅ 已解决: {actualResolved}");
            report.AppendLine($"  ⏳ 进行中: {actualPending}");
            report.AppendLine($"  ❌ 未开始: {actualNotStarted}");
            report.AppendLine($"  总计: {actualTotal}");

            report.AppendLine("\n统计表显示:");
            report.AppendLine($"  ✅ 已解决: {reportedResolved}");
            report.AppendLine($"  ⏳ 进行中: {reportedPending}");
            report.AppendLine($"  ❌ 未开始: {reportedNotStarted}");
            report.AppendLine($"  总计: {reportedTotal}");

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  更新 RepositoryStructure.md 中的技术债统计表，");
            report.AppendLine("  使其与索引表中的实际条目数量一致。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-TD-ZERO02: 验证 TechnicalDebtLog.md 与 RepositoryStructure.md 的技术债条目一致
    /// Verify that TechnicalDebtLog.md and RepositoryStructure.md have consistent entries
    /// </summary>
    [Fact]
    public void TechnicalDebtEntriesShouldBeConsistentBetweenDocuments()
    {
        var repositoryStructurePath = GetRepositoryStructurePath();
        var technicalDebtLogPath = GetTechnicalDebtLogPath();

        Assert.True(File.Exists(repositoryStructurePath),
            $"RepositoryStructure.md 文件不存在: {repositoryStructurePath}");
        Assert.True(File.Exists(technicalDebtLogPath),
            $"TechnicalDebtLog.md 文件不存在: {technicalDebtLogPath}");

        var repositoryStructureContent = File.ReadAllText(repositoryStructurePath);
        var technicalDebtLogContent = File.ReadAllText(technicalDebtLogPath);

        // 从 RepositoryStructure.md 提取技术债 ID（使用 Ordinal 比较保持大小写一致性）
        var indexMatches = TechnicalDebtEntryPattern().Matches(repositoryStructureContent);
        var indexIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in indexMatches)
        {
            indexIds.Add(match.Groups["id"].Value.Trim());
        }

        // 从 TechnicalDebtLog.md 提取章节标题中的技术债 ID（使用 Ordinal 比较保持大小写一致性）
        var logIdPattern = TechnicalDebtLogIdPattern();
        var logMatches = logIdPattern.Matches(technicalDebtLogContent);
        var logIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in logMatches)
        {
            logIds.Add(match.Groups["id"].Value.Trim());
        }

        var missingInLog = indexIds.Except(logIds).ToList();
        var missingInIndex = logIds.Except(indexIds).ToList();

        if (missingInLog.Count > 0 || missingInIndex.Count > 0)
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 技术债文档不一致:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (missingInLog.Count > 0)
            {
                report.AppendLine($"\n在 TechnicalDebtLog.md 中缺失的条目 ({missingInLog.Count} 条)：");
                foreach (var id in missingInLog.OrderBy(x => x))
                {
                    report.AppendLine($"  ❌ {id}");
                }
            }

            if (missingInIndex.Count > 0)
            {
                report.AppendLine($"\n在 RepositoryStructure.md 索引中缺失的条目 ({missingInIndex.Count} 条)：");
                foreach (var id in missingInIndex.OrderBy(x => x))
                {
                    report.AppendLine($"  ❌ {id}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  确保 RepositoryStructure.md 和 TechnicalDebtLog.md 中的");
            report.AppendLine("  技术债条目完全同步。");

            Assert.Fail(report.ToString());
        }
    }

    [GeneratedRegex(@"##\s*\[(?<id>TD-\d+)\]", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex TechnicalDebtLogIdPattern();

    /// <summary>
    /// PR-TD-ZERO02: 生成技术债状态报告
    /// Generate technical debt status report
    /// </summary>
    /// <remarks>
    /// 此测试生成技术债务的状态报告，便于审查当前状态。
    /// 测试始终通过，仅用于输出报告。
    /// </remarks>
    [Fact]
    public void GenerateTechnicalDebtStatusReport()
    {
        var repositoryStructurePath = GetRepositoryStructurePath();

        if (!File.Exists(repositoryStructurePath))
        {
            Console.WriteLine($"RepositoryStructure.md 文件不存在: {repositoryStructurePath}");
            return;
        }

        var content = File.ReadAllText(repositoryStructurePath);
        var matches = TechnicalDebtEntryPattern().Matches(content);

        var resolvedItems = new List<(string Id, string Summary)>();
        var pendingItems = new List<(string Id, string Summary)>();
        var notStartedItems = new List<(string Id, string Summary)>();

        foreach (Match match in matches)
        {
            var id = match.Groups["id"].Value.Trim();
            var status = match.Groups["status"].Value.Trim();
            var summary = match.Groups["summary"].Value.Trim();

            // 使用 StartsWith 确保准确判断状态
            if (status.StartsWith(ResolvedStatusMarker))
            {
                resolvedItems.Add((id, summary));
            }
            else if (status.StartsWith(PendingStatusMarker))
            {
                pendingItems.Add((id, summary));
            }
            else if (status.StartsWith(NotStartedStatusMarker))
            {
                notStartedItems.Add((id, summary));
            }
        }

        var report = new StringBuilder();
        report.AppendLine("\n📊 技术债状态报告");
        report.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        report.AppendLine($"\n📈 统计摘要:");
        report.AppendLine($"  ✅ 已解决: {resolvedItems.Count}");
        report.AppendLine($"  ⏳ 进行中: {pendingItems.Count}");
        report.AppendLine($"  ❌ 未开始: {notStartedItems.Count}");
        report.AppendLine($"  总计: {resolvedItems.Count + pendingItems.Count + notStartedItems.Count}");

        if (pendingItems.Count == 0 && notStartedItems.Count == 0)
        {
            report.AppendLine("\n🎉 恭喜！所有技术债已解决！");
        }
        else
        {
            if (pendingItems.Count > 0)
            {
                report.AppendLine($"\n⏳ 进行中 ({pendingItems.Count} 条):");
                foreach (var (id, summary) in pendingItems)
                {
                    report.AppendLine($"  - {id}: {summary}");
                }
            }

            if (notStartedItems.Count > 0)
            {
                report.AppendLine($"\n❌ 未开始 ({notStartedItems.Count} 条):");
                foreach (var (id, summary) in notStartedItems)
                {
                    report.AppendLine($"  - {id}: {summary}");
                }
            }
        }

        report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // 检查环境变量状态
        if (IsAllowPendingTechnicalDebtEnabled())
        {
            report.AppendLine($"\n⚠️ 注意: 环境变量 {AllowPendingTechnicalDebtEnvVar}=true");
            report.AppendLine("   零技术债检查已临时禁用。");
        }

        Console.WriteLine(report);

        Assert.True(true, "Report generated successfully");
    }
}
