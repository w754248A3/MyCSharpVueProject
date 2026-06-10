using System.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Text.Json.Serialization;

namespace MyNodeView;


public sealed class MyAsyncRunSql{

    readonly object _lock =new();
    public Task<T> Run<T>(Func<T> func){

        return Task.Run(()=>{
            lock(_lock){
                return func();
            }
        });
    }
}


public sealed class MyNodeDataStore{

    static int s_newCount =0;

    readonly SqliteConnection _con;
    readonly MyAsyncRunSql _run= new();

    public MyNodeDataStore(){

        if(Interlocked.Exchange(ref s_newCount, 1)!=0){
            throw new InvalidOperationException("类只能有一个实例");
        }

        _con = InitData();
    }

   static SqliteConnection InitData(){

        var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Xb1r83sB.db");

        // 使用 SqliteConnectionStringBuilder 构造连接字符串。
        var connectionString = new SqliteConnectionStringBuilder()
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DataSource=dbPath
        }.ToString();

        // 创建并打开单一数据库连接。
        var con = new SqliteConnection(connectionString);
        con.Open();

        // 注册自定义分词器，用于 FTS5 全文搜索。
        SqliteEx.MySqliteTokenizer.RegisterCustomTokenizer(con, "mytokenizer");

        // 使用显式 SQL 命令执行初始化和建表。
        using var cmd = con.CreateCommand();

        // 启用外键约束。
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();

        // 验证外键约束是否已启用。
        cmd.CommandText = "PRAGMA foreign_keys;";
        var foreignKeysEnabled = (long)cmd.ExecuteScalar()!;
        if(foreignKeysEnabled != 1){
            throw new InvalidOperationException("数据库不支持外键约束");
        }

