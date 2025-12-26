namespace ZakYip.WheelDiverterSorter.Observability;

/// <summary>
/// Prometheus metrics stub (removed for performance optimization)
/// </summary>
/// <remarks>
/// This class was removed as part of performance optimization.
/// All metric recording calls are now no-ops.
/// </remarks>
public sealed class PrometheusMetrics
{
    // Empty stub methods for compatibility
    public void RecordSortingFailedParcel(string reason) { }
    public void RecordSortingFailure(double elapsedSeconds) { }
    public void RecordSortingSuccess(double elapsedSeconds) { }
    public void SetDegradedNodesTotal(int count) { }
    public void SetDegradedMode(int degradedCount) { }
    public void RecordPathExecution(double elapsedSeconds) { }  // Accept double parameter
    public void SetHealthCheckStatus(string checkName, int status) { }  // Added for HostHealthStatusProvider
    public void RecordSelfTestFailure() { }
    public void RecordSelfTestSuccess() { }
    public void SetDriverHealthStatus(int status) { }
    public void SetRuleEngineConnectionHealth(int status) { }
    public void SetSystemState(string state) { }
    public void SetTtlSchedulerHealth(int status) { }
    public void SetUpstreamHealthStatus(int status) { }
}
