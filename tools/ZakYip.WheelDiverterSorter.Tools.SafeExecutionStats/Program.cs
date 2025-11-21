using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.Tools.SafeExecutionStats;

/// <summary>
/// 统计工具：分析代码库中 SafeExecutionService 的使用情况
/// Statistics tool: Analyze SafeExecutionService usage in the codebase
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        var rootPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        
        if (!Directory.Exists(rootPath))
        {
            Console.Error.WriteLine($"Error: Directory not found: {rootPath}");
            return 1;
        }

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("SafeExecutionService Usage Statistics");
        Console.WriteLine("SafeExecutionService 使用情况统计");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var stats = AnalyzeCodebase(rootPath);
        
        PrintStatistics(stats);
        
        return 0;
    }

    private static CodeStatistics AnalyzeCodebase(string rootPath)
    {
        var stats = new CodeStatistics();
        
        // Find all C# files in src directory (excluding tests and tools)
        var srcPath = Path.Combine(rootPath, "src");
        if (!Directory.Exists(srcPath))
        {
            Console.WriteLine($"Warning: src directory not found at {srcPath}");
            return stats;
        }

        var csFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in csFiles)
        {
            AnalyzeFile(file, stats);
        }
        
        return stats;
    }

    private static void AnalyzeFile(string filePath, CodeStatistics stats)
    {
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);
        
        // Check for SafeExecutionService usage
        var safeExecutionMatches = Regex.Matches(content, 
            @"(?:ISafeExecutionService|SafeExecutionService)\.ExecuteAsync",
            RegexOptions.Multiline);
        
        if (safeExecutionMatches.Count > 0)
        {
            stats.FilesUsingSafeExecution++;
            stats.TotalSafeExecutionCalls += safeExecutionMatches.Count;
            stats.FilesWithSafeExecution.Add(filePath);
        }
        
        // Check for BackgroundService without SafeExecutionService
        if (content.Contains(": BackgroundService") || 
            content.Contains(":BackgroundService"))
        {
            stats.TotalBackgroundServices++;
            
            var hasExecuteAsync = Regex.IsMatch(content, 
                @"protected\s+override\s+async\s+Task\s+ExecuteAsync",
                RegexOptions.Multiline);
            
            if (hasExecuteAsync)
            {
                var hasSafeExecution = safeExecutionMatches.Count > 0;
                
                if (!hasSafeExecution)
                {
                    stats.BackgroundServicesWithoutSafeExecution++;
                    stats.BackgroundServicesWithoutSafeExecutionFiles.Add(filePath);
                }
                else
                {
                    stats.BackgroundServicesWithSafeExecution++;
                }
            }
        }
        
        // Check for DateTime.Now or DateTime.UtcNow usage (excluding SystemClock implementations)
        if (!fileName.Contains("SystemClock") && !fileName.Contains("TestClock"))
        {
            var dateTimeMatches = Regex.Matches(content,
                @"DateTime\.(Now|UtcNow)",
                RegexOptions.Multiline);
            
            if (dateTimeMatches.Count > 0)
            {
                stats.FilesWithDateTimeUsage++;
                stats.TotalDateTimeUsages += dateTimeMatches.Count;
            }
        }
    }

    private static void PrintStatistics(CodeStatistics stats)
    {
        Console.WriteLine("📊 Overall Statistics / 总体统计");
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine($"  SafeExecutionService.ExecuteAsync calls: {stats.TotalSafeExecutionCalls}");
        Console.WriteLine($"  Files using SafeExecutionService: {stats.FilesUsingSafeExecution}");
        Console.WriteLine();
        
        Console.WriteLine("🔒 BackgroundService Analysis / BackgroundService 分析");
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine($"  Total BackgroundService classes: {stats.TotalBackgroundServices}");
        Console.WriteLine($"  ✅ With SafeExecutionService: {stats.BackgroundServicesWithSafeExecution}");
        Console.WriteLine($"  ⚠️  Without SafeExecutionService: {stats.BackgroundServicesWithoutSafeExecution}");
        
        if (stats.BackgroundServicesWithoutSafeExecution > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Files needing SafeExecutionService:");
            foreach (var file in stats.BackgroundServicesWithoutSafeExecutionFiles)
            {
                var relativePath = GetRelativePath(file);
                Console.WriteLine($"    - {relativePath}");
            }
        }
        
        Console.WriteLine();
        Console.WriteLine("⏰ DateTime Usage Analysis / DateTime 使用分析");
        Console.WriteLine("-".PadRight(80, '-'));
        Console.WriteLine($"  Files with DateTime.Now/UtcNow usage: {stats.FilesWithDateTimeUsage}");
        Console.WriteLine($"  Total DateTime.Now/UtcNow calls: {stats.TotalDateTimeUsages}");
        
        Console.WriteLine();
        Console.WriteLine("📈 Trend Indicators / 趋势指标");
        Console.WriteLine("-".PadRight(80, '-'));
        
        var safeExecutionCoverage = stats.TotalBackgroundServices > 0
            ? (double)stats.BackgroundServicesWithSafeExecution / stats.TotalBackgroundServices * 100
            : 0;
        
        Console.WriteLine($"  SafeExecution coverage: {safeExecutionCoverage:F1}%");
        
        if (stats.BackgroundServicesWithoutSafeExecution == 0)
        {
            Console.WriteLine("  ✅ All BackgroundServices use SafeExecutionService!");
        }
        else
        {
            Console.WriteLine($"  ⚠️  {stats.BackgroundServicesWithoutSafeExecution} BackgroundService(s) need SafeExecutionService");
        }
        
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("📝 Note: These statistics help track progress in adopting SafeExecutionService.");
        Console.WriteLine("   Goal: 100% coverage for all BackgroundService classes.");
        Console.WriteLine("=".PadRight(80, '='));
    }

    private static string GetRelativePath(string fullPath)
    {
        var currentDir = Directory.GetCurrentDirectory();
        if (fullPath.StartsWith(currentDir))
        {
            return fullPath.Substring(currentDir.Length).TrimStart(Path.DirectorySeparatorChar);
        }
        return fullPath;
    }
}

class CodeStatistics
{
    public int TotalSafeExecutionCalls { get; set; }
    public int FilesUsingSafeExecution { get; set; }
    public List<string> FilesWithSafeExecution { get; } = new();
    
    public int TotalBackgroundServices { get; set; }
    public int BackgroundServicesWithSafeExecution { get; set; }
    public int BackgroundServicesWithoutSafeExecution { get; set; }
    public List<string> BackgroundServicesWithoutSafeExecutionFiles { get; } = new();
    
    public int FilesWithDateTimeUsage { get; set; }
    public int TotalDateTimeUsages { get; set; }
}
