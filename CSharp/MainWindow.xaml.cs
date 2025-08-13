using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Metrics;
using System.IO;
using System.Text;
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



        Init();
    }
    private void ReLoad_Click(object sender, RoutedEventArgs e)
    {
        webView2.CoreWebView2.Reload();
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        var connectionString = new SqliteConnectionStringBuilder()
        {
            Mode = SqliteOpenMode.ReadWriteCreate,

            Cache = SqliteCacheMode.Shared,

            DataSource="Xb1r83sB.db"





        }.ToString();

        using var db = new DataConnection(LinqToDB.ProviderName.SQLiteMS, connectionString);


        db.Execute("PRAGMA foreign_keys = ON;");

        if(db.Execute<int>("PRAGMA foreign_keys;") != 1){
            throw new InvalidOperationException("数据库不支持外键约束");
        }

        db.Execute("""
        CREATE TABLE IF NOT EXISTS nodes (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        parent_id INTEGER,
        FOREIGN KEY (parent_id) REFERENCES nodes(id)
        );
        """);
        
        db.Execute("""
        CREATE TABLE IF NOT EXISTS node_content (
        node_id INTEGER PRIMARY KEY,
        title TEXT NOT NULL,
        description TEXT,
        FOREIGN KEY (node_id) REFERENCES nodes(id)
        );
        """);

        int? parent_id=null;


        int id = db.InsertWithInt32Identity(new {parent_id=parent_id}, tableName:"nodes");


        db.InsertWithInt32Identity(new {parent_id=id}, tableName:"nodes");

        db.Insert(new {node_id=1, title= "标题1", description="描述"}, tableName:"node_content");

        db.Insert(new {node_id=100, title= "标题2", description="描述2"}, tableName:"node_content");



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

        webView2.CoreWebView2.WebMessageReceived += (sender, args) =>
        {
           
            string message = args.WebMessageAsJson;
            webView2.CoreWebView2.PostWebMessageAsString(message);
        };



       
    }
}