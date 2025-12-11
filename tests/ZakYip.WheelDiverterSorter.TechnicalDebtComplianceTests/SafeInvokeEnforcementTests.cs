using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 防线测试：确保所有事件调用都使用SafeInvoke，防止订阅者异常影响其他订阅者
/// Defense Line Tests: Ensure all event invocations use SafeInvoke to prevent subscriber exceptions from affecting others
/// </summary>
/// <remarks>
/// PR-SAFE-EVENTS: 建立绝对防线，确保所有EventHandler类型的事件都通过SafeInvoke调用
/// PR-SAFE-EVENTS: Establish absolute defense to ensure all EventHandler events are invoked through SafeInvoke
/// 
/// 相关用户需求：
/// - Comment #3637949120: "所有事件订阅者的异常不能影响其他订阅者和发布者"
/// - Comment #3637950494: "需要建立绝对防线"
/// - Comment #3638026124: "需要建立防线，保证这个解决方案中任何事件代码都遵守"
/// </remarks>
public class SafeInvokeEnforcementTests
{
    private readonly ITestOutputHelper _output;
    private static readonly string SolutionRoot = GetSolutionRoot();
    private static readonly string[] SourceDirectories = { "src/Core", "src/Execution", "src/Ingress", "src/Infrastructure", "src/Drivers", "src/Observability" };

    public SafeInvokeEnforcementTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Solution root not found");
    }

    /// <summary>
    /// 核心防线：禁止在业务代码中直接使用 event?.Invoke() 模式
    /// Core Defense: Prohibit direct event?.Invoke() pattern in business code
    /// </summary>
    [Fact]
    public void AllEventInvocationsMustUseSafeInvoke()
    {
        var violations = new List<string>();
        
        // Regex to find event invocations: event?.Invoke(
        // Excludes SafeInvoke and regular method calls
        var directInvokePattern = new Regex(
            @"\w+\?\s*\.\s*Invoke\s*\(",
            RegexOptions.Compiled);

        foreach (var sourceDir in SourceDirectories)
        {
            var dirPath = Path.Combine(SolutionRoot, sourceDir);
            if (!Directory.Exists(dirPath))
            {
                continue;
            }

            var csFiles = Directory.GetFiles(dirPath, "*.cs", SearchOption.AllDirectories);
            
            foreach (var file in csFiles)
            {
                // Skip test files, generated files, and migration files
                if (file.Contains("/obj/") || file.Contains("/bin/") || 
                    file.Contains(".g.cs") || file.Contains(".Designer.cs") ||
                    file.Contains("Migrations/"))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    
                    // Skip comments
                    if (line.TrimStart().StartsWith("//"))
                    {
                        continue;
                    }

                    // Check for direct event invocation
                    if (directInvokePattern.IsMatch(line))
                    {
                        // Exclude SafeInvoke calls
                        if (!line.Contains("SafeInvoke"))
                        {
                            var relativePath = Path.GetRelativePath(SolutionRoot, file);
                            var lineNumber = i + 1;
                            violations.Add($"{relativePath}:{lineNumber} - {line.Trim()}");
                        }
                    }
                }
            }
        }

        if (violations.Any())
        {
            _output.WriteLine("❌ 发现直接事件调用（未使用SafeInvoke）：");
            _output.WriteLine("❌ Found direct event invocations (not using SafeInvoke):");
            _output.WriteLine("");
            
            foreach (var violation in violations.Take(20)) // Show first 20
            {
                _output.WriteLine($"  {violation}");
            }
            
            if (violations.Count > 20)
            {
                _output.WriteLine($"  ... and {violations.Count - 20} more violations");
            }
            
            _output.WriteLine("");
            _output.WriteLine("修复方法 / Fix:");
            _output.WriteLine("  将 'Event?.Invoke(this, args)' 改为 'Event.SafeInvoke(this, args, logger, nameof(Event))'");
            _output.WriteLine("  Change 'Event?.Invoke(this, args)' to 'Event.SafeInvoke(this, args, logger, nameof(Event))'");
            _output.WriteLine("");
            _output.WriteLine("SafeInvoke 的优势 / Benefits of SafeInvoke:");
            _output.WriteLine("  ✅ 每个订阅者异常被独立捕获");
            _output.WriteLine("  ✅ 一个订阅者崩溃不影响其他订阅者");
            _output.WriteLine("  ✅ 发布者不受订阅者异常影响");
            _output.WriteLine("  ✅ 异常被记录到日志，便于调试");
        }

        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证SafeInvoke扩展方法存在且可访问
    /// Verify SafeInvoke extension method exists and is accessible
    /// </summary>
    [Fact]
    public void SafeInvokeExtensionMethodShouldExist()
    {
        var extensionsFile = Path.Combine(SolutionRoot, "src/Observability/ZakYip.WheelDiverterSorter.Observability/Utilities/EventHandlerExtensions.cs");
        
        Assert.True(File.Exists(extensionsFile), $"EventHandlerExtensions.cs should exist at {extensionsFile}");
        
        var content = File.ReadAllText(extensionsFile);
        
        // Check that the class is public
        Assert.True(content.Contains("public static class EventHandlerExtensions"), "EventHandlerExtensions should be public");
        
        // Check that SafeInvoke methods exist
        Assert.True(content.Contains("public static void SafeInvoke<TEventArgs>"), "SafeInvoke<TEventArgs> method should exist");
        Assert.True(content.Contains("public static void SafeInvoke("), "SafeInvoke overload for simple EventHandler should exist");
        
        // Check that it no longer has EventArgs constraint (supports record class)
        Assert.False(content.Contains("where TEventArgs : EventArgs"), "Should not have EventArgs constraint to support record class");
    }

    /// <summary>
    /// 验证已知的事件发布点都已使用SafeInvoke
    /// Verify known event publisher points are using SafeInvoke
    /// </summary>
    [Theory]
    [InlineData("src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Sensors/LeadshineSensor.cs", "SensorTriggered.SafeInvoke")]
    [InlineData("src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Sensors/MockSensor.cs", "SensorTriggered.SafeInvoke")]
    [InlineData("src/Ingress/ZakYip.WheelDiverterSorter.Ingress/Services/ParcelDetectionService.cs", "ParcelDetected.SafeInvoke")]
    [InlineData("src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/RuleEngineClientBase.cs", "ChuteAssigned.SafeInvoke")]
    [InlineData("src/Execution/ZakYip.WheelDiverterSorter.Execution/Health/NodeHealthRegistry.cs", "NodeHealthChanged.SafeInvoke")]
    public void KnownEventPublishersShouldUseSafeInvoke(string relativeFilePath, string expectedPattern)
    {
        var filePath = Path.Combine(SolutionRoot, relativeFilePath);
        
        Assert.True(File.Exists(filePath), $"File should exist: {filePath}");
        
        var content = File.ReadAllText(filePath);
        
        Assert.True(content.Contains(expectedPattern), 
            $"{Path.GetFileName(filePath)} should use SafeInvoke for event publication. Expected pattern: {expectedPattern}");
    }
}
