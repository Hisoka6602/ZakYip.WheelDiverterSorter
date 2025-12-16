namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// MiniApi服务配置选项
/// </summary>
public class MiniApiOptions
{
    /// <summary>
    /// API服务监听地址和端口列表
    /// </summary>
    /// <remarks>
    /// 支持多个地址，例如：
    /// - "http://localhost:5000" - 仅本地访问
    /// - "http://0.0.0.0:5000" - 允许外部访问（绑定所有IPv4接口）
    /// - "http://*:5000" - 绑定所有网络接口
    /// - "https://0.0.0.0:5001" - HTTPS（需要配置证书）
    /// </remarks>
    public string[] Urls { get; set; } = new[] { "http://localhost:5000" };

    /// <summary>
    /// 是否启用Swagger文档
    /// </summary>
    /// <remarks>
    /// 生产环境建议设为false以提高安全性和性能
    /// </remarks>
    public bool EnableSwagger { get; set; } = true;
}
