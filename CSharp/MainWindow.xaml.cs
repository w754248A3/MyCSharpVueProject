using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Metrics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Linq;
using LinqToDB.Mapping;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;

namespace CSharp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{


    public class NodesTable
    {
        
        public int Id{get;set;}
    }

    public MainWindow()
    {
        InitializeComponent();


        InitData();
        Init();

        
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
        FOREIGN KEY (parent_id) REFERENCES nodesTable(id) ON DELETE RESTRICT ON UPDATE RESTRICT
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


    QueryData QueryFunc(int id){

        var obj = _con.GetTable<NodeData>().TableName("nodesTable")
        .Where(p=> p.Id==id).First();


        var vs = _con.GetTable<NodeData>().TableName("nodesTable")
        .Where(p=> p.Parent_Id ==id).ToList();


        return new QueryData{Root = obj, Child=vs};



    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        

        

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


           
            var s = JsonSerializer.Serialize(new MessageData<List<NodeData>>{Type= MessageType.SEARCH, Index= index, Value=
            [new NodeData{Id=1, Parent_Id=null, Text="1"}]});

            webView2.CoreWebView2.PostWebMessageAsString(s);
        }
        else{
            throw new IndexOutOfRangeException("没有这个消息类型");
        }


        

    //webView2.CoreWebView2.PostWebMessageAsString(message);
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
    }

    public class MessageData<T>{
        [JsonPropertyName("type")]
        public string Type{get;set;}

        [JsonPropertyName("index")]
        public int Index{get;set;}

        [JsonPropertyName("value")]
        public T Value {get;set;}

    }

    async void Init(){


        string GetUserDataPath(){


            var s = AppDomain.CurrentDomain.BaseDirectory;


            return System.IO.Path.Combine(s, "CSharp.exe.WebView2");
        }

        var info = new CoreWebView2EnvironmentOptions();

        var s = GetUserDataPath();

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder:s, options: info);



        await webView2.EnsureCoreWebView2Async(environment);
        webView2.CoreWebView2.SetVirtualHostNameToFolderMapping("mypage.test", 
         @"C:\Users\PC\code\MyCSharpVueProject\Vue\WebView2Page\dist", CoreWebView2HostResourceAccessKind.DenyCors);
        webView2.CoreWebView2.Navigate("https://mypage.test/index.html");


        //webView2.Source= new Uri(@"C:\Users\PC\cpp\myvue\fileView\dist\index.html");

        webView2.CoreWebView2.WebMessageReceived += OnWebMessageReceived;



       
    }
}