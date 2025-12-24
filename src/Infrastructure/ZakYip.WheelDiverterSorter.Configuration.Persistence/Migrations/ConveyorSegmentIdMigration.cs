using LiteDB;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Configuration.Persistence.Migrations;

/// <summary>
/// 输送线段配置数据迁移工具
/// </summary>
/// <remarks>
/// 用于修复旧数据库中 ConveyorSegmentConfiguration 集合的 _id 字段类型问题。
/// 旧数据中 _id 为 ObjectId 类型，需要迁移为 Int64 类型（使用 SegmentId 作为主键）。
/// </remarks>
public class ConveyorSegmentIdMigration
{
    private const string CollectionName = "ConveyorSegmentConfiguration";
    private const string BackupCollectionName = "ConveyorSegmentConfiguration_Backup";

    /// <summary>
    /// 执行迁移
    /// </summary>
    /// <param name="databasePath">LiteDB 数据库文件路径</param>
    /// <param name="logger">日志记录器</param>
    /// <returns>是否需要迁移以及迁移结果</returns>
    public static (bool MigrationNeeded, bool Success, string Message) Migrate(
        string databasePath,
        ILogger? logger = null)
    {
        try
        {
            var connectionString = $"Filename={databasePath};Connection=shared";
            using var db = new LiteDatabase(connectionString);

            var collection = db.GetCollection(CollectionName);
            
            // 检查集合是否存在
            if (!db.CollectionExists(CollectionName))
            {
                logger?.LogInformation("集合 {CollectionName} 不存在，无需迁移", CollectionName);
                return (false, true, "集合不存在，无需迁移");
            }

            var count = collection.Count();
            if (count == 0)
            {
                logger?.LogInformation("集合 {CollectionName} 为空，无需迁移", CollectionName);
                return (false, true, "集合为空，无需迁移");
            }

            // 读取第一条记录检查 _id 字段类型
            var firstDoc = collection.FindAll().FirstOrDefault();
            if (firstDoc == null)
            {
                logger?.LogInformation("无法读取第一条记录，无需迁移");
                return (false, true, "无法读取记录，无需迁移");
            }

            var idField = firstDoc["_id"];
            
            // 如果 _id 已经是 Int64，无需迁移
            if (idField.IsInt64)
            {
                logger?.LogInformation("集合 {CollectionName} 的 _id 字段已是 Int64 类型，无需迁移", CollectionName);
                return (false, true, "_id 已是正确类型，无需迁移");
            }

            // 如果 _id 不是 ObjectId，报告异常情况
            if (!idField.IsObjectId)
            {
                var errorMsg = $"_id 字段类型异常：{idField.Type}，无法迁移";
                logger?.LogWarning(errorMsg);
                return (true, false, errorMsg);
            }

            logger?.LogInformation("开始迁移集合 {CollectionName}，共 {Count} 条记录", CollectionName, count);

            // 1. 备份旧集合
            if (db.CollectionExists(BackupCollectionName))
            {
                logger?.LogInformation("删除旧备份集合 {BackupCollectionName}", BackupCollectionName);
                db.DropCollection(BackupCollectionName);
            }

            logger?.LogInformation("重命名 {CollectionName} 为 {BackupCollectionName}", CollectionName, BackupCollectionName);
            db.RenameCollection(CollectionName, BackupCollectionName);

            // 2. 读取所有旧数据（使用泛型 BsonDocument，不依赖类型映射）
            var backupCollection = db.GetCollection(BackupCollectionName);
            var allDocs = backupCollection.FindAll().ToList();

            logger?.LogInformation("从备份中读取了 {Count} 条记录", allDocs.Count);

            // 3. 关闭原数据库，以便使用新的映射配置重新打开
            db.Dispose();

            // 4. 使用正确的映射配置重新打开数据库
            var mapper = Repositories.LiteDb.LiteDbMapperConfig.CreateConfiguredMapper();
            using var newDb = new LiteDatabase(connectionString, mapper);
            var newCollection = newDb.GetCollection(CollectionName);

            // 5. 逐条迁移数据，使用 SegmentId 作为新的 _id
            var migratedCount = 0;
            var failedCount = 0;

            foreach (var doc in allDocs)
            {
                try
                {
                    // 使用 SegmentId 作为新的 _id
                    if (doc.TryGetValue("SegmentId", out var segmentIdValue) && segmentIdValue.IsInt64)
                    {
                        var segmentId = segmentIdValue.AsInt64;
                        
                        // 创建新文档，将 _id 设置为 SegmentId
                        var newDoc = new BsonDocument(doc);
                        newDoc["_id"] = new BsonValue(segmentId);
                        
                        // 插入到新集合
                        newCollection.Insert(newDoc);
                        migratedCount++;
                    }
                    else
                    {
                        logger?.LogWarning("记录缺少有效的 SegmentId 字段，跳过: {Doc}", doc.ToString());
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "迁移记录失败: {Doc}", doc.ToString());
                    failedCount++;
                }
            }

            var message = $"迁移完成：成功 {migratedCount} 条，失败 {failedCount} 条";
            logger?.LogInformation(message);

            if (failedCount > 0)
            {
                logger?.LogWarning("部分记录迁移失败，备份保留在 {BackupCollectionName}", BackupCollectionName);
                return (true, migratedCount > 0, message);
            }

            // 6. 迁移完全成功，删除备份
            logger?.LogInformation("迁移完全成功，删除备份集合 {BackupCollectionName}", BackupCollectionName);
            newDb.DropCollection(BackupCollectionName);

            return (true, true, message);
        }
        catch (Exception ex)
        {
            var message = $"迁移失败: {ex.Message}";
            logger?.LogError(ex, message);
            return (true, false, message);
        }
    }
}
