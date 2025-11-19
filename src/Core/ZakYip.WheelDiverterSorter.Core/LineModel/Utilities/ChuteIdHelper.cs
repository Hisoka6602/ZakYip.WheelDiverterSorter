using System.Runtime.CompilerServices;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Utilities;

/// <summary>
/// 格口ID转换辅助类
/// </summary>
public static class ChuteIdHelper
{
    /// <summary>
    /// 尝试将字符串格口ID转换为整数
    /// 支持纯数字（如"1"）或带前缀格式（如"CHUTE-01"、"CHUTE_01"）
    /// </summary>
    /// <param name="chuteId">字符串格口ID</param>
    /// <param name="result">转换后的整数ID</param>
    /// <returns>是否转换成功</returns>
    public static bool TryParseChuteId(string? chuteId, out int result)
    {
        result = 0;
        
        if (string.IsNullOrWhiteSpace(chuteId))
        {
            return false;
        }

        // 尝试直接解析为整数
        if (int.TryParse(chuteId, out result))
        {
            return result > 0;
        }

        // 尝试从格式化字符串中提取数字
        // 支持格式: "CHUTE-01", "CHUTE_01", "chute-1", 等
        var cleaned = chuteId.ToUpperInvariant()
            .Replace("CHUTE-", "")
            .Replace("CHUTE_", "")
            .Replace("CHUTE", "")
            .Trim();

        if (int.TryParse(cleaned, out result))
        {
            return result > 0;
        }

        return false;
    }

    /// <summary>
    /// 将字符串格口ID转换为整数，如果失败则返回null
    /// </summary>
    /// <param name="chuteId">字符串格口ID</param>
    /// <returns>转换后的整数ID，如果失败则返回null</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int? ParseChuteId(string? chuteId)
    {
        return TryParseChuteId(chuteId, out var result) ? result : null;
    }

    /// <summary>
    /// 将整数格口ID转换为字符串表示
    /// </summary>
    /// <param name="chuteId">整数格口ID</param>
    /// <returns>字符串格式的格口ID</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatChuteId(int chuteId)
    {
        return chuteId.ToString();
    }
}
