using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace MyNodeView;

public partial class MainWindow
{
    private NodeImageStore? _imageStore;
    private string? _webRootPath;
    private static readonly JsonSerializerOptions ApiJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private void InitImageStore()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeImages.db");
        _imageStore = new NodeImageStore(dbPath);
    }

    private void RegisterWebResourceRoutes(string rootPath)
    {
        _webRootPath = rootPath;
        webView2.CoreWebView2.AddWebResourceRequestedFilter("https://mypage.test/*", CoreWebView2WebResourceContext.All);
        webView2.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
    }

    
#if DEBUG
    private readonly HttpClient _hc = new HttpClient();
    
#endif


    private async void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!e.Request.Uri.StartsWith("https://mypage.test/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {


            if(IsDataApiRequest(e.Request.Uri)){

                if(e.Request.Method != "POST"){
                    e.Response = SetErrorResponse("Method is not post");
                    return;
                }

                
                if(e.Request.Headers.GetHeader("Content-Type") != "application/json"){
                    e.Response = SetErrorResponse("Content-Type is not json");
                    return;
                }
                
                var ms = new MemoryStream();

                await e.Request.Content.CopyToAsync(ms);

                ms.Position=0;

                var s = Encoding.UTF8.GetString(ms.ToArray());

                var res = await RunDataReadWriteSQL(s);

                var by = Encoding.UTF8.GetBytes(res);

                var resms = new MemoryStream(by);
               
                var response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(resms, 200, "OK", BuildStaticHeaders("application/json", by.Length));
                e.Response = response;
                 
            }
            else if(IsImageApiRequest(e.Request.Uri)){
                e.Response =await HandleImageApiRequestAsync(e.Request);
            }
            else{
#if DEBUG   
                e.Response =await HandleDebugStaticFileRequest(e.Request);
#else
                e.Response =HandleStaticFileRequest(e.Request);
#endif

            }
         
        }
        catch (Exception ex)
        {
            e.Response = SetErrorResponse(ex.Message);
        }
        finally
        {
            deferral.Complete();
        }
    }

    CoreWebView2WebResourceResponse SetErrorResponse(string message){
        var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"error\":\"{EscapeJson(message)}\"}}"));
        return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, 500, "Internal Server Error", BuildStaticHeaders("application/json"));
    }

    private bool IsDataApiRequest(string requestUri)
    {
        return requestUri.StartsWith("https://mypage.test/api/data", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsImageApiRequest(string requestUri)
    {
        return requestUri.StartsWith("https://mypage.test/api/images", StringComparison.OrdinalIgnoreCase);
    }
#if DEBUG
    private async  Task<CoreWebView2WebResourceResponse> HandleDebugStaticFileRequest(CoreWebView2WebResourceRequest request){

        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return webView2.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 405, "Method Not Allowed", BuildStaticHeaders("text/plain; charset=utf-8"));
        }

        var uri = new UriBuilder(request.Uri);

        uri.Scheme="http";

        uri.Host="localhost";

        uri.Port=5173;

        HttpRequestMessage rm = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
        
        foreach (var item in request.Headers)
        {
            rm.Headers.Add(item.Key, item.Value);
        }

        var res = await _hc.SendAsync(rm);

        var stream = await res.Content.ReadAsStreamAsync();

        var res2 = webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, ((int)res.StatusCode), res.ReasonPhrase, "");
        
        foreach (var item in res.Headers)
        {
            res2.Headers.AppendHeader(item.Key, string.Join(',', item.Value));
            
        }

        foreach (var item in res.Content.Headers)
        {
            res2.Headers.AppendHeader(item.Key, string.Join(',', item.Value));
            
        }


        return res2;
    }
