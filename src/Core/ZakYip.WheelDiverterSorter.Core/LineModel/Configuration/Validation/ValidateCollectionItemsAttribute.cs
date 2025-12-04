using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Validation;

/// <summary>
/// 验证集合中每个项目是否满足指定的范围约束。
/// Validates that each item in a collection satisfies the specified range constraint.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ValidateCollectionItemsAttribute : ValidationAttribute
{
    private readonly int _minimum;
    private readonly int _maximum;

    /// <summary>
    /// 初始化 ValidateCollectionItemsAttribute 实例。
    /// </summary>
    /// <param name="minimum">允许的最小值（包含）</param>
    /// <param name="maximum">允许的最大值（包含）</param>
    public ValidateCollectionItemsAttribute(int minimum, int maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    /// <inheritdoc/>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not IEnumerable<int> collection)
        {
            return new ValidationResult($"{validationContext.DisplayName} 必须是整数集合");
        }

        var invalidItems = collection.Where(item => item < _minimum || item > _maximum).ToList();
        
        if (invalidItems.Count > 0)
        {
            var invalidValues = string.Join(", ", invalidItems);
            return new ValidationResult(
                ErrorMessage ?? 
                $"{validationContext.DisplayName} 包含无效值: {invalidValues}。所有项必须在 {_minimum} 到 {_maximum} 之间");
        }

        return ValidationResult.Success;
    }
}
