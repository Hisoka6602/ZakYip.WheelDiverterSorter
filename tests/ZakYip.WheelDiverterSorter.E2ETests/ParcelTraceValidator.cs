using FluentAssertions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// PR-42: 包裹追踪验证器
/// 用于验证 Parcel-First 语义的时间顺序不变式
/// </summary>
public class ParcelTraceValidator
{
    private readonly InMemoryLogCollector _logCollector;
    private readonly ITestOutputHelper? _output;

    public ParcelTraceValidator(InMemoryLogCollector logCollector, ITestOutputHelper? output = null)
    {
        _logCollector = logCollector ?? throw new ArgumentNullException(nameof(logCollector));
        _output = output;
    }

    /// <summary>
    /// 验证包裹的完整追踪链路和时间顺序
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="isDebugMode">是否为调试模式（调试模式下可能没有完整追踪）</param>
    public void ValidateParcelTrace(long parcelId, bool isDebugMode = false)
    {
        var allLogs = _logCollector.GetAllLogs();
        
        // 过滤出与该包裹相关的 PR-42 Trace 日志
        var parcelLogs = allLogs
            .Where(log => 
                log.Level == LogLevel.Trace && 
                log.CategoryName.Contains("ParcelSortingOrchestrator") &&
                log.Message.Contains("[PR-42 Parcel-First]") &&
                log.Message.Contains($"ParcelId={parcelId}"))
            .OrderBy(log => log.Timestamp)
            .ToList();

        _output?.WriteLine($"\n=== 包裹 {parcelId} 的追踪日志 ===");
        
        if (!parcelLogs.Any())
        {
            _output?.WriteLine($"⚠ 未找到包裹 {parcelId} 的 PR-42 Trace 日志");
            
            if (isDebugMode)
            {
                _output?.WriteLine($"ℹ 调试模式：通过 Debug API 触发的包裹可能绕过正常流程，不产生完整追踪日志");
                return; // 调试模式下允许没有追踪日志
            }
            else
            {
                _output?.WriteLine($"❌ 生产模式：包裹应该有完整的追踪日志链路");
            }
        }
        else
        {
            foreach (var log in parcelLogs)
            {
                _output?.WriteLine($"[{log.Timestamp:HH:mm:ss.fff}] {log.Message}");
            }
        }

        // 应该至少有 3 个关键事件：创建、请求上游、路由绑定（非调试模式）
        if (!isDebugMode)
        {
            parcelLogs.Should().HaveCountGreaterOrEqualTo(3, 
                $"包裹 {parcelId} 应该至少有 3 个追踪事件（创建、请求、绑定）");

            // 提取事件
            var createdLog = parcelLogs.FirstOrDefault(l => l.Message.Contains("本地创建包裹"));
            var requestLog = parcelLogs.FirstOrDefault(l => l.Message.Contains("发送上游路由请求"));
            var bindLog = parcelLogs.FirstOrDefault(l => l.Message.Contains("路由绑定完成"));

            // 验证关键事件存在
            createdLog.Should().NotBeNull($"包裹 {parcelId} 应该有创建事件");
            requestLog.Should().NotBeNull($"包裹 {parcelId} 应该有上游请求事件");
            bindLog.Should().NotBeNull($"包裹 {parcelId} 应该有路由绑定事件");

            // 验证时间顺序：t_created < t_request < t_bind
            if (createdLog != null && requestLog != null)
            {
                createdLog.Timestamp.Should().BeBefore(requestLog.Timestamp,
                    "包裹创建时间必须早于上游请求时间");
            }

            if (requestLog != null && bindLog != null)
            {
                requestLog.Timestamp.Should().BeBefore(bindLog.Timestamp,
                    "上游请求时间必须早于路由绑定时间");
            }

            _output?.WriteLine($"✅ 包裹 {parcelId} 的追踪链路完整，时间顺序正确");
        }
    }

