using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Swagger;

/// <summary>
/// Swagger Schema Filter that dynamically filters vendor-specific properties
/// in DriverConfiguration based on the currently configured VendorType.
/// This ensures that only the relevant vendor configuration is shown in Swagger UI.
/// </summary>
public class DriverConfigurationSchemaFilter : ISchemaFilter
{
    private readonly IDriverConfigurationRepository _repository;

    public DriverConfigurationSchemaFilter(IDriverConfigurationRepository repository)
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
            // If we can't get the configuration, don't filter
            return;
        }

        var currentVendor = currentConfig.VendorType;

        // Remove vendor-specific properties that don't match the current vendor
        if (schema.Properties != null)
        {
            // List of vendor-specific property names (only those that actually exist in DriverConfiguration)
            var vendorProperties = new Dictionary<DriverVendorType, string[]>
            {
                { DriverVendorType.Leadshine, new[] { "leadshine" } },
                { DriverVendorType.ShuDiNiao, new[] { "shuDiNiao" } }
                // Note: Siemens, Mitsubishi, Omron, Mock properties will be added when their configurations are implemented
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
                schema.Description += $"\n\n**当前配置的驱动厂商**: {currentVendor} ({GetVendorDisplayName(currentVendor)})";
            }
            else
            {
                schema.Description = $"**当前配置的驱动厂商**: {currentVendor} ({GetVendorDisplayName(currentVendor)})";
            }
        }
    }

    private static string GetVendorDisplayName(DriverVendorType vendorType)
    {
        return vendorType switch
        {
            DriverVendorType.Mock => "模拟驱动器",
            DriverVendorType.Leadshine => "雷赛控制器",
            DriverVendorType.Siemens => "西门子PLC",
            DriverVendorType.Mitsubishi => "三菱PLC",
            DriverVendorType.Omron => "欧姆龙PLC",
            DriverVendorType.ShuDiNiao => "数递鸟摆轮设备",
            _ => "未知"
        };
    }
}