#endif
    private CoreWebView2WebResourceResponse HandleStaticFileRequest(CoreWebView2WebResourceRequest request)
    {

        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return webView2.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 405, "Method Not Allowed", BuildStaticHeaders("text/plain; charset=utf-8"));
        }

        if (string.IsNullOrWhiteSpace(_webRootPath) || !Directory.Exists(_webRootPath))
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("Static resource root not found."));
            return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, 500, "Internal Server Error", BuildStaticHeaders("text/plain; charset=utf-8"));
        }

        var requestUri = new Uri(request.Uri);
        var relativePath = requestUri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(relativePath))
        {
            relativePath = "index.html";
        }

        var safePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(_webRootPath, safePath));
        var rootFullPath = Path.GetFullPath(_webRootPath);
        if (!candidatePath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return webView2.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 403, "Forbidden", BuildStaticHeaders("text/plain; charset=utf-8"));
        }

        if (!File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(_webRootPath, "index.html");
            if (!File.Exists(candidatePath))
            {
                return webView2.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", BuildStaticHeaders("text/plain; charset=utf-8"));
            }
        }

        var mimeType = GuessMimeType(candidatePath);
        var stream2 = new FileStream(candidatePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream2, 200, "OK", BuildStaticHeaders(mimeType));
    }

    private static string GuessMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".mjs" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream"
        };
    }

    private async Task<CoreWebView2WebResourceResponse> HandleImageApiRequestAsync(CoreWebView2WebResourceRequest request)
    {
        if (_imageStore is null)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"error\":\"Image store not initialized\"}"));
            return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, 500, "Internal Server Error", BuildStaticHeaders("application/json"));
        }

        var uri = new Uri(request.Uri);
        var path = uri.AbsolutePath;
        var method = request.Method.ToUpperInvariant();

        if (method == "OPTIONS")
        {
            return webView2.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 204, "No Content", BuildStaticHeaders("text/plain"));
        }

        if (method == "GET" && path.EndsWith("/meta", StringComparison.OrdinalIgnoreCase))
        {
            var nodeId = ParseIntQuery(uri, "nodeId");
            if (nodeId is null)
            {
                return CreateBadRequest("nodeId is required");
            }

            var summary = await _imageStore.GetSummaryAsync(nodeId.Value);
            var payload = JsonSerializer.Serialize(summary, ApiJsonSerializerOptions);
            return CreateJsonResponse(payload);
        }

        if (method == "GET" && path.EndsWith("/list", StringComparison.OrdinalIgnoreCase))
        {
            var nodeId = ParseIntQuery(uri, "nodeId");
            if (nodeId is null)
            {
                return CreateBadRequest("nodeId is required");
            }

            var list = await _imageStore.ListAsync(nodeId.Value);
            var payload = JsonSerializer.Serialize(list, ApiJsonSerializerOptions);
            return CreateJsonResponse(payload);
        }

        if (method == "GET" && path.EndsWith("/content", StringComparison.OrdinalIgnoreCase))
        {
            var id = ParseLongQuery(uri, "id");
            if (id is null)
            {
                return CreateBadRequest("id is required");
            }

            var image = await _imageStore.GetImageAsync(id.Value);
            if (image is null)
            {
                return CreateNotFound();
            }

            var stream = new MemoryStream(image.Data, writable: false);
            return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, 200, "OK", BuildStaticHeaders(image.MimeType, image.Data.LongLength));
        }

        if (method == "POST" && path.EndsWith("/upload", StringComparison.OrdinalIgnoreCase))
        {
            var nodeId = ParseIntQuery(uri, "nodeId");
            if (nodeId is null)
            {
                return CreateBadRequest("nodeId is required");
            }

            var mimeType = request.Headers.GetHeader("Content-Type");
            if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return CreateBadRequest("Content-Type must be image/*");
            }

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                if (request.Content is null)
                {
                    return CreateBadRequest("Request body is empty");
                }

                await request.Content.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            if (imageBytes.Length == 0)
            {
                return CreateBadRequest("Request body is empty");
            }

            if (imageBytes.LongLength > 20 * 1024 * 1024)
            {
                return CreateBadRequest("Image is too large (max 20MB)");
            }

            var fileNameRaw = request.Headers.GetHeader("X-File-Name");
            var fileName = string.IsNullOrWhiteSpace(fileNameRaw) ? string.Empty : Uri.UnescapeDataString(fileNameRaw);

            var imageId = await _imageStore.InsertAsync(nodeId.Value, fileName, mimeType, imageBytes);
            var payload = JsonSerializer.Serialize(new { id = imageId }, ApiJsonSerializerOptions);
            return CreateJsonResponse(payload, 201, "Created");
        }

        if (method == "DELETE" && path.EndsWith("/delete", StringComparison.OrdinalIgnoreCase))
        {
            var id = ParseLongQuery(uri, "id");
            if (id is null)
            {
                return CreateBadRequest("id is required");
            }

            var ok = await _imageStore.DeleteAsync(id.Value);
            if (!ok)
            {
                return CreateNotFound();
            }

            return CreateJsonResponse("{\"ok\":true}");
        }

        return webView2.CoreWebView2.Environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", BuildStaticHeaders("text/plain"));
    }

    private CoreWebView2WebResourceResponse CreateJsonResponse(string json, int statusCode = 200, string statusText = "OK")
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, statusCode, statusText, BuildStaticHeaders("application/json; charset=utf-8"));
    }

    private CoreWebView2WebResourceResponse CreateBadRequest(string message)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{{\"error\":\"{EscapeJson(message)}\"}}"));
        return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, 400, "Bad Request", BuildStaticHeaders("application/json; charset=utf-8"));
    }

    private CoreWebView2WebResourceResponse CreateNotFound()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"error\":\"Not found\"}"));
        return webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream, 404, "Not Found", BuildStaticHeaders("application/json; charset=utf-8"));
    }

    private static int? ParseIntQuery(Uri uri, string key)
    {
        var value = GetQueryValue(uri, key);
        return int.TryParse(value, out var id) ? id : null;
    }

    private static long? ParseLongQuery(Uri uri, string key)
    {
        var value = GetQueryValue(uri, key);
        return long.TryParse(value, out var id) ? id : null;
    }

    private static string? GetQueryValue(Uri uri, string key)
    {
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var items = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            var kv = item.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }

    private static string BuildStaticHeaders(string contentType, long? contentLength = null)
    {
        var sb = new StringBuilder();
        sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
        if (contentLength.HasValue)
        {
            sb.Append("Content-Length: ").Append(contentLength.Value).Append("\r\n");
        }
        sb.Append("Cache-Control: no-store\r\n");
        return sb.ToString();
    }


    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
