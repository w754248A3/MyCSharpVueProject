using System.Text.Json;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;

namespace MyNodeView;

public sealed class NodeDataApiService
{
    private readonly MyNodeDataStore _dataStore;

    public NodeDataApiService(MyNodeDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task<string> RunDataReadWriteSQL(string jsonString)
    {
        // 解析前端发送的统一消息格式，后续分支都使用同一个 JsonDocument。
        using var jsonDocument = JsonDocument.Parse(jsonString);
        var rootElement = jsonDocument.RootElement;
        var type = rootElement.GetProperty("type").GetString();
        var index = rootElement.GetProperty("index").GetInt32();

        // 新增节点，并把数据库生成的 Id 回传给前端。
        if (type == MessageType.ADDNODE)
        {
            var valueElement = rootElement.GetProperty("value");
            var node = valueElement.Deserialize<NodeData>();
            if (node is null)
            {
                throw new InvalidOperationException("节点数据不能为空");
            }

            var id = await _dataStore.Inset(node);
            node.Id = id;

            var response = new MessageData<NodeData>
            {
                Type = MessageType.ADDNODE,
                Index = index,
                Value = node
            };
            var json = JsonSerializer.Serialize(response);
            return json;
        }

        // 查询一个节点以及它的直接子节点。
        if (type == MessageType.QUERY)
        {
            var valueElement = rootElement.GetProperty("value");
            var id = valueElement.GetInt32();
            var queryData = await _dataStore.QueryFunc(id);

            var response = new MessageData<QueryData>
            {
                Type = MessageType.QUERY,
                Index = index,
                Value = queryData
            };
            var json = JsonSerializer.Serialize(response);
            return json;
        }

        // 搜索节点；空搜索词用于加载根节点列表。
        if (type == MessageType.SEARCH)
        {
            var valueElement = rootElement.GetProperty("value");
            var searchText = valueElement.GetString();
            var searchResults = await SearchNodesAsync(searchText);

            var response = new MessageData<List<NodeSearchResult>>
            {
                Type = MessageType.SEARCH,
                Index = index,
                Value = searchResults
            };
            var json = JsonSerializer.Serialize(response);
            return json;
        }

        // 更新节点文本，并把更新后的节点回传给前端。
        if (type == MessageType.UPDATA)
        {
            var valueElement = rootElement.GetProperty("value");
            var node = valueElement.Deserialize<NodeData>();
            if (node is null)
            {
                throw new InvalidOperationException("节点数据不能为空");
            }

            var updatedNode = await _dataStore.UpData(node);
            var response = new MessageData<NodeData>
            {
                Type = MessageType.UPDATA,
                Index = index,
                Value = updatedNode
            };
            var json = JsonSerializer.Serialize(response);
            return json;
        }

        // 未知消息类型直接抛出，Controller 会转换为 HTTP 错误响应。
        throw new IndexOutOfRangeException("没有这个消息类型");
    }

    private async Task<List<NodeSearchResult>> SearchNodesAsync(string? searchText)
    {
        // 空搜索词对应原来的根节点加载逻辑。
        if (string.IsNullOrWhiteSpace(searchText))
        {
            var roots = await _dataStore.SearchFunc();
            var rootResults = roots.Select(root => new NodeSearchResult
            {
                Item = root,
                Parents = new List<NodeData>()
            }).ToList();
            return rootResults;
        }

        // SQLite FTS 查询异常不让前端崩溃，保持原有逻辑返回空列表。
        try
        {
            var results = await _dataStore.SearchNodesWithFullPath(searchText);
            return results;
        }
        catch (SqliteException)
        {
            return new List<NodeSearchResult>();
        }
        catch (SqlException)
        {
            return new List<NodeSearchResult>();
        }
    }

    public static class MessageType
    {
        public const string ADDNODE = "ADDNODE";

        public const string QUERY = "QUERY";

        public const string SEARCH = "SEARCH";

        public const string UPDATA = "UPDATA";
    }
}
