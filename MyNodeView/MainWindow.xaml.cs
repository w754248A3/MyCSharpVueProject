using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Metrics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Linq;
using LinqToDB.Mapping;
using LinqToDB.SqlQuery;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;

namespace MyNodeView;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{


    public MainWindow()
    {
        InitializeComponent();


        InitData();
        InitWebView2();

        
    }
    private void ReLoad_Click(object sender, RoutedEventArgs e)
    {
        webView2.CoreWebView2.Reload();
    }

    DataConnection _con;

    void InitData(){

        static void RegisterCustomTokenizer(DataConnection db){
            if (db.Connection is not SqliteConnection con)
            {

                throw new ArgumentException("Connection is not SqliteConnection or null");
            }


            SqliteEx.MySqliteTokenizer.RegisterCustomTokenizer(con, "mytokenizer");

        }


        var connectionString = new SqliteConnectionStringBuilder()
        {
            Mode = SqliteOpenMode.ReadWriteCreate,

            Cache = SqliteCacheMode.Shared,

            DataSource="Xb1r83sB.db"





        }.ToString();

        
        var db = new DataConnection(LinqToDB.ProviderName.SQLiteMS, connectionString);

        var v = db.Connection.ServerVersion;

        RegisterCustomTokenizer(db);

        db.Execute("PRAGMA foreign_keys = ON;");

        if(db.Execute<int>("PRAGMA foreign_keys;") != 1){
            throw new InvalidOperationException("数据库不支持外键约束");
        }

        db.Execute("""
        CREATE TABLE IF NOT EXISTS nodesTable (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        parent_id INTEGER,
        text TEXT NOT NULL, 
        FOREIGN KEY (parent_id) REFERENCES nodesTable(id) ON DELETE RESTRICT ON UPDATE NO ACTION
        );
        """);

        db.Execute("""
        CREATE INDEX IF NOT EXISTS idx_nodes_parent ON nodesTable(parent_id);
        """);


        db.Execute("""
        CREATE VIRTUAL TABLE IF NOT EXISTS  textSearchTable USING fts5(
        Value, 
        tokenize="mytokenizer", 
        content='',
        contentless_delete=1
        )
        """);

        _con=db;
      
    }

    int Inset(NodeData data){
        using(var tr= _con.BeginTransaction()){


           var id =  _con.InsertWithInt32Identity(new { parent_id =data.Parent_Id, text = data.Text}, tableName:"nodesTable");


           _con.Insert(new {rowid = id, Value=data.Text}, tableName:"textSearchTable");


            tr.Commit();

            return id;
        }
    }

    NodeData UpData(NodeData data){
        using(var tr= _con.BeginTransaction()){


           _con.GetTable<NodeData>().TableName("nodesTable")
           .Where(p=> p.Id== data.Id)
           .Set(p=>p.Text, data.Text)
           .Update();
           


            _con.GetTable<SearchData>().TableName("textSearchTable")
           .Where(p=> _ex.RowId(p)== data.Id)
           .Set(p=>p.Value, data.Text)
           .Update();

            tr.Commit();

            return data;
        }
    }


    QueryData QueryFunc(int id){

        var obj = _con.GetTable<NodeData>().TableName("nodesTable")
        .Where(p=> p.Id==id).First();


        var vs = _con.GetTable<NodeData>().TableName("nodesTable")
        .Where(p=> p.Parent_Id ==id).ToList();


        return new QueryData{Root = obj, Child=vs};



    }

    public class SearchData
    {
        [LinqToDB.Mapping.Column("rowid")]
        public uint Rowid { get; set; }

        [LinqToDB.Mapping.Column("Value")]
        public string Value { get; set; }        
    }

    readonly ISQLiteExtensions _ex = Sql.Ext.SQLite();
    List<NodeData> SearchFunc(string searchText){

        var vs = _con.GetTable<SearchData>().TableName("textSearchTable")
        .Where(p=> _ex.Match(p.Value, searchText))
        .Select(p => new { RowId = _ex.RowId(p), Rank = _ex.Rank(p) })
        
        .InnerJoin(_con.GetTable<NodeData>().TableName("nodesTable"), (a,b)=> a.RowId == b.Id, (a, b) => new { Value = b, a.Rank })
        .OrderBy(p => p.Rank)
        .Select(p => p.Value)
        .ToList();



        return vs;


    }

