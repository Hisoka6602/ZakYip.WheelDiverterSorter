using System.Net;
using System.Net.Http.Json;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Host.Models;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.E2ETests;

/// <summary>
/// E2E测试：输送线段配置向后兼容性测试
/// </summary>
/// <remarks>
/// <para>测试目标：确保系统能正确处理数据库中已有的 ObjectId 类型 _id 字段的旧数据</para>
/// <para>测试场景：</para>
/// <list type="number">
/// <item>创建包含 ObjectId _id 的旧数据</item>
/// <item>通过 API 读取旧数据</item>
/// <item>通过 API 更新旧数据</item>
/// <item>通过 API 删除旧数据</item>
/// <item>创建新数据并验证</item>
/// <item>验证分拣流程中能正确读取输送线段配置</item>
/// </list>
/// </remarks>
[Collection("ConveyorSegmentTests")]  // 确保测试按顺序运行，避免数据库并发问题
public class ConveyorSegmentLegacyDataE2ETests : E2ETestBase
{
    private const string ApiBaseUrl = "/api/config/conveyor-segments";

    public ConveyorSegmentLegacyDataE2ETests(E2ETestFactory factory, ITestOutputHelper output) 
        : base(factory, output)
    {
    }

    /// <summary>
    /// 在数据库中直接插入包含 ObjectId _id 的旧数据
    /// </summary>
    private void SeedLegacyData()
    {
        // 通过配置获取数据库路径
        var configuration = Scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var dbPath = configuration.GetValue<string>("RouteConfiguration:DatabasePath") ?? "Data/routes.db";
        
        // 确保数据库目录存在
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        // 直接使用 LiteDB 插入旧格式数据（包含 ObjectId 作为 _id）
        using var db = new LiteDatabase($"Filename={dbPath};Connection=shared");
        var collection = db.GetCollection("ConveyorSegmentConfiguration");

        // 先清空现有数据
        collection.DeleteAll();

        // 模拟旧数据：_id 为 ObjectId 类型
        var legacyDoc1 = new BsonDocument
        {
            ["_id"] = ObjectId.NewObjectId(),  // ObjectId 类型的 _id
            ["SegmentId"] = 1L,
            ["SegmentName"] = "Legacy Segment 1",
            ["LengthMm"] = 5000.0,
            ["SpeedMmps"] = 1000.0m,
            ["TimeToleranceMs"] = 500L,
            ["EnableLossDetection"] = true,
            ["Remarks"] = "旧数据 - ObjectId _id",
            ["CreatedAt"] = DateTime.Now.AddDays(-30),
            ["UpdatedAt"] = DateTime.Now.AddDays(-30)
        };

        var legacyDoc2 = new BsonDocument
        {
            ["_id"] = ObjectId.NewObjectId(),  // ObjectId 类型的 _id
            ["SegmentId"] = 2L,
            ["SegmentName"] = "Legacy Segment 2",
            ["LengthMm"] = 6000.0,
            ["SpeedMmps"] = 1200.0m,
            ["TimeToleranceMs"] = 600L,
            ["EnableLossDetection"] = true,
            ["Remarks"] = "旧数据 - ObjectId _id",
            ["CreatedAt"] = DateTime.Now.AddDays(-20),
            ["UpdatedAt"] = DateTime.Now.AddDays(-20)
        };

        var legacyDoc3 = new BsonDocument
        {
            ["_id"] = ObjectId.NewObjectId(),  // ObjectId 类型的 _id
            ["SegmentId"] = 3L,
            ["SegmentName"] = "Legacy Segment 3",
            ["LengthMm"] = 4500.0,
            ["SpeedMmps"] = 900.0m,
            ["TimeToleranceMs"] = 450L,
            ["EnableLossDetection"] = false,
            ["Remarks"] = "旧数据 - ObjectId _id",
            ["CreatedAt"] = DateTime.Now.AddDays(-10),
            ["UpdatedAt"] = DateTime.Now.AddDays(-10)
        };

        collection.Insert(legacyDoc1);
        collection.Insert(legacyDoc2);
        collection.Insert(legacyDoc3);

        Output.WriteLine($"✅ 已插入 3 条旧格式数据（ObjectId _id）到数据库: {dbPath}");
    }

