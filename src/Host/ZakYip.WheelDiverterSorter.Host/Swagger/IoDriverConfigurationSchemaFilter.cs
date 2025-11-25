using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Swagger;

/// <summary>
/// Swagger Schema Filter that dynamically filters vendor-specific properties
/// in IO Driver Configuration based on the currently configured VendorType.
/// This ensures that only the relevant vendor configuration is shown in Swagger UI.
/// </summary>
public class IoDriverConfigurationSchemaFilter : ISchemaFilter
{
    private readonly IDriverConfigurationRepository _repository;

    public IoDriverConfigurationSchemaFilter(IDriverConfigurationRepository repository)
    {
        _repository = repository;
    }

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Only apply to DriverConfiguration type
        if (context.Type != typeof(DriverConfiguration))
        {
            return;
        }

        // Get current driver configuration to determine active vendor
        DriverConfiguration currentConfig;
        try
        {
            currentConfig = _repository.Get();
        }
        catch
        {
            // If we can't get the configuration (e.g., during startup or if database is not initialized),
            // we fail gracefully by not filtering. This ensures Swagger generation doesn't break.
            // The schema will show all vendor properties, which is acceptable fallback behavior.
            return;
        }

        var currentVendor = currentConfig.VendorType;

        // Remove vendor-specific properties that don't match the current vendor
        if (schema.Properties != null)
        {
            // List of vendor-specific property names (only those that actually exist in DriverConfiguration)
            var vendorProperties = new Dictionary<DriverVendorType, string[]>
            {
                { DriverVendorType.Leadshine, new[] { "leadshine" } }
                // Note: Siemens, Mitsubishi, Omron properties will be added when their configurations are implemented
            };

            // Remove all vendor-specific properties except the current vendor's
            foreach (var vendorEntry in vendorProperties)
            {
                if (vendorEntry.Key == currentVendor)
                {
                    continue; // Keep current vendor's properties
                }

                foreach (var propertyName in vendorEntry.Value)
                {
                    // OpenAPI uses camelCase for property names
                    schema.Properties.Remove(propertyName);
                }
            }

            // Add description indicating which vendor is currently active
            if (schema.Description != null)
            {
                schema.Description += $"\n\n**当前配置的IO驱动厂商**: {currentVendor} ({GetVendorDisplayName(currentVendor)})";
            }
            else
            {
                schema.Description = $"**当前配置的IO驱动厂商**: {currentVendor} ({GetVendorDisplayName(currentVendor)})";
            }
        }
    }

    private static string GetVendorDisplayName(DriverVendorType vendorType)
    {
        return vendorType switch
        {
            DriverVendorType.Mock => "模拟IO驱动器",
            DriverVendorType.Leadshine => "雷赛运动控制卡",
            DriverVendorType.Siemens => "西门子PLC",
            DriverVendorType.Mitsubishi => "三菱PLC",
            DriverVendorType.Omron => "欧姆龙PLC",
            _ => "未知"
        };
    }
}
