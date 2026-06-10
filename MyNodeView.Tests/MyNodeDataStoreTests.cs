using System.Reflection;
using Xunit;

namespace MyNodeView.Tests;

/// <summary>
/// 数据库测试夹具。
/// 由于 MyNodeDataStore 内部有单例检测（静态计数器），
/// 整个测试进程只能创建一个实例，因此使用 IClassFixture 共享。
///
/// 每次测试运行前：
/// 1. 删除旧的数据库文件，确保干净状态
/// 2. 通过反射重置单例计数器
/// 3. 创建唯一的 MyNodeDataStore 实例
/// </summary>
public class DatabaseFixture : IDisposable
{
    public MyNodeDataStore Store { get; }

    public DatabaseFixture()
    {
        // 获取数据库文件路径，与 InitData() 中的路径逻辑一致。
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xb1r83sB.db");

        // 删除旧的测试数据库，确保每次测试运行从空白状态开始。
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        // 通过反射重置静态单例计数器。
        // MyNodeDataStore 构造函数中 Interlocked.Exchange 会将 s_newCount 设为 1，
        // 必须先重置为 0 才能再次 new。
        var singletonField = typeof(MyNodeDataStore).GetField(
            "s_newCount",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (singletonField is not null)
        {
            singletonField.SetValue(null, 0);
        }

        // 创建唯一的 MyNodeDataStore 实例供所有测试共享。
        Store = new MyNodeDataStore();
    }

    public void Dispose()
    {
        // 测试结束后不做清理，保留数据库文件便于调试。
        // 如需清理可在此处删除 Xb1r83sB.db。
    }
}

/// <summary>
/// MyNodeDataStore 基本功能测试。
///
/// 测试范围：
/// - 插入根节点和子节点
/// - 按 ID 查询节点及其子节点
/// - 更新节点文本
/// - 全文搜索
/// - 根节点列表查询
/// - 父-子分类一致性
///
/// 不测试：并发、大数据量、性能。
/// </summary>
public class MyNodeDataStoreTests : IClassFixture<DatabaseFixture>
{
    private readonly MyNodeDataStore _store;

    public MyNodeDataStoreTests(DatabaseFixture fixture)
    {
        _store = fixture.Store;
    }

    // ==================== 插入与查询测试 ====================

    /// <summary>
    /// 插入一个根节点（无父节点），通过 ID 查询，验证返回的文本和 ID 一致。
    /// </summary>
    [Fact]
    public async Task InsertRootNode_QueryById_ReturnsSameData()
    {
        // ---------- 准备：创建一个根节点 ----------
        var newNode = new NodeData
        {
            Text = "测试根节点_插入查询"
        };

        // ---------- 执行：插入并获取返回的 ID ----------
        var insertedId = await _store.Inset(newNode);

        // ---------- 验证：通过 ID 能查到该节点 ----------
        var queryResult = await _store.QueryFunc(insertedId);

        Assert.NotNull(queryResult);
        Assert.NotNull(queryResult.Root);

        // 验证返回的文本与插入时一致。
        Assert.Equal("测试根节点_插入查询", queryResult.Root.Text);

        // 验证返回的 ID 与插入返回的 ID 一致。
        Assert.Equal(insertedId, queryResult.Root.Id);

        // 验证根节点的 Parent_Id 为 null。
        Assert.Null(queryResult.Root.Parent_Id);
    }

    /// <summary>
    /// 插入根节点后，再插入一个子节点，
    /// 查询根节点时，子节点应出现在 Child 列表中。
    /// </summary>
    [Fact]
    public async Task InsertChildNode_AppearsInParentChildList()
    {
        // ---------- 准备：先创建根节点 ----------
        var rootNode = new NodeData
        {
            Text = "父节点_子节点测试"
        };
        var rootId = await _store.Inset(rootNode);

        // ---------- 准备：创建子节点，关联到根节点 ----------
        var childNode = new NodeData
        {
            Text = "子节点_子节点测试",
            Parent_Id = rootId
        };
        var childId = await _store.Inset(childNode);

        // ---------- 执行：查询根节点 ----------
        var queryResult = await _store.QueryFunc(rootId);

        // ---------- 验证：Child 列表包含刚插入的子节点 ----------
        Assert.NotNull(queryResult.Child);

        var foundChild = queryResult.Child.FirstOrDefault(c => c.Id == childId);
        Assert.NotNull(foundChild);

        // 验证子节点文本一致。
        Assert.Equal("子节点_子节点测试", foundChild.Text);

        // 验证子节点的 parent_id 指向正确的根节点。
        Assert.Equal(rootId, foundChild.Parent_Id);
    }