    /// <summary>
    /// 验证不存在 Invariant 违反的 Error 日志
    /// </summary>
    public void ValidateNoInvariantViolations()
    {
        var invariantErrors = _logCollector.GetLogs(LogLevel.Error)
            .Where(log => log.Message.Contains("[PR-42 Invariant Violation]"))
            .ToList();

        if (invariantErrors.Any())
        {
            _output?.WriteLine("\n=== ❌ 发现 Invariant 违反错误 ===");
            foreach (var error in invariantErrors)
            {
                _output?.WriteLine($"[{error.Timestamp:HH:mm:ss.fff}] {error.Message}");
            }
        }

        invariantErrors.Should().BeEmpty(
            "不应该存在任何 [PR-42 Invariant Violation] 错误日志");
    }

    /// <summary>
    /// 验证成功场景中没有 Error 日志
    /// </summary>
    public void ValidateNoErrorLogs()
    {
        var errorLogs = _logCollector.GetLogs(LogLevel.Error);

        if (errorLogs.Any())
        {
            _output?.WriteLine("\n=== ❌ 发现 Error 日志 ===");
            foreach (var error in errorLogs.Take(10)) // 只显示前 10 条
            {
                _output?.WriteLine($"[{error.Timestamp:HH:mm:ss.fff}] [{error.CategoryName}] {error.Message}");
            }
        }

        errorLogs.Should().BeEmpty(
            "成功场景中不应该有任何 Error 级别日志");
    }

    /// <summary>
    /// 验证是否有上游请求在包裹创建之前发送（违反 Invariant 1）
    /// </summary>
    public void ValidateNoUpstreamRequestWithoutParcel()
    {
        var allLogs = _logCollector.GetAllLogs();

        // 查找所有"尝试为不存在的包裹发送上游请求"的错误
        var violations = allLogs
            .Where(log => 
                log.Level == LogLevel.Error &&
                log.Message.Contains("[PR-42 Invariant Violation]") &&
                log.Message.Contains("尝试为不存在的包裹"))
            .ToList();

        if (violations.Any())
        {
            _output?.WriteLine("\n=== ❌ 发现无包裹的上游请求 ===");
            foreach (var violation in violations)
            {
                _output?.WriteLine($"[{violation.Timestamp:HH:mm:ss.fff}] {violation.Message}");
            }
        }

        violations.Should().BeEmpty(
            "不应该在包裹创建前发送上游请求（Invariant 1）");
    }

    /// <summary>
    /// 验证是否有路由绑定到不存在的包裹（违反 Invariant 2）
    /// </summary>
    public void ValidateNoRouteBindingToPhantomParcel()
    {
        var allLogs = _logCollector.GetAllLogs();

        // 查找所有"收到未知包裹的路由响应"的错误
        var violations = allLogs
            .Where(log => 
                log.Level == LogLevel.Error &&
                log.Message.Contains("[PR-42 Invariant Violation]") &&
                log.Message.Contains("收到未知包裹"))
            .ToList();

        if (violations.Any())
        {
            _output?.WriteLine("\n=== ❌ 发现幽灵包裹路由绑定 ===");
            foreach (var violation in violations)
            {
                _output?.WriteLine($"[{violation.Timestamp:HH:mm:ss.fff}] {violation.Message}");
            }
        }

        violations.Should().BeEmpty(
            "不应该将路由绑定到不存在的包裹（Invariant 2）");
    }

    /// <summary>
    /// 全面验证 Parcel-First 语义
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="isDebugMode">是否为调试模式（调试模式下可能没有完整追踪）</param>
    public void ValidateParcelFirstSemantics(long parcelId, bool isDebugMode = false)
    {
        _output?.WriteLine($"\n=== 验证 Parcel-First 语义：包裹 {parcelId} ===");

        if (isDebugMode)
        {
            _output?.WriteLine($"ℹ 调试模式：部分验证可能跳过");
        }

        ValidateParcelTrace(parcelId, isDebugMode);
        ValidateNoInvariantViolations();
        ValidateNoUpstreamRequestWithoutParcel();
        ValidateNoRouteBindingToPhantomParcel();
        ValidateNoErrorLogs();

        _output?.WriteLine($"✅ 包裹 {parcelId} 的 Parcel-First 语义验证通过");
    }
}
