namespace ZakYip.WheelDiverterSorter.Core.LineModel.Utilities;

/// <summary>
/// 日志相关的工具类 - 仅限当前文件内部使用
/// Logging utilities - internal to this file only
/// </summary>
file static class LoggingHelper
{
    /// <summary>
    /// 清理字符串以防止日志注入攻击
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <returns>清理后的字符串</returns>
    public static string SanitizeForLogging(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        // 移除或替换可能导致日志注入的字符（换行符、回车符等）
        return input
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\t", " ");
    }
}