        // 创建节点表。
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS nodesTable (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        parent_id INTEGER,
        text TEXT NOT NULL,
        FOREIGN KEY (parent_id) REFERENCES nodesTable(id) ON DELETE RESTRICT ON UPDATE NO ACTION
        );
        """;
        cmd.ExecuteNonQuery();

        // 创建 parent_id 索引，加速子节点查询。
        cmd.CommandText = """
        CREATE INDEX IF NOT EXISTS idx_nodes_parent ON nodesTable(parent_id);
        """;
        cmd.ExecuteNonQuery();

        // 创建 FTS5 全文搜索虚拟表。
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

    int Inset2(NodeData data){
        // 在事务中同时插入节点表和全文搜索表，保证数据一致性。
        using var tr = _con.BeginTransaction();

        // 插入节点表。
        using var insertCmd = _con.CreateCommand();
        insertCmd.CommandText = "INSERT INTO nodesTable (parent_id, text) VALUES (@parent_id, @text);";
        insertCmd.Parameters.AddWithValue("@parent_id", (object?)data.Parent_Id ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@text", data.Text);
        insertCmd.ExecuteNonQuery();

        // 获取自增主键的值。
        insertCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)insertCmd.ExecuteScalar()!;
        var id = (int)newId;

        // 插入全文搜索表。
        insertCmd.CommandText = "INSERT INTO textSearchTable (rowid, Value) VALUES (@rowid, @value);";
        insertCmd.Parameters.Clear();
        insertCmd.Parameters.AddWithValue("@rowid", id);
        insertCmd.Parameters.AddWithValue("@value", data.Text);
        insertCmd.ExecuteNonQuery();

        tr.Commit();

        return id;
    }

    public Task<int> Inset(NodeData data){
        return _run.Run(()=> Inset2(data));
    }

    NodeData UpData2(NodeData data){
        // 在事务中同时更新节点表和全文搜索表。
        using var tr = _con.BeginTransaction();

        using var updateCmd = _con.CreateCommand();

        // 更新节点表的文本。
        updateCmd.CommandText = "UPDATE nodesTable SET text = @text WHERE id = @id;";
        updateCmd.Parameters.AddWithValue("@text", data.Text);
        updateCmd.Parameters.AddWithValue("@id", data.Id);
        updateCmd.ExecuteNonQuery();

        // 更新全文搜索表的索引文本。
        updateCmd.CommandText = "UPDATE textSearchTable SET Value = @text WHERE rowid = @id;";
        updateCmd.Parameters.Clear();
        updateCmd.Parameters.AddWithValue("@text", data.Text);
        updateCmd.Parameters.AddWithValue("@id", data.Id);
        updateCmd.ExecuteNonQuery();

        tr.Commit();

        return data;
    }

    public Task<NodeData> UpData(NodeData data){
        return _run.Run(()=> UpData2(data));
    }

    QueryData QueryFunc2(int id){

        using var queryCmd = _con.CreateCommand();

        // 查询指定 ID 的节点（Root）。
        queryCmd.CommandText = "SELECT id, parent_id, text FROM nodesTable WHERE id = @id;";
        queryCmd.Parameters.AddWithValue("@id", id);
        using var rootReader = queryCmd.ExecuteReader();

        // 将 reader 推进到第一行数据后再读取。
        var hasRoot = rootReader.Read();
        var root = hasRoot ? ReadNodeData(rootReader) : new NodeData();
        rootReader.Close();

        // 查询该节点的所有直接子节点（Child 列表）。
        queryCmd.CommandText = "SELECT id, parent_id, text FROM nodesTable WHERE parent_id = @parent_id;";
        queryCmd.Parameters.Clear();
        queryCmd.Parameters.AddWithValue("@parent_id", id);
        using var childReader = queryCmd.ExecuteReader();
        var children = ReadNodeDataList(childReader);

        return new QueryData{Root = root, Child=children};
    }

    public Task<QueryData> QueryFunc(int id){
        return _run.Run(()=> QueryFunc2(id));
    }

    // ==================== 全文搜索 ====================

    List<NodeData> SearchFunc2(string searchText){

        using var searchCmd = _con.CreateCommand();

        // FTS5 MATCH 搜索，按 rank 排序，连接节点表获取完整数据。
        searchCmd.CommandText = """
            SELECT n.id, n.parent_id, n.text
            FROM textSearchTable t
            INNER JOIN nodesTable n ON t.rowid = n.id
            WHERE textSearchTable MATCH @searchText
            ORDER BY rank
            """;
        searchCmd.Parameters.AddWithValue("@searchText", searchText);

        using var reader = searchCmd.ExecuteReader();
        var results = ReadNodeDataList(reader);

        return results;
    }

    public Task<List<NodeData>> SearchFunc(string searchText){
        return _run.Run(()=> SearchFunc2(searchText));
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
            // 使用单条 SQL 查询同时完成全文搜索和递归路径追溯。
            string sql = @"
                WITH RECURSIVE
                  -- 1. 全文搜索获取匹配的节点 ID 和 Rank
                  MatchedNodes AS (
                    SELECT rowid AS id, rank
                    FROM textSearchTable
                    WHERE textSearchTable MATCH @keyword
                    ORDER BY rank
                    LIMIT @limit
                  ),
                  -- 2. 递归获取所有路径上的节点（包括匹配节点本身，depth=0）
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

            using var cmd = _con.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@keyword", searchKeyword);
            cmd.Parameters.AddWithValue("@limit", maxResults);

            using var reader = cmd.ExecuteReader();
            var allRecords = ReadAncestorRecords(reader);

            if (allRecords.Count == 0)
            {
                return new List<NodeSearchResult>();
            }

            // 调试输出：检查获取到的总记录数。
            System.Diagnostics.Trace.WriteLine($"单条 SQL 查询返回总记录数: {allRecords.Count}");

            // 3. 在内存中按 target_id 分组并重组结果。
            var results = allRecords
                .GroupBy(r => r.Target_Id)
                .OrderBy(g => g.First().Rank) // 保持 FTS 排序。
                .Select(g => new NodeSearchResult
                {
                    // depth 为 0 的记录是匹配项本身。
                    Item = g.Where(r => r.Depth == 0).Select(r => new NodeData
                    {
                        Id = r.Id,
                        Parent_Id = r.Parent_Id,
                        Text = r.Text
                    }).FirstOrDefault(),
                    // depth > 0 的记录是父节点，且已按 depth DESC 排序（根到子）。
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

    public Task<List<NodeSearchResult>> SearchNodesWithFullPath(string searchKeyword, int maxResults = 10){
        return _run.Run(()=> SearchNodesWithFullPath2(searchKeyword, maxResults));
    }

    // ==================== 根节点列表查询 ====================

    List<NodeData> SearchFunc2(){

        using var cmd = _con.CreateCommand();

        // 查询根节点（parent_id 为 NULL），按 id 降序，最多 100 条。
        cmd.CommandText = """
            SELECT id, parent_id, text
            FROM nodesTable
            WHERE parent_id IS NULL
            ORDER BY id DESC
            LIMIT 100
            """;

        using var reader = cmd.ExecuteReader();
        var results = ReadNodeDataList(reader);

        return results;
    }

    public Task<List<NodeData>> SearchFunc(){
        return _run.Run(()=> SearchFunc2());
    }

}


    public class NodeData{
        [JsonPropertyName("id")]
        public int Id{get;set;}

        [JsonPropertyName("parentId")]
        public int? Parent_Id {get;set;}

        [JsonPropertyName("title")]
        public string Text {get;set;}


    }

    public class QueryData{

        [JsonPropertyName("root")]
        public NodeData Root{get;set;}

        [JsonPropertyName("child")]
        public List<NodeData> Child{get;set;}


    }




    public class MessageData<T>{
        [JsonPropertyName("type")]
        public string Type{get;set;}

        [JsonPropertyName("index")]
        public int Index{get;set;}

        [JsonPropertyName("value")]
        public T Value {get;set;}

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