    // ==================== 更新测试 ====================

    /// <summary>
    /// 插入节点后更新其文本，再次查询时文本应已变更。
    /// </summary>
    [Fact]
    public async Task UpdateNodeText_QueryReflectsChange()
    {
        // ---------- 准备：插入一个节点 ----------
        var originalNode = new NodeData
        {
            Text = "原始文本"
        };
        var nodeId = await _store.Inset(originalNode);

        // ---------- 准备：构造更新后的节点数据 ----------
        var updatedNode = new NodeData
        {
            Id = nodeId,
            Text = "修改后的文本"
        };

        // ---------- 执行：更新节点 ----------
        var result = await _store.UpData(updatedNode);

        // 验证更新方法返回了正确的数据。
        Assert.Equal(nodeId, result.Id);
        Assert.Equal("修改后的文本", result.Text);

        // ---------- 验证：查询确认文本已变更 ----------
        var queryResult = await _store.QueryFunc(nodeId);

        Assert.Equal("修改后的文本", queryResult.Root.Text);

        // 确认旧文本不再存在。
        Assert.NotEqual("原始文本", queryResult.Root.Text);
    }

    // ==================== 全文搜索测试 ====================

    /// <summary>
    /// 插入节点后通过全文搜索能找到该节点。
    /// </summary>
    [Fact]
    public async Task InsertNode_SearchByText_FindsNode()
    {
        // ---------- 准备：插入一个带有唯一文本的节点 ----------
        var uniqueText = "全文搜索测试节点_" + Guid.NewGuid().ToString("N")[..8];
        var newNode = new NodeData
        {
            Text = uniqueText
        };
        var nodeId = await _store.Inset(newNode);

        // ---------- 执行：用节点文本中的关键词搜索 ----------
        var searchKeyword = uniqueText[^8..]; // 取最后 8 个字符作为搜索词
        var searchResults = await _store.SearchFunc(searchKeyword);

        // ---------- 验证：搜索结果的 ID 列表包含刚插入的节点 ----------
        Assert.NotEmpty(searchResults);

        var foundNode = searchResults.FirstOrDefault(n => n.Id == nodeId);
        Assert.NotNull(foundNode);
        Assert.Equal(uniqueText, foundNode.Text);
    }

    /// <summary>
    /// 全文搜索带路径的方法：插入多层节点后搜索，
    /// 验证返回的 Item 是匹配节点，Parents 包含从根到该节点的路径。
    /// </summary>
    [Fact]
    public async Task SearchNodesWithFullPath_ReturnsCorrectPath()
    {
        // ---------- 准备：创建三层树结构 根→父→子 ----------
        var rootNode = new NodeData { Text = "路径测试根节点" };
        var rootId = await _store.Inset(rootNode);

        var parentNode = new NodeData
        {
            Text = "路径测试中间节点",
            Parent_Id = rootId
        };
        var parentId = await _store.Inset(parentNode);

        var childNode = new NodeData
        {
            Text = "路径测试叶子节点",
            Parent_Id = parentId
        };
        var childId = await _store.Inset(childNode);

        // ---------- 执行：全文搜索叶子节点的文本 ----------
        var searchResults = await _store.SearchNodesWithFullPath("路径测试叶子节点");

        // ---------- 验证：搜索结果包含叶子节点 ----------
        Assert.NotEmpty(searchResults);

        var result = searchResults.FirstOrDefault(r => r.Item?.Id == childId);
        Assert.NotNull(result);
        Assert.NotNull(result.Item);
        Assert.Equal("路径测试叶子节点", result.Item.Text);

        // ---------- 验证：Parents 列表包含根节点和中间节点 ----------
        Assert.NotNull(result.Parents);

        // Parents 列表应包含根节点（Id == rootId）。
        var foundRootInParents = result.Parents.Any(p => p.Id == rootId);
        Assert.True(foundRootInParents, "Parents 列表应包含根节点");

        // Parents 列表应包含中间节点（Id == parentId）。
        var foundParentInParents = result.Parents.Any(p => p.Id == parentId);
        Assert.True(foundParentInParents, "Parents 列表应包含中间父节点");
    }

    // ==================== 根节点列表测试 ====================

