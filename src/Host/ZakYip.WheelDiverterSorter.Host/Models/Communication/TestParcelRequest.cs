using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Communication;

/// <summary>
/// 测试包裹请求模型 - Test Parcel Request Model
/// </summary>
public sealed record TestParcelRequest
{
    /// <summary>
    /// 测试包裹ID - Test parcel ID
    /// </summary>
    /// <remarks>
    /// 用于标识此测试包裹的唯一ID
    /// </remarks>
    /// <example>TEST-PKG-001</example>
    [Required(ErrorMessage = "包裹ID不能为空 - Parcel ID is required")]
    [StringLength(50, ErrorMessage = "包裹ID长度不能超过50个字符 - Parcel ID length cannot exceed 50 characters")]
    public required string ParcelId { get; init; }
}
