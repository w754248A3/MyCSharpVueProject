using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
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


        this.Loaded+=Window_Loaded;
        
    }

    MyNodeDataStore _dataStore;
    private async void Window_Loaded(object sender, RoutedEventArgs e){

        _dataStore = await MyNodeDataStore.Create();
        InitImageStore();
        InitWebView2();

        this.Loaded-=Window_Loaded;

    }
    private void ReLoad_Click(object sender, RoutedEventArgs e)
    {
        webView2.CoreWebView2.Reload();
    }

    string _webapimesage="";
    private void Message_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(_webapimesage);
    }

    public void UpdateMessage(string message)
    {
        // 重点：Web API 运行在后台线程，WPF UI 只能在主线程更新。
        // 所以必须使用 Dispatcher 切换回 UI 线程！
        Dispatcher.Invoke(() =>
        {
            _webapimesage = message;
        });
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        

        

    }


    private void Export_Click(object sender, RoutedEventArgs e)
    {


    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        
        


        var json = File.ReadAllText("json_data.json",Encoding.UTF8);

        var vs = JsonSerializer.Deserialize<List<NodeData>>(json);

    }

    

    async Task<string> RunDataReadWriteSQL(string jsonString){

        using var jsondoc = JsonDocument.Parse(jsonString);


        var type = jsondoc.RootElement.GetProperty("type").GetString();

        var index = jsondoc.RootElement.GetProperty("index").GetInt32();

        if (type == MessageType.ADDNODE)
        {
            var obj = jsondoc.RootElement.GetProperty("value").Deserialize<NodeData>();

            var id = await _dataStore.Inset(obj);

            obj.Id=id;

            var s = JsonSerializer.Serialize(new MessageData<NodeData>{Type= MessageType.ADDNODE, Index= index, Value=obj});

            return s;
        }
        else if(type == MessageType.QUERY){

            var id = jsondoc.RootElement.GetProperty("value").GetInt32();


            var q = await _dataStore.QueryFunc(id);

            var s = JsonSerializer.Serialize(new MessageData<QueryData>{Type= MessageType.QUERY, Index= index, Value=q});

            return s;
        }
        else if(type == MessageType.SEARCH){

            var searchText = jsondoc.RootElement.GetProperty("value").GetString();

            List<NodeSearchResult> vs = new List<NodeSearchResult>();

            try{

                if(string.IsNullOrWhiteSpace(searchText)){
                    var roots = await _dataStore.SearchFunc();
                    vs = roots.Select(r => new NodeSearchResult { Item = r, Parents = new List<NodeData>() }).ToList();
                }
                else{
                    vs = await _dataStore.SearchNodesWithFullPath(searchText);
                }
            }
            catch(SqliteException ex){
                vs = new List<NodeSearchResult>();
            }
            catch(SqlException ex){
                vs = new List<NodeSearchResult>();
            }

               
            var s = JsonSerializer.Serialize(new MessageData<List<NodeSearchResult>>{Type= MessageType.SEARCH, Index= index, Value=vs});

            return s;
        }
        else if(type == MessageType.UPDATA){

            var obj = jsondoc.RootElement.GetProperty("value").Deserialize<NodeData>();

            obj = await _dataStore.UpData(obj);

            
            var s = JsonSerializer.Serialize(new MessageData<NodeData>{Type= MessageType.UPDATA, Index= index, Value=obj});

            return s;
        }
        else if(type == MessageType.CLIPBOARDHISTORY){
            
            var vs = _记录粘贴板.ToList().Reverse<string>().ToList();
            var s = JsonSerializer.Serialize(new MessageData<List<string>>{Type= MessageType.CLIPBOARDHISTORY, Index= index, Value=vs});

            return s;
        }
        else{
            throw new IndexOutOfRangeException("没有这个消息类型");
        }


    
    }


    public class MessageType{
        public const string ADDNODE = "ADDNODE";

        public const string QUERY = "QUERY";

        public const string SEARCH = "SEARCH";


        public const string UPDATA = "UPDATA";

        public const string CLIPBOARDHISTORY ="CLIPBOARDHISTORY";
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
        RegisterWebResourceRoutes(@"C:\Users\PC\code\MyCSharpVueProject\Vue\WebView2Page\dist");
        webView2.CoreWebView2.Navigate("https://mypage.test/");
      


       
    }










     [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    const int WM_CLIPBOARDUPDATE = 0x031D;
    HwndSource _hwndSource;

    protected override void OnClosing(CancelEventArgs e)
    {
        var result = MessageBox.Show("确定要退出吗？", "确认退出", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.No)
        {
            e.Cancel = true;
        }
        else
        {
            // 确认关闭时移除剪贴板监听器
            if (_hwndSource != null)
            {
                RemoveClipboardFormatListener(_hwndSource.Handle);
            }
        }
        base.OnClosing(e);
    }

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
