using System.Reflection;
using Xunit;

namespace MyNodeView.Tests;

// ==================== 测试夹具 ====================

/// <summary>
/// NodeImageStore 测试夹具。
/// NodeImageStore 内部有单例检测（静态计数器），
/// 整个测试进程只能创建一个实例，因此使用 IClassFixture 共享。
///
/// 每次测试运行前：
/// 1. 删除旧的数据库文件，确保干净状态
/// 2. 通过反射重置单例计数器
/// 3. 创建唯一的 NodeImageStore 实例
/// </summary>
public class ImageStoreFixture : IDisposable
{
    public NodeImageStore Store { get; }

    public ImageStoreFixture()
    {
        // 获取数据库文件路径，与 NodeImageStore 构造函数中的路径一致。
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeImages.db");

        // 删除旧的测试数据库，确保每次测试运行从空白状态开始。
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        // 通过反射重置静态单例计数器。
        var singletonField = typeof(NodeImageStore).GetField(
            "s_newCount",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (singletonField is not null)
        {
            singletonField.SetValue(null, 0);
        }

        // 创建唯一的 NodeImageStore 实例供所有测试共享。
        Store = new NodeImageStore();
    }

    public void Dispose()
    {
        // 测试结束后不做清理，保留数据库文件便于调试。
    }
}

// ==================== 测试辅助方法 ====================

/// <summary>
/// NodeImageStore 基本功能测试。
///
/// 测试范围：
/// - 插入图片并读取（验证二进制数据一致）
/// - 按 nodeId 列出图片信息
/// - 获取节点图片摘要（数量、最新 ID）
/// - 删除图片
/// - 不同 nodeId 的数据隔离
///
/// 不测试：并发、大数据量、性能。
/// </summary>
public class NodeImageStoreTests : IClassFixture<ImageStoreFixture>
{
    private readonly NodeImageStore _store;