    /// <summary>
    /// SearchFunc() 无参数方法应只返回根节点（parent_id IS NULL），
    /// 不包含任何有父节点的子节点。
    /// </summary>
    [Fact]
    public async Task SearchFunc_NoParams_ReturnsOnlyRootNodes()
    {
        // ---------- 准备：插入根节点 ----------
        var rootNode = new NodeData
        {
            Text = "列表测试_根节点"
        };
        var rootId = await _store.Inset(rootNode);

        // ---------- 准备：插入子节点（不应出现在 SearchFunc 结果中） ----------
        var childNode = new NodeData
        {
            Text = "列表测试_子节点",
            Parent_Id = rootId
        };
        var childId = await _store.Inset(childNode);

        // ---------- 执行：获取根节点列表 ----------
        var rootNodes = await _store.SearchFunc();

        // ---------- 验证：结果包含刚插入的根节点 ----------
        Assert.NotEmpty(rootNodes);

        var foundRoot = rootNodes.FirstOrDefault(n => n.Id == rootId);
        Assert.NotNull(foundRoot);
        Assert.Equal("列表测试_根节点", foundRoot.Text);

        // ---------- 验证：结果中不包含子节点 ----------
        var foundChild = rootNodes.FirstOrDefault(n => n.Id == childId);
        Assert.Null(foundChild);

        // ---------- 验证：所有返回节点的 parent_id 都为 null ----------
        foreach (var node in rootNodes)
        {
            Assert.Null(node.Parent_Id);
        }
    }

    // ==================== 分类一致性测试 ====================

    /// <summary>
    /// 插入的节点如果指定了 parent_id，查询到的 parent_id 应与预期一致。
    /// 验证数据插入和查询的分类信息没有偏差。
    /// </summary>
    [Fact]
    public async Task InsertNode_WithParentId_CategoryIsConsistent()
    {
        // ---------- 准备：创建根节点 ----------
        var rootNode = new NodeData { Text = "分类根节点" };
        var rootId = await _store.Inset(rootNode);

        // ---------- 准备：创建子节点，指定 parent_id ----------
        var expectedParentId = rootId;
        var childNode = new NodeData
        {
            Text = "分类子节点",
            Parent_Id = expectedParentId
        };
        var childId = await _store.Inset(childNode);

        // ---------- 验证：通过 QueryFunc 查到的子节点 parent_id 一致 ----------
        var rootQuery = await _store.QueryFunc(rootId);
        var childFromQuery = rootQuery.Child.First(c => c.Id == childId);

        Assert.Equal(expectedParentId, childFromQuery.Parent_Id);

        // ---------- 验证：查询根节点有多于预期的子节点时，分类仍然正确 ----------
        // 再插入一个子节点到同一根节点下。
        var secondChild = new NodeData
        {
            Text = "分类子节点2",
            Parent_Id = rootId
        };
        var secondChildId = await _store.Inset(secondChild);

        // 重新查询根节点。
        var rootQuery2 = await _store.QueryFunc(rootId);

        // 两个子节点都应出现在 Child 列表中。
        Assert.Equal(2, rootQuery2.Child.Count);

        // 两个子节点的 parent_id 都正确指向根节点。
        foreach (var child in rootQuery2.Child)
        {
            Assert.Equal(rootId, child.Parent_Id);
        }
    }

    /// <summary>
    /// 插入不设置 Parent_Id 的节点（根节点），查询时 Parent_Id 应为 null。
    /// 验证根节点和子节点在分类上的区分是准确的。
    /// </summary>
    [Fact]
    public async Task RootNode_ParentIdIsNull_ChildNode_ParentIdIsSet()
    {
        // ---------- 准备：创建根节点（不设置 Parent_Id） ----------
        var root = new NodeData { Text = "分类区分_根" };
        var rootId = await _store.Inset(root);

        // ---------- 验证：根节点的 Parent_Id 为 null ----------
        var rootQuery = await _store.QueryFunc(rootId);
        Assert.Null(rootQuery.Root.Parent_Id);

        // ---------- 准备：创建子节点（设置 Parent_Id） ----------
        var child = new NodeData
        {
            Text = "分类区分_子",
            Parent_Id = rootId
        };
        var childId = await _store.Inset(child);

        // ---------- 验证：直接查询子节点，Parent_Id 应不为 null ----------
        var childQuery = await _store.QueryFunc(childId);
        Assert.NotNull(childQuery.Root.Parent_Id);
        Assert.Equal(rootId, childQuery.Root.Parent_Id.Value);

        // 根节点和子节点的分类区别明确：一个 parent_id 是 null，另一个不是。
    }
}