    /// <summary>
    /// 全文搜索节点并返回从根节点到匹配节点的完整路径。
    /// </summary>
    /// <param name="searchKeyword">要搜索的关键字。</param>
    /// <param name="maxResults">限制返回的最大结果数量，默认值为 10。</param>
    /// <returns>包含匹配节点及其父节点路径的列表。</returns>
    /// <exception cref="Exception">当数据库操作失败时抛出封装后的异常。</exception>
    public List<NodeSearchResult> SearchNodesWithFullPath(string searchKeyword, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchKeyword))
        {
            return new List<NodeSearchResult>();
        }

        try
        {
            // 使用单条 SQL 查询同时完成全文搜索和递归路径追溯
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

            var allRecords = _con.Query<AncestorRecord>(sql, new { keyword = searchKeyword, limit = maxResults }).ToList();

            if (allRecords.Count == 0)
            {
                return new List<NodeSearchResult>();
            }

            // 调试输出：检查获取到的总记录数
            System.Diagnostics.Trace.WriteLine($"单条 SQL 查询返回总记录数: {allRecords.Count}");

            // 3. 在内存中按 target_id 分组并重组结果
            var results = allRecords
                .GroupBy(r => r.Target_Id)
                .OrderBy(g => g.First().Rank) // 保持 FTS 排序
                .Select(g => new NodeSearchResult
                {
                    // depth 为 0 的记录是匹配项本身
                    Item = g.Where(r => r.Depth == 0).Select(r => new NodeData
                    {
                        Id = r.Id,
                        Parent_Id = r.Parent_Id,
                        Text = r.Text
                    }).FirstOrDefault(),
                    // depth > 0 的记录是父节点，且已按 depth DESC 排序（根到子）
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
        catch (SqliteException ex)
        {
            System.Diagnostics.Trace.WriteLine($"SearchNodesWithFullPath 发生错误: {ex.Message}");
            
            throw;
        }
    }

    List<NodeData> SearchFunc(){

        var vs = _con.GetTable<NodeData>().TableName("nodesTable")
        .Where(p=> p.Parent_Id == null)
        .OrderByDescending(p=> p.Id)
        .Take(100)
        .ToList();



        return vs;


    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        

        

    }


    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var vs = _con.GetTable<NodeData>().TableName("nodesTable").ToList();

        var json = JsonSerializer.Serialize(vs);


        File.WriteAllText("json_data.json", json, Encoding.UTF8);

    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        
        


        var json = File.ReadAllText("json_data.json",Encoding.UTF8);

        var vs = JsonSerializer.Deserialize<List<NodeData>>(json);

    }

    void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e){

        using var jsondoc = JsonDocument.Parse(e.WebMessageAsJson);


        var type = jsondoc.RootElement.GetProperty("type").GetString();

        var index = jsondoc.RootElement.GetProperty("index").GetInt32();

        if (type == MessageType.ADDNODE)
        {
            var obj = jsondoc.RootElement.GetProperty("value").Deserialize<NodeData>();

            var id = Inset(obj);

            obj.Id=id;

            var s = JsonSerializer.Serialize(new MessageData<NodeData>{Type= MessageType.ADDNODE, Index= index, Value=obj});

            webView2.CoreWebView2.PostWebMessageAsString(s);
        }
        else if(type == MessageType.QUERY){

            var id = jsondoc.RootElement.GetProperty("value").GetInt32();


            var q = QueryFunc(id);

            var s = JsonSerializer.Serialize(new MessageData<QueryData>{Type= MessageType.QUERY, Index= index, Value=q});

            webView2.CoreWebView2.PostWebMessageAsString(s);
        }
        else if(type == MessageType.SEARCH){

            var searchText = jsondoc.RootElement.GetProperty("value").GetString();

            List<NodeData> vs;

            try{

                if(string.IsNullOrWhiteSpace(searchText)){
                    vs = SearchFunc();
                }
                else{

                    var vvs = SearchNodesWithFullPath(searchText);
                    var options = new JsonSerializerOptions
                    {Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true // 关键点：开启美化打印
                    };
                    var ss = JsonSerializer.Serialize(vvs, options);

                    System.Diagnostics.Trace.WriteLine(ss);
                    


                    vs = SearchFunc(searchText);
                }
            }
            catch(SqliteException ex){
                vs = new List<NodeData>();
            }
            catch(SqlException ex){
                vs = new List<NodeData>();
            }

               
            var s = JsonSerializer.Serialize(new MessageData<List<NodeData>>{Type= MessageType.SEARCH, Index= index, Value=vs});


            webView2.CoreWebView2.PostWebMessageAsString(s);
        }
        else if(type == MessageType.UPDATA){

            var obj = jsondoc.RootElement.GetProperty("value").Deserialize<NodeData>();

            obj = UpData(obj);

            
            var s = JsonSerializer.Serialize(new MessageData<NodeData>{Type= MessageType.UPDATA, Index= index, Value=obj});

            webView2.CoreWebView2.PostWebMessageAsString(s);
        }
        else if(type == MessageType.CLIPBOARDHISTORY){
            
            var vs = _记录粘贴板.ToList().Reverse<string>().ToList();
            var s = JsonSerializer.Serialize(new MessageData<List<string>>{Type= MessageType.CLIPBOARDHISTORY, Index= index, Value=vs});

            webView2.CoreWebView2.PostWebMessageAsString(s);
        }
        else{
            throw new IndexOutOfRangeException("没有这个消息类型");
        }


    
    }

    public class NodeData{
        [LinqToDB.Mapping.Column("id")]
        [JsonPropertyName("id")]
        public int Id{get;set;}

        [LinqToDB.Mapping.Column("parent_id")]
        [JsonPropertyName("parentId")]
        public int? Parent_Id {get;set;}

        [LinqToDB.Mapping.Column("text")]
        [JsonPropertyName("title")]
        public string Text {get;set;}


    }

    public class QueryData{

        [JsonPropertyName("root")]  
        public NodeData Root{get;set;}

        [JsonPropertyName("child")]
        public List<NodeData> Child{get;set;}


    }


    public class MessageType{
        public const string ADDNODE = "ADDNODE";

        public const string QUERY = "QUERY";

        public const string SEARCH = "SEARCH";


        public const string UPDATA = "UPDATA";

        public const string CLIPBOARDHISTORY ="CLIPBOARDHISTORY";
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
        [LinqToDB.Mapping.Column("id")]
        public int Id { get; set; }

        [LinqToDB.Mapping.Column("parent_id")]
        public int? Parent_Id { get; set; }

        [LinqToDB.Mapping.Column("text")]
        public string Text { get; set; }

        [LinqToDB.Mapping.Column("target_id")]
        public int Target_Id { get; set; }

        [LinqToDB.Mapping.Column("depth")]
        public int Depth { get; set; }

        [LinqToDB.Mapping.Column("rank")]
        public double Rank { get; set; }
    }

    async void InitWebView2(){


        string GetUserDataPath(){


            var s = AppDomain.CurrentDomain.BaseDirectory;


            return System.IO.Path.Combine(s, "MyNodeView.exe.WebView2");
        }

        var info = new CoreWebView2EnvironmentOptions();

        var s = GetUserDataPath();

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder:s, options: info);



        await webView2.EnsureCoreWebView2Async(environment);
        webView2.CoreWebView2.SetVirtualHostNameToFolderMapping("mypage.test", 
         @"C:\Users\PC\code\MyCSharpVueProject\Vue\WebView2Page\dist", CoreWebView2HostResourceAccessKind.DenyCors);
        webView2.CoreWebView2.Navigate("https://mypage.test/index.html");

        webView2.CoreWebView2.WebMessageReceived += OnWebMessageReceived;



       
    }










     [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    const int WM_CLIPBOARDUPDATE = 0x031D;
    HwndSource _hwndSource;
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        //IntPtr windowHandle = new WindowInteropHelper(this).Handle;

        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        AddClipboardFormatListener(_hwndSource.Handle);
        _hwndSource.AddHook(WndProc);
    }

    
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        


        if (msg == WM_CLIPBOARDUPDATE)
        {
            string data = F尝试获取粘贴板的值();
            if (data is not null)
            {
                F添加已记录的粘贴板值(data);
            }

        }

        return IntPtr.Zero;
    }

    static string F尝试获取粘贴板的值()
    {


        const uint ERRORCODE = 0x800401D0;


        try
        {
            IDataObject iData = Clipboard.GetDataObject();
            if (iData is not null && iData.GetDataPresent(DataFormats.UnicodeText))
            {
                string data = (string)iData.GetData(DataFormats.UnicodeText);
                // 使用data进行后续操作

                return data;
            }
            else
            {
                return null;
            }
        }
        catch (COMException ex)
        {
            
            return null;
        }
        

    }


    Queue<string> _记录粘贴板 = new Queue<string>();
    string _上一个粘贴板记录的值 = string.Empty;
    string _应用本身主动写入的粘贴板值 = string.Empty;
    void F添加已记录的粘贴板值(string text)
    {
        if (text.Equals(_上一个粘贴板记录的值, StringComparison.Ordinal)||
            text.Equals(_应用本身主动写入的粘贴板值, StringComparison.Ordinal))
        {
            return;
        }
        else
        {
            _上一个粘贴板记录的值 = text;
        }


        _记录粘贴板.Enqueue(text);
        if (_记录粘贴板.Count > 20)
        {


            _记录粘贴板.Dequeue();
        }
    }

}