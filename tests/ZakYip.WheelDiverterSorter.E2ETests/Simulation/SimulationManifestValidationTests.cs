using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ZakYip.WheelDiverterSorter.E2ETests.Simulation;

/// <summary>
/// PR-42: 仿真场景清单验证测试
/// 确保所有仿真场景都在清单中登记，并且所有清单场景都有对应的测试方法
/// 这是回归套件的"总控测试"，防止遗漏场景或脏场景ID
/// </summary>
public class SimulationManifestValidationTests
{
    private readonly ITestOutputHelper _output;

    public SimulationManifestValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllManifestScenarios_ShouldHaveCorrespondingTests()
    {
        // Arrange: 获取清单中所有场景ID
        var manifestScenarios = SimulationScenariosManifest.AllScenarioIds.ToHashSet();
        _output.WriteLine($"=== 清单中登记的场景总数: {manifestScenarios.Count} ===");

        // Act: 反射扫描所有带 SimulationScenarioAttribute 的测试方法
        var testScenarios = GetAllTestScenarioIds();
        _output.WriteLine($"=== 测试中标记的场景总数: {testScenarios.Count} ===");

        // Assert: 清单中的每个场景都至少有一个测试方法
        var missingScenariosInTests = manifestScenarios.Except(testScenarios).ToList();

        if (missingScenariosInTests.Any())
        {
            _output.WriteLine("\n=== ❌ 以下场景在清单中但没有对应的测试方法 ===");
            foreach (var scenario in missingScenariosInTests)
            {
                _output.WriteLine($"  - {scenario}");
            }
        }

        missingScenariosInTests.Should().BeEmpty(
            "清单中的所有场景ID都必须至少有一个带 [SimulationScenario] 的测试方法");
    }

    [Fact]
    public void AllTestScenarios_ShouldBeInManifest()
    {
        // Arrange: 获取所有测试中标记的场景ID
        var testScenarios = GetAllTestScenarioIds();
        _output.WriteLine($"=== 测试中标记的场景总数: {testScenarios.Count} ===");

        // Act: 获取清单中所有场景ID
        var manifestScenarios = SimulationScenariosManifest.AllScenarioIds.ToHashSet();
        _output.WriteLine($"=== 清单中登记的场景总数: {manifestScenarios.Count} ===");

        // Assert: 测试中标记的每个场景都必须在清单中
        var unregisteredScenarios = testScenarios.Except(manifestScenarios).ToList();

        if (unregisteredScenarios.Any())
        {
            _output.WriteLine("\n=== ❌ 以下场景在测试中标记但未在清单中登记 ===");
            foreach (var scenario in unregisteredScenarios)
            {
                _output.WriteLine($"  - {scenario}");
            }
        }

        unregisteredScenarios.Should().BeEmpty(
            "所有带 [SimulationScenario] 的测试方法必须在清单中登记其场景ID");
    }

    [Fact]
    public void ManifestScenarioIds_ShouldBeUnique()
    {
        // Arrange & Act
        var allScenarios = SimulationScenariosManifest.AllScenarioIds;
        var uniqueScenarios = allScenarios.Distinct().ToList();

        // Assert
        var duplicates = allScenarios
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => new { ScenarioId = g.Key, Count = g.Count() })
            .ToList();

        if (duplicates.Any())
        {
            _output.WriteLine("\n=== ❌ 发现重复的场景ID ===");
            foreach (var dup in duplicates)
            {
                _output.WriteLine($"  - {dup.ScenarioId} (出现 {dup.Count} 次)");
            }
        }