    [Fact]
    public async Task Test01_GetAllSegments_ShouldReadLegacyData_Successfully()
    {
        // Arrange
        SeedLegacyData();

        // Act
        var response = await Client.GetAsync(ApiBaseUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ConveyorSegmentResponse>>>();
        Assert.NotNull(result);
        Assert.True(result.Success, $"API 调用失败: {result.Message}");
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Count);

        // 验证数据内容
        var segment1 = result.Data.FirstOrDefault(s => s.SegmentId == 1);
        Assert.NotNull(segment1);
        Assert.Equal("Legacy Segment 1", segment1.SegmentName);
        Assert.Equal(5000, segment1.LengthMm);
        Assert.Equal(1000m, segment1.SpeedMmps);

        Output.WriteLine("✅ 成功读取旧数据（ObjectId _id）");
    }

    [Fact]
    public async Task Test02_GetSegmentById_ShouldReadLegacyData_Successfully()
    {
        // Arrange
        SeedLegacyData();

        // Act
        var response = await Client.GetAsync($"{ApiBaseUrl}/2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.SegmentId);
        Assert.Equal("Legacy Segment 2", result.Data.SegmentName);
        Assert.Equal(6000, result.Data.LengthMm);
        Assert.Equal(1200m, result.Data.SpeedMmps);

        Output.WriteLine("✅ 成功通过 ID 读取旧数据");
    }

    [Fact]
    public async Task Test03_UpdateSegment_ShouldUpdateLegacyData_Successfully()
    {
        // Arrange
        SeedLegacyData();

        var updateRequest = new ConveyorSegmentRequest
        {
            SegmentId = 1,
            SegmentName = "Updated Legacy Segment 1",
            LengthMm = 5500,
            SpeedMmps = 1100m,
            TimeToleranceMs = 550,
            EnableLossDetection = true,
            Remarks = "已更新的旧数据"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"{ApiBaseUrl}/1", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success, $"更新失败: {result.Message}");
        Assert.NotNull(result.Data);
        Assert.Equal("Updated Legacy Segment 1", result.Data.SegmentName);
        Assert.Equal(5500, result.Data.LengthMm);
        Assert.Equal(1100m, result.Data.SpeedMmps);

        // 验证更新后能再次读取
        var getResponse = await Client.GetAsync($"{ApiBaseUrl}/1");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentResponse>>();
        Assert.NotNull(getResult);
        Assert.True(getResult.Success);
        Assert.Equal("Updated Legacy Segment 1", getResult.Data!.SegmentName);

        Output.WriteLine("✅ 成功更新旧数据");
    }

    [Fact]
    public async Task Test04_DeleteSegment_ShouldDeleteLegacyData_Successfully()
    {
        // Arrange
        SeedLegacyData();

        // Act - 删除
        var deleteResponse = await Client.DeleteAsync($"{ApiBaseUrl}/3");

        // Assert - 验证删除成功
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(deleteResult);
        Assert.True(deleteResult.Success);

        // 验证删除后无法再读取
        var getResponse = await Client.GetAsync($"{ApiBaseUrl}/3");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        // 验证其他数据仍然存在
        var getAllResponse = await Client.GetAsync(ApiBaseUrl);
        var getAllResult = await getAllResponse.Content.ReadFromJsonAsync<ApiResponse<List<ConveyorSegmentResponse>>>();
        Assert.NotNull(getAllResult);
        Assert.Equal(2, getAllResult.Data!.Count);  // 只剩 2 条

        Output.WriteLine("✅ 成功删除旧数据");
    }

    [Fact]
    public async Task Test05_CreateNewSegment_AfterLegacyData_ShouldWork()
    {
        // Arrange
        SeedLegacyData();

        var newSegmentRequest = new ConveyorSegmentRequest
        {
            SegmentId = 100,
            SegmentName = "New Segment After Legacy",
            LengthMm = 7000,
            SpeedMmps = 1400m,
            TimeToleranceMs = 700,
            EnableLossDetection = true,
            Remarks = "新创建的数据"
        };

        // Act
        var response = await Client.PostAsJsonAsync(ApiBaseUrl, newSegmentRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ConveyorSegmentResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success, $"创建失败: {result.Message}");
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.SegmentId);
        Assert.Equal("New Segment After Legacy", result.Data.SegmentName);

        // 验证总共有 4 条数据（3 条旧 + 1 条新）
        var getAllResponse = await Client.GetAsync(ApiBaseUrl);
        var getAllResult = await getAllResponse.Content.ReadFromJsonAsync<ApiResponse<List<ConveyorSegmentResponse>>>();
        Assert.NotNull(getAllResult);
        Assert.Equal(4, getAllResult.Data!.Count);

        Output.WriteLine("✅ 在旧数据基础上成功创建新数据");
    }

