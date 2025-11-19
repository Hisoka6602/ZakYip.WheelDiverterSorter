using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZakYip.WheelDiverterSorter.Observability.Tracing;

/// <summary>
/// 默认日志清理策略实现
/// </summary>
/// <remarks>
/// 基于文件修改时间和总大小的清理策略。
/// 定期扫描日志目录，删除过期或超出大小限制的日志文件。
/// </remarks>
public class DefaultLogCleanupPolicy : ILogCleanupPolicy
{
    private readonly ILogger<DefaultLogCleanupPolicy> _logger;
    private readonly LogCleanupOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultLogCleanupPolicy(
        ILogger<DefaultLogCleanupPolicy> logger,
        IOptions<LogCleanupOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 执行日志清理
    /// </summary>
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var logDir = _options.LogDirectory;
            if (!Directory.Exists(logDir))
            {
                _logger.LogDebug("日志目录不存在，跳过清理: {LogDirectory}", logDir);
                return;
            }

            _logger.LogInformation("开始清理日志文件。目录: {LogDirectory}, 保留天数: {RetentionDays}, 大小上限: {MaxSizeMB} MB",
                logDir, _options.RetentionDays, _options.MaxTotalSizeMb);

            var logFiles = Directory.GetFiles(logDir, "*.log", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();

            var deletedCount = 0;
            long deletedSize = 0;
            var cutoffDate = DateTime.UtcNow.AddDays(-_options.RetentionDays);

            // 第一步：删除超过保留天数的文件
            foreach (var file in logFiles.ToList())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (file.LastWriteTimeUtc < cutoffDate)
                {
                    try
                    {
                        var size = file.Length;
                        file.Delete();
                        deletedCount++;
                        deletedSize += size;
                        logFiles.Remove(file);

                        _logger.LogInformation("删除过期日志文件: {FileName}, 修改时间: {LastWriteTime}, 大小: {Size} bytes",
                            file.Name, file.LastWriteTimeUtc, size);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除日志文件失败: {FilePath}", file.FullName);
                    }
                }
            }

            // 第二步：如果总大小超过上限，继续删除最旧的文件
            var maxSizeBytes = (long)_options.MaxTotalSizeMb * 1024 * 1024;
            var currentTotalSize = logFiles.Sum(f => f.Length);

            if (currentTotalSize > maxSizeBytes)
            {
                _logger.LogWarning("日志总大小 {CurrentSize} MB 超过上限 {MaxSize} MB，开始删除最旧文件",
                    currentTotalSize / 1024.0 / 1024.0, _options.MaxTotalSizeMb);

                foreach (var file in logFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (currentTotalSize <= maxSizeBytes)
                        break;

                    try
                    {
                        var size = file.Length;
                        file.Delete();
                        deletedCount++;
                        deletedSize += size;
                        currentTotalSize -= size;

                        _logger.LogInformation("删除旧日志文件（大小限制）: {FileName}, 大小: {Size} bytes",
                            file.Name, size);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除日志文件失败: {FilePath}", file.FullName);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("日志清理完成。删除文件数: {DeletedCount}, 释放空间: {DeletedSize} MB",
                    deletedCount, deletedSize / 1024.0 / 1024.0);
            }
            else
            {
                _logger.LogDebug("无需清理日志文件");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "日志清理过程发生异常");
        }

        await Task.CompletedTask;
    }
}
