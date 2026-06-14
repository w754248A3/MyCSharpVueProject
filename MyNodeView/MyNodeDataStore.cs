using System.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Text.Json.Serialization;

namespace MyNodeView;

// ==================== 数据库访问序列化器 ====================

/// <summary>
/// 通过排他锁将数据库操作序列化到单一线程，
/// 确保同一时刻只有一个数据库操作在执行。
/// </summary>
public sealed class MyAsyncRunSql
{
    readonly object _lock = new();

    public Task<T> Run<T>(Func<T> func)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                return func();
            }
        });
    }
}

// ==================== 数据存储 ====================

public sealed class MyNodeDataStore : IDisposable
{
    static int s_newCount = 0;

    readonly SqliteConnection _con;
    readonly MyAsyncRunSql _run = new();

    // SQL 命令缓存。键为 SQL 文本，值为已编译（Prepare）的命令。
    // Prepare() 仅在首次创建时调用一次，后续复用已编译的命令。
    // 每次使用前清除参数，由调用方重新绑定。
    private readonly Dictionary<string, SqliteCommand> _commandCache = new();

    // ==================== 构造函数 ====================

    public MyNodeDataStore()
    {
        if (Interlocked.Exchange(ref s_newCount, 1) != 0)
        {
            throw new InvalidOperationException("类只能有一个实例");
        }

        _con = InitData();
    }

    // ==================== 一次性 SQL 便捷方法 ====================

