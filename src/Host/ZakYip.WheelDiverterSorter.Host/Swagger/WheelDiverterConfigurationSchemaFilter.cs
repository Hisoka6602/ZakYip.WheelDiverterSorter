using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Swagger;

/// <summary>
/// Swagger Schema Filter that dynamically filters vendor-specific properties
/// in WheelDiverterConfiguration based on the currently configured VendorType.
/// This ensures that only the relevant vendor configuration is shown in Swagger UI.
/// </summary>
public class WheelDiverterConfigurationSchemaFilter : ISchemaFilter
{
    private readonly IWheelDiverterConfigurationRepository _repository;

    public WheelDiverterConfigurationSchemaFilter(IWheelDiverterConfigurationRepository repository)
    {
        _repository = repository;
    }

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Only apply to WheelDiverterConfiguration type
        if (context.Type != typeof(WheelDiverterConfiguration))
        {
            return;
        }

        // Get current wheel diverter configuration to determine active vendor
        WheelDiverterConfiguration currentConfig;
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
            // List of vendor-specific property names
            var vendorProperties = new Dictionary<WheelDiverterVendorType, string[]>
            {
                { WheelDiverterVendorType.ShuDiNiao, new[] { "shuDiNiao" } },
                { WheelDiverterVendorType.Modi, new[] { "modi" } }
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
                schema.Description += $"\n\n**当前配置的摆轮厂商**: {currentVendor} ({GetVendorDisplayName(currentVendor)})";
            }
            else
            {
                schema.Description = $"**当前配置的摆轮厂商**: {currentVendor} ({GetVendorDisplayName(currentVendor)})";
            }
        }
    }

    private static string GetVendorDisplayName(WheelDiverterVendorType vendorType)
    {
        return vendorType switch
        {
            WheelDiverterVendorType.Mock => "模拟摆轮",
            WheelDiverterVendorType.ShuDiNiao => "数递鸟摆轮设备",
            WheelDiverterVendorType.Modi => "莫迪摆轮设备",
            _ => "未知"
        };
    }
}