    [Fact]
    public async Task Test06_BatchOperations_WithLegacyData_ShouldWork()
    {
        // Arrange
        SeedLegacyData();

        var batchCreateRequest = new List<ConveyorSegmentRequest>
        {
            new()
            {
                SegmentId = 200,
                SegmentName = "Batch Segment 1",
                LengthMm = 5000,
                SpeedMmps = 1000m,
                TimeToleranceMs = 500,
                EnableLossDetection = true
            },
            new()
            {
                SegmentId = 201,
                SegmentName = "Batch Segment 2",
                LengthMm = 6000,
                SpeedMmps = 1200m,
                TimeToleranceMs = 600,
                EnableLossDetection = true
            }
        };

        // Act - 批量创建
        var response = await Client.PostAsJsonAsync($"{ApiBaseUrl}/batch", batchCreateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ConveyorSegmentResponse>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);

        // 验证总共有 5 条数据（3 条旧 + 2 条批量新增）
        var getAllResponse = await Client.GetAsync(ApiBaseUrl);
        var getAllResult = await getAllResponse.Content.ReadFromJsonAsync<ApiResponse<List<ConveyorSegmentResponse>>>();
        Assert.NotNull(getAllResult);
        Assert.Equal(5, getAllResult.Data!.Count);

        Output.WriteLine("✅ 批量操作在旧数据基础上成功");
    }

    [Fact]
    public async Task Test07_SortingFlow_ShouldReadConveyorSegmentConfig_FromLegacyData()
    {
        // Arrange - 创建旧数据
        SeedLegacyData();

        // 验证分拣流程能读取输送线段配置
        // 这里需要通过 DI 容器获取配置服务来验证
        var serviceProvider = Factory.Services;
        var conveyorSegmentService = serviceProvider.GetRequiredService<ZakYip.WheelDiverterSorter.Application.Services.Config.IConveyorSegmentService>();

        // Act - 通过服务读取配置
        var segment1 = conveyorSegmentService.GetSegmentById(1);
        var segment2 = conveyorSegmentService.GetSegmentById(2);
        var segment3 = conveyorSegmentService.GetSegmentById(3);

        // Assert
        Assert.NotNull(segment1);
        Assert.Equal("Legacy Segment 1", segment1.SegmentName);
        Assert.Equal(5000, segment1.LengthMm);

        Assert.NotNull(segment2);
        Assert.Equal("Legacy Segment 2", segment2.SegmentName);

        Assert.NotNull(segment3);
        Assert.Equal("Legacy Segment 3", segment3.SegmentName);
        Assert.False(segment3.EnableLossDetection);

        // 验证计算方法
        var transitTime = segment1.CalculateTransitTimeMs();
        var timeoutThreshold = segment1.CalculateTimeoutThresholdMs();
        
        Assert.Equal(5000, transitTime);  // 5000mm / 1000mmps * 1000 = 5000ms
        Assert.Equal(5500, timeoutThreshold);  // 5000ms + 500ms = 5500ms

        Output.WriteLine("✅ 分拣流程成功读取旧数据配置");
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Output.WriteLine("🧹 测试完成");
    }
}