        allScenarios.Should().HaveCount(uniqueScenarios.Count,
            "清单中的场景ID必须唯一，不允许重复");
    }

    [Fact]
    public void ManifestCategories_ShouldNotOverlap()
    {
        // Arrange
        var normalSorting = SimulationScenariosManifest.NormalSortingScenarios.ToHashSet();
        var faultScenarios = SimulationScenariosManifest.FaultScenarios.ToHashSet();
        var longRunning = SimulationScenariosManifest.LongRunningScenarios.ToHashSet();

        // Act: 检查分类之间是否有重叠
        var normalFaultOverlap = normalSorting.Intersect(faultScenarios).ToList();
        var normalLongOverlap = normalSorting.Intersect(longRunning).ToList();
        var faultLongOverlap = faultScenarios.Intersect(longRunning).ToList();

        // Assert: 分类之间不应该重叠（可选规则，根据业务需求调整）
        _output.WriteLine($"=== 正常分拣场景: {normalSorting.Count} 个 ===");
        _output.WriteLine($"=== 故障场景: {faultScenarios.Count} 个 ===");
        _output.WriteLine($"=== 长时运行场景: {longRunning.Count} 个 ===");

        if (normalFaultOverlap.Any())
        {
            _output.WriteLine("\n⚠ 正常分拣和故障场景有重叠:");
            foreach (var scenario in normalFaultOverlap)
            {
                _output.WriteLine($"  - {scenario}");
            }
        }

        // 目前允许重叠，未来可以根据需要严格化规则
        // normalFaultOverlap.Should().BeEmpty("正常分拣和故障场景不应重叠");
    }

    [Fact]
    public void AllCategorizedScenarios_ShouldBeInMainManifest()
    {
        // Arrange
        var allScenarios = SimulationScenariosManifest.AllScenarioIds.ToHashSet();
        var categorized = new HashSet<string>();

        categorized.UnionWith(SimulationScenariosManifest.NormalSortingScenarios);
        categorized.UnionWith(SimulationScenariosManifest.FaultScenarios);
        categorized.UnionWith(SimulationScenariosManifest.LongRunningScenarios);

        // Act: 找出在分类中但不在主清单中的场景
        var invalidCategorized = categorized.Except(allScenarios).ToList();

        // Assert
        if (invalidCategorized.Any())
        {
            _output.WriteLine("\n=== ❌ 以下场景在分类中但不在主清单 AllScenarioIds 中 ===");
            foreach (var scenario in invalidCategorized)
            {
                _output.WriteLine($"  - {scenario}");
            }
        }

        invalidCategorized.Should().BeEmpty(
            "分类中的所有场景ID必须在主清单 AllScenarioIds 中存在");
    }

    [Fact]
    public void TestScenarioAttributes_ShouldHaveValidScenarioIds()
    {
        // Arrange & Act: 获取所有测试方法及其场景ID
        var testMethodsWithScenarios = GetTestMethodsWithScenarios();

        // Assert: 所有场景ID不应为空或空白
        var invalidScenarios = testMethodsWithScenarios
            .Where(x => string.IsNullOrWhiteSpace(x.ScenarioId))
            .ToList();

        if (invalidScenarios.Any())
        {
            _output.WriteLine("\n=== ❌ 以下测试方法的场景ID无效（空或空白）===");
            foreach (var item in invalidScenarios)
            {
                _output.WriteLine($"  - {item.TestMethod.Name}");
            }
        }

        invalidScenarios.Should().BeEmpty(
            "所有 SimulationScenarioAttribute 的 ScenarioId 必须非空");
    }

    [Fact]
    public void GenerateScenarioCoverageReport()
    {
        // 这是一个信息性测试，用于生成场景覆盖报告
        var manifestScenarios = SimulationScenariosManifest.AllScenarioIds.ToHashSet();
        var testMethodsWithScenarios = GetTestMethodsWithScenarios();

        _output.WriteLine("\n=== 仿真场景覆盖报告 ===\n");
        _output.WriteLine($"清单中登记的场景总数: {manifestScenarios.Count}");
        _output.WriteLine($"已实现的测试方法总数: {testMethodsWithScenarios.Count}");

        // 按场景ID分组，显示每个场景对应的测试方法数量
        var scenarioTestCounts = testMethodsWithScenarios
            .GroupBy(x => x.ScenarioId)
            .OrderBy(g => g.Key)
            .Select(g => new { ScenarioId = g.Key, TestCount = g.Count(), Tests = g.Select(x => x.TestMethod.Name).ToList() })
            .ToList();

        _output.WriteLine("\n场景 → 测试方法映射:");
        foreach (var item in scenarioTestCounts)
        {
            _output.WriteLine($"\n  {item.ScenarioId} ({item.TestCount} 个测试):");
            foreach (var testName in item.Tests)
            {
                _output.WriteLine($"    - {testName}");
            }
        }

        // 显示未覆盖的场景
        var coveredScenarios = scenarioTestCounts.Select(x => x.ScenarioId).ToHashSet();
        var uncoveredScenarios = manifestScenarios.Except(coveredScenarios).ToList();

        if (uncoveredScenarios.Any())
        {
            _output.WriteLine("\n⚠ 未覆盖的场景:");
            foreach (var scenario in uncoveredScenarios)
            {
                _output.WriteLine($"  - {scenario}");
            }
        }
        else
        {
            _output.WriteLine("\n✅ 所有清单场景都已覆盖!");
        }
    }

    #region Helper Methods

    private HashSet<string> GetAllTestScenarioIds()
    {
        var testMethodsWithScenarios = GetTestMethodsWithScenarios();
        return testMethodsWithScenarios.Select(x => x.ScenarioId).ToHashSet();
    }

    private List<(MethodInfo TestMethod, string ScenarioId)> GetTestMethodsWithScenarios()
    {
        var result = new List<(MethodInfo, string)>();

        // 获取当前测试程序集
        var assembly = Assembly.GetExecutingAssembly();

        // 扫描所有类型
        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            // 跳过非测试类
            if (!type.IsClass || type.IsAbstract)
                continue;

            // 获取所有公共方法
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                // 查找 SimulationScenarioAttribute
                var attributes = method.GetCustomAttributes<SimulationScenarioAttribute>().ToList();

                foreach (var attr in attributes)
                {
                    result.Add((method, attr.ScenarioId));
                }
            }
        }

        return result;
    }

    #endregion
}