    public NodeImageStoreTests(ImageStoreFixture fixture)
    {
        _store = fixture.Store;
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 生成指定大小的随机二进制数据，模拟图片数据。
    /// </summary>
    private static byte[] GenerateRandomImageData(int sizeInBytes = 256)
    {
        var data = new byte[sizeInBytes];
        Random.Shared.NextBytes(data);
        return data;
    }

    // ==================== 插入与读取测试 ====================

    /// <summary>
    /// 插入一张图片后通过 ID 读取，
    /// 验证二进制数据、MIME 类型、文件名和 nodeId 完全一致。
    /// </summary>
    [Fact]
    public async Task InsertImage_GetById_ReturnsSameData()
    {
        // ---------- 准备：生成随机图片数据 ----------
        var nodeId = 1;
        var fileName = "test.png";
        var mimeType = "image/png";
        var imageData = GenerateRandomImageData();

        // ---------- 执行：插入图片 ----------
        var insertedId = await _store.InsertAsync(nodeId, fileName, mimeType, imageData);

        // ---------- 验证：通过 ID 读取到的数据应与插入时一致 ----------
        var blob = await _store.GetImageAsync(insertedId);

        Assert.NotNull(blob);
        Assert.Equal(insertedId, blob.Id);
        Assert.Equal(nodeId, blob.NodeId);
        Assert.Equal(fileName, blob.FileName);
        Assert.Equal(mimeType, blob.MimeType);
        Assert.Equal(imageData, blob.Data);
    }

    /// <summary>
    /// 插入一张不设文件名的图片，读取时 FileName 应为空字符串。
    /// </summary>
    [Fact]
    public async Task InsertImage_NullFileName_ReturnsEmptyString()
    {
        // ---------- 准备：文件名为 null ----------
        var nodeId = 2;
        var imageData = GenerateRandomImageData();

        // ---------- 执行：插入 ----------
        var insertedId = await _store.InsertAsync(nodeId, null, "image/jpeg", imageData);

        // ---------- 验证：FileName 应为空字符串 ----------
        var blob = await _store.GetImageAsync(insertedId);

        Assert.NotNull(blob);
        Assert.Equal(string.Empty, blob.FileName);
    }

    // ==================== 列表查询测试 ====================

    /// <summary>
    /// 为同一个 nodeId 插入多张图片，ListAsync 应返回全部图片信息。
    /// </summary>
    [Fact]
    public async Task InsertMultipleImages_ListByNodeId_ReturnsAll()
    {
        // ---------- 准备：为同一个节点插入 3 张不同大小的图片 ----------
        var nodeId = 10;
        var data1 = GenerateRandomImageData(100);
        var data2 = GenerateRandomImageData(200);
        var data3 = GenerateRandomImageData(300);

        var id1 = await _store.InsertAsync(nodeId, "a.jpg", "image/jpeg", data1);
        var id2 = await _store.InsertAsync(nodeId, "b.jpg", "image/jpeg", data2);
        var id3 = await _store.InsertAsync(nodeId, "c.jpg", "image/jpeg", data3);

        // ---------- 执行：列出该 nodeId 的所有图片 ----------
        var list = await _store.ListAsync(nodeId);

        // ---------- 验证：列表应包含 3 条记录，ID 和大小与插入时一致 ----------
        Assert.Equal(3, list.Count);

        var ids = list.Select(info => info.Id).ToHashSet();
        Assert.Contains(id1, ids);
        Assert.Contains(id2, ids);
        Assert.Contains(id3, ids);

        // 验证每条记录的大小与原始数据一致。
        var info1 = list.First(i => i.Id == id1);
        Assert.Equal(100, info1.Size);

        var info2 = list.First(i => i.Id == id2);
        Assert.Equal(200, info2.Size);

        var info3 = list.First(i => i.Id == id3);
        Assert.Equal(300, info3.Size);
    }

    /// <summary>
    /// 查询不存在图片的 nodeId，ListAsync 应返回空列表。
    /// </summary>
    [Fact]
    public async Task ListAsync_NoImages_ReturnsEmptyList()
    {
        var list = await _store.ListAsync(99999);
        Assert.Empty(list);
    }

    // ==================== 摘要查询测试 ====================

    /// <summary>
    /// 插入多张图片后，GetSummaryAsync 返回的数量和最新 ID 应正确。
    /// </summary>
    [Fact]
    public async Task InsertImages_GetSummary_ReturnsCorrectCountAndLatestId()
    {
        // ---------- 准备：插入 3 张图片 ----------
        var nodeId = 20;
        var data1 = GenerateRandomImageData(50);
        var data2 = GenerateRandomImageData(50);
        var data3 = GenerateRandomImageData(50);

        var id1 = await _store.InsertAsync(nodeId, null, "image/png", data1);
        var id2 = await _store.InsertAsync(nodeId, null, "image/png", data2);
        var id3 = await _store.InsertAsync(nodeId, null, "image/png", data3);

        // ---------- 执行：获取摘要 ----------
        var summary = await _store.GetSummaryAsync(nodeId);

        // ---------- 验证：数量为 3，最新 ID 为最后插入的 ID ----------
        Assert.Equal(3, summary.Count);

        var maxId = new[] { id1, id2, id3 }.Max();
        Assert.Equal(maxId, summary.LatestImageId);
    }

    /// <summary>
    /// 查询不存在图片的 nodeId，GetSummaryAsync 应返回 Count=0、LatestImageId=null。
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_NoImages_ReturnsZeroAndNull()
    {
        var summary = await _store.GetSummaryAsync(99999);

        Assert.Equal(0, summary.Count);
        Assert.Null(summary.LatestImageId);
    }

    // ==================== 删除测试 ====================

    /// <summary>
    /// 插入图片后删除，DeleteAsync 返回 true，
    /// 再次 GetImageAsync 应返回 null。
    /// </summary>
    [Fact]
    public async Task InsertThenDelete_GetById_ReturnsNull()
    {
        // ---------- 准备：插入一张图片 ----------
        var nodeId = 30;
        var imageData = GenerateRandomImageData();
        var insertedId = await _store.InsertAsync(nodeId, "delete_test.jpg", "image/jpeg", imageData);

        // ---------- 执行：删除 ----------
        var deleted = await _store.DeleteAsync(insertedId);

        // ---------- 验证：删除返回 true，再查返回 null ----------
        Assert.True(deleted);

        var blob = await _store.GetImageAsync(insertedId);
        Assert.Null(blob);
    }

    /// <summary>
    /// 删除不存在的 ID，DeleteAsync 应返回 false。
    /// </summary>
    [Fact]
    public async Task Delete_NonExistentId_ReturnsFalse()
    {
        var deleted = await _store.DeleteAsync(99999);
        Assert.False(deleted);
    }

    /// <summary>
    /// 删除后再查看摘要，Count 应减 1。
    /// </summary>
    [Fact]
    public async Task Delete_SummaryCountDecreases()
    {
        // ---------- 准备：插入 2 张图片 ----------
        var nodeId = 40;
        var data1 = GenerateRandomImageData(50);
        var data2 = GenerateRandomImageData(50);

        var id1 = await _store.InsertAsync(nodeId, null, "image/png", data1);
        var id2 = await _store.InsertAsync(nodeId, null, "image/png", data2);

        var summaryBefore = await _store.GetSummaryAsync(nodeId);
        Assert.Equal(2, summaryBefore.Count);

        // ---------- 执行：删除 1 张 ----------
        var deleted = await _store.DeleteAsync(id1);
        Assert.True(deleted);

        // ---------- 验证：Count 减为 1，LatestImageId 指向剩余的那张 ----------
        var summaryAfter = await _store.GetSummaryAsync(nodeId);
        Assert.Equal(1, summaryAfter.Count);
        Assert.Equal(id2, summaryAfter.LatestImageId);
    }

    // ==================== 数据隔离测试 ====================

    /// <summary>
    /// 为不同 nodeId 分别插入图片，
    /// ListAsync 和 GetSummaryAsync 的结果应各自独立。
    /// </summary>
    [Fact]
    public async Task DifferentNodeIds_DataIsIsolated()
    {
        // ---------- 准备：为两个不同 nodeId 各插入图片 ----------
        var nodeA = 100;
        var nodeB = 200;

        var dataA = GenerateRandomImageData(100);
        var dataB = GenerateRandomImageData(200);

        var idA = await _store.InsertAsync(nodeA, "a.jpg", "image/jpeg", dataA);
        var idB = await _store.InsertAsync(nodeB, "b.jpg", "image/jpeg", dataB);

        // ---------- 验证：nodeA 的列表中只有自己的图片 ----------
        var listA = await _store.ListAsync(nodeA);
        Assert.Single(listA);
        Assert.Equal(idA, listA[0].Id);

        // ---------- 验证：nodeB 的列表中只有自己的图片 ----------
        var listB = await _store.ListAsync(nodeB);
        Assert.Single(listB);
        Assert.Equal(idB, listB[0].Id);

        // ---------- 验证：摘要也各自独立 ----------
        var summaryA = await _store.GetSummaryAsync(nodeA);
        Assert.Equal(1, summaryA.Count);

        var summaryB = await _store.GetSummaryAsync(nodeB);
        Assert.Equal(1, summaryB.Count);
    }

    /// <summary>
    /// 通过 ID 读取图片时，返回的 NodeId 应与插入时的 nodeId 一致。
    /// </summary>
    [Fact]
    public async Task GetImageById_ReturnsCorrectNodeId()
    {
        var nodeId = 50;
        var imageData = GenerateRandomImageData();
        var insertedId = await _store.InsertAsync(nodeId, "test.jpg", "image/jpeg", imageData);

        var blob = await _store.GetImageAsync(insertedId);

        Assert.NotNull(blob);
        Assert.Equal(nodeId, blob.NodeId);
    }
}
