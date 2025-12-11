using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System;
using System.Runtime.CompilerServices;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// Module initializer to set up test environment before any tests run
/// This ensures ASPNETCORE_ENVIRONMENT is set to Testing for all integration tests
/// </summary>
internal static class TestEnvironmentSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Set environment to Testing for all integration tests
        // This must be done before WebApplicationFactory creates the host
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }
}