    /// <summary>
    /// 执行不需要参数的一次性非查询 SQL（如 PRAGMA、CREATE TABLE 等建表语句）。
    /// 每次调用创建新命令，执行后立即释放。
    /// </summary>
    private void ExecuteNonQueryOnce(string sql)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 执行不需要参数的一次性 SQL 并返回标量值。
    /// 每次调用创建新命令，执行后立即释放。
    /// </summary>
    private T ExecuteScalarOnce<T>(string sql)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }

    // ==================== 命令缓存与编译 ====================

    /// <summary>
    /// 获取指定 SQL 对应的已编译命令。
    /// 首次调用时创建命令、设置 SQL、调用 Prepare() 编译，并加入缓存。
    /// 后续调用直接复用缓存的命令，只清除参数不重新编译。
    /// 调用方拿到命令后绑定参数，然后开启事务并执行。
    /// </summary>
    private SqliteCommand GetPreparedCommand(string sql)
    {
        var isCached = _commandCache.TryGetValue(sql, out var cachedCommand);

        if (!isCached)
        {
            cachedCommand = _con.CreateCommand();
            cachedCommand.CommandText = sql;
            cachedCommand.Prepare();
            _commandCache[sql] = cachedCommand;
        }

        cachedCommand!.Parameters.Clear();
        return cachedCommand;
    }

    // ==================== 数据库初始化 ====================

    static SqliteConnection InitData()
    {
        var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xb1r83sB.db");

        var connectionString = new SqliteConnectionStringBuilder()
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DataSource = dbPath
        }.ToString();

        var con = new SqliteConnection(connectionString);
        con.Open();

        SqliteEx.MySqliteTokenizer.RegisterCustomTokenizer(con, "mytokenizer");

        // 建表和初始化 SQL 只在启动时执行一次，直接使用普通命令。
        using var cmd = con.CreateCommand();

        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA foreign_keys;";
        var foreignKeysEnabled = (long)cmd.ExecuteScalar()!;
        if (foreignKeysEnabled != 1)
        {
            throw new InvalidOperationException("数据库不支持外键约束");
        }

        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS nodesTable (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        parent_id INTEGER,
        text TEXT NOT NULL,
        FOREIGN KEY (parent_id) REFERENCES nodesTable(id) ON DELETE RESTRICT ON UPDATE NO ACTION
        );
        """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = """
        CREATE INDEX IF NOT EXISTS idx_nodes_parent ON nodesTable(parent_id);
        """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = """
        CREATE VIRTUAL TABLE IF NOT EXISTS  textSearchTable USING fts5(
        Value,
        tokenize="mytokenizer",
        content='',
        contentless_delete=1
        )
        """;
        cmd.ExecuteNonQuery();

        return con;
    }

    // ==================== 数据读取辅助方法 ====================

    /// <summary>
    /// 从 SqliteDataReader 当前行读取一个 NodeData 对象。
    /// 列顺序必须为：id, parent_id, text。
    /// </summary>
    private static NodeData ReadNodeData(SqliteDataReader reader)
    {
        var node = new NodeData
        {
            Id = reader.GetInt32(0),
            Parent_Id = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            Text = reader.GetString(2)
        };
        return node;
    }

    /// <summary>
    /// 从 SqliteDataReader 读取所有行，返回 NodeData 列表。
    /// 列顺序必须为：id, parent_id, text。
    /// </summary>
    private static List<NodeData> ReadNodeDataList(SqliteDataReader reader)
    {
        var list = new List<NodeData>();
        while (reader.Read())
        {
            list.Add(ReadNodeData(reader));
        }
        return list;
    }

    /// <summary>
    /// 从 SqliteDataReader 读取所有行，返回 AncestorRecord 列表。
    /// 列顺序必须为：id, parent_id, text, target_id, depth, rank。
    /// </summary>
    private static List<AncestorRecord> ReadAncestorRecords(SqliteDataReader reader)
    {
        var list = new List<AncestorRecord>();
        while (reader.Read())
        {
            var record = new AncestorRecord
            {
                Id = reader.GetInt32(0),
                Parent_Id = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Text = reader.GetString(2),
                Target_Id = reader.GetInt32(3),
                Depth = reader.GetInt32(4),
                Rank = reader.GetDouble(5)
            };
            list.Add(record);
        }
        return list;
    }

    // ==================== 数据操作方法 ====================

    int Inset2(NodeData data)
    {
        // 步骤 1：获取已编译命令并绑定参数（事务开启前）。
        var insertCmd = GetPreparedCommand(
            "INSERT INTO nodesTable (parent_id, text) VALUES (@parent_id, @text);");
        insertCmd.Parameters.AddWithValue("@parent_id", (object?)data.Parent_Id ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@text", data.Text);

        var lastIdCmd = GetPreparedCommand("SELECT last_insert_rowid();");

        var insertFtsCmd = GetPreparedCommand(
            "INSERT INTO textSearchTable (rowid, Value) VALUES (@rowid, @value);");
        insertFtsCmd.Parameters.AddWithValue("@rowid", 0);
        insertFtsCmd.Parameters.AddWithValue("@value", data.Text);

        // 步骤 2：开启事务。
        using var tr = _con.BeginTransaction();

        // 步骤 3：将缓存的命令关联到当前事务，然后在事务内执行。
        // 缓存的命令创建于事务开启前，其 Transaction 属性需要在事务开启后显式设置。
        insertCmd.Transaction = tr;
        lastIdCmd.Transaction = tr;
        insertFtsCmd.Transaction = tr;

        insertCmd.ExecuteNonQuery();

        var newId = (long)lastIdCmd.ExecuteScalar()!;
        var id = (int)newId;

        insertFtsCmd.Parameters["@rowid"].Value = id;
        insertFtsCmd.ExecuteNonQuery();

        tr.Commit();

        return id;
    }

    public Task<int> Inset(NodeData data)
    {
        return _run.Run(() => Inset2(data));
    }

    NodeData UpData2(NodeData data)
    {
        // 步骤 1：获取已编译命令并绑定参数（事务开启前）。
        var updateCmd = GetPreparedCommand(
            "UPDATE nodesTable SET text = @text WHERE id = @id;");
        updateCmd.Parameters.AddWithValue("@text", data.Text);
        updateCmd.Parameters.AddWithValue("@id", data.Id);

        var updateFtsCmd = GetPreparedCommand(
            "UPDATE textSearchTable SET Value = @text WHERE rowid = @id;");
        updateFtsCmd.Parameters.AddWithValue("@text", data.Text);
        updateFtsCmd.Parameters.AddWithValue("@id", data.Id);

        // 步骤 2：开启事务。
        using var tr = _con.BeginTransaction();

        // 步骤 3：将缓存的命令关联到当前事务，然后在事务内执行。
        updateCmd.Transaction = tr;
        updateFtsCmd.Transaction = tr;

        updateCmd.ExecuteNonQuery();
        updateFtsCmd.ExecuteNonQuery();

        tr.Commit();

        return data;
    }

    public Task<NodeData> UpData(NodeData data)
    {
        return _run.Run(() => UpData2(data));
    }

    QueryData QueryFunc2(int id)
    {
        // 步骤 1：获取已编译命令并绑定参数（事务开启前）。
        var rootCmd = GetPreparedCommand(
            "SELECT id, parent_id, text FROM nodesTable WHERE id = @id;");
        rootCmd.Parameters.AddWithValue("@id", id);

        var childCmd = GetPreparedCommand(
            "SELECT id, parent_id, text FROM nodesTable WHERE parent_id = @parent_id;");
        childCmd.Parameters.AddWithValue("@parent_id", id);

        // 步骤 2：开启事务。
        using var tr = _con.BeginTransaction();

        // 步骤 3：将缓存的命令关联到当前事务，然后执行查询。
        rootCmd.Transaction = tr;
        childCmd.Transaction = tr;

        using var rootReader = rootCmd.ExecuteReader();
        var hasRoot = rootReader.Read();
        var root = hasRoot ? ReadNodeData(rootReader) : new NodeData();

        using var childReader = childCmd.ExecuteReader();
        var children = ReadNodeDataList(childReader);

        tr.Commit();

        return new QueryData { Root = root, Child = children };
    }

    public Task<QueryData> QueryFunc(int id)
    {
        return _run.Run(() => QueryFunc2(id));
    }

    // ==================== 全文搜索 ====================

    List<NodeData> SearchFunc2(string searchText)
    {
        // 步骤 1：获取已编译命令并绑定参数（事务开启前）。
        var searchCmd = GetPreparedCommand("""
            SELECT n.id, n.parent_id, n.text
            FROM textSearchTable t
            INNER JOIN nodesTable n ON t.rowid = n.id
            WHERE textSearchTable MATCH @searchText
            ORDER BY rank
            """);
        searchCmd.Parameters.AddWithValue("@searchText", searchText);

        // 步骤 2：开启事务。
        using var tr = _con.BeginTransaction();

        // 步骤 3：将缓存的命令关联到当前事务，然后执行查询。
        searchCmd.Transaction = tr;

        using var reader = searchCmd.ExecuteReader();
        var results = ReadNodeDataList(reader);

        tr.Commit();

        return results;
    }

    public Task<List<NodeData>> SearchFunc(string searchText)
    {
        return _run.Run(() => SearchFunc2(searchText));
    }

    /// <summary>
    /// 全文搜索节点并返回从根节点到匹配节点的完整路径。
    /// </summary>
    /// <param name="searchKeyword">要搜索的关键字。</param>
    /// <param name="maxResults">限制返回的最大结果数量，默认值为 10。</param>
    /// <returns>包含匹配节点及其父节点路径的列表。</returns>
    /// <exception cref="SqliteException">当数据库操作失败时抛出。</exception>
    List<NodeSearchResult> SearchNodesWithFullPath2(string searchKeyword, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchKeyword))
        {
            return new List<NodeSearchResult>();
        }

        try
        {
            var sql = @"
                WITH RECURSIVE
                  MatchedNodes AS (
                    SELECT rowid AS id, rank
                    FROM textSearchTable
                    WHERE textSearchTable MATCH @keyword
                    ORDER BY rank
                    LIMIT @limit
                  ),
                  PathCTE(id, parent_id, text, target_id, depth, target_rank) AS (
                    SELECT n.id, n.parent_id, n.text, n.id, 0, m.rank
                    FROM nodesTable n
                    JOIN MatchedNodes m ON n.id = m.id
                    UNION ALL
                    SELECT n.id, n.parent_id, n.text, p.target_id, p.depth + 1, p.target_rank
                    FROM nodesTable n
                    JOIN PathCTE p ON n.id = p.parent_id
                  )
                SELECT id, parent_id, text, target_id, depth, target_rank AS rank
                FROM PathCTE
                ORDER BY target_rank, target_id, depth DESC;";

            var cmd = GetPreparedCommand(sql);
            cmd.Parameters.AddWithValue("@keyword", searchKeyword);
            cmd.Parameters.AddWithValue("@limit", maxResults);

            // 开启事务，将缓存的命令关联到当前事务后执行查询。
            using var tr = _con.BeginTransaction();
            cmd.Transaction = tr;

            using var reader = cmd.ExecuteReader();
            var allRecords = ReadAncestorRecords(reader);

            tr.Commit();

            if (allRecords.Count == 0)
            {
                return new List<NodeSearchResult>();
            }

            System.Diagnostics.Trace.WriteLine($"单条 SQL 查询返回总记录数: {allRecords.Count}");

            var results = allRecords
                .GroupBy(r => r.Target_Id)
                .OrderBy(g => g.First().Rank)
                .Select(g => new NodeSearchResult
                {
                    Item = g.Where(r => r.Depth == 0).Select(r => new NodeData
                    {
                        Id = r.Id,
                        Parent_Id = r.Parent_Id,
                        Text = r.Text
                    }).FirstOrDefault(),
                    Parents = g.Where(r => r.Depth > 0).Select(r => new NodeData
                    {
                        Id = r.Id,
                        Parent_Id = r.Parent_Id,
                        Text = r.Text
                    }).ToList()
                })
                .ToList();

            return results;
        }
        catch (SqliteException)
        {
            System.Diagnostics.Trace.WriteLine($"SearchNodesWithFullPath 发生错误");
            throw;
        }
    }

    public Task<List<NodeSearchResult>> SearchNodesWithFullPath(string searchKeyword, int maxResults = 10)
    {
        return _run.Run(() => SearchNodesWithFullPath2(searchKeyword, maxResults));
    }

    // ==================== 根节点列表查询 ====================

    List<NodeData> SearchFunc2()
    {
        // 步骤 1：获取已编译命令（无参数绑定，事务开启前）。
        var cmd = GetPreparedCommand("""
            SELECT id, parent_id, text
            FROM nodesTable
            WHERE parent_id IS NULL
            ORDER BY id DESC
            LIMIT 100
            """);

        // 步骤 2：开启事务。
        using var tr = _con.BeginTransaction();

        // 步骤 3：将缓存的命令关联到当前事务，然后执行查询。
        cmd.Transaction = tr;

        using var reader = cmd.ExecuteReader();
        var results = ReadNodeDataList(reader);

        tr.Commit();

        return results;
    }

    public Task<List<NodeData>> SearchFunc()
    {
        return _run.Run(() => SearchFunc2());
    }

    // ==================== 资源释放 ====================

    /// <summary>
    /// 释放所有缓存的 SQL 命令和数据库连接。
    /// </summary>
    public void Dispose()
    {
        foreach (var cmd in _commandCache.Values)
        {
            cmd.Dispose();
        }
        _commandCache.Clear();

        _con?.Dispose();
    }
}

// ==================== 数据模型类 ====================

public class NodeData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("parentId")]
    public int? Parent_Id { get; set; }

    [JsonPropertyName("title")]
    public string Text { get; set; }
}

public class QueryData
{
    [JsonPropertyName("root")]
    public NodeData Root { get; set; }

    [JsonPropertyName("child")]
    public List<NodeData> Child { get; set; }
}

public class MessageData<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("value")]
    public T Value { get; set; }
}

/// <summary>
/// 搜索结果项，包含当前节点及其父节点路径
/// </summary>
public class NodeSearchResult
{
    /// <summary>
    /// 匹配到的节点
    /// </summary>
    [JsonPropertyName("item")]
    public NodeData Item { get; set; }

    /// <summary>
    /// 从根节点到当前节点的路径上的所有父节点列表（按从根到子的顺序排列，不包含当前节点本身）
    /// </summary>
    [JsonPropertyName("parents")]
    public List<NodeData> Parents { get; set; }
}

/// <summary>
/// 内部使用的祖先节点记录类，用于递归查询结果映射
/// </summary>
public class AncestorRecord
{
    public int Id { get; set; }

    public int? Parent_Id { get; set; }

    public string Text { get; set; }

    public int Target_Id { get; set; }

    public int Depth { get; set; }

    public double Rank { get; set; }
}
