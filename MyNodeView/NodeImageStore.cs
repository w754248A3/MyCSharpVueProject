using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace MyNodeView;

public sealed class NodeImageStore
{
    private readonly string _connectionString;

    public NodeImageStore(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DataSource = dbPath
        }.ToString();

        InitializeAsync().GetAwaiter().GetResult();
    }

    async Task ApplyPragmas(SqliteConnection con){
        using var pragma = con.CreateCommand();
      
        pragma.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA cache_size = -20000;
            PRAGMA temp_store = MEMORY;
        ";
        await pragma.ExecuteNonQueryAsync();

        //GetPragmas(con);
    }

    void GetPragmas(SqliteConnection con)
    {
        using var cmd = con.CreateCommand();
        string[] pragmas = ["journal_mode", "synchronous", "cache_size", "temp_store"];

        foreach (var p in pragmas)
        {
            cmd.CommandText = $"PRAGMA {p};";
            var value = cmd.ExecuteScalar();
            Debug.WriteLine($"{p} = {value}");
        }
    }

    private async Task InitializeAsync()
    {
        await using var con = new SqliteConnection(_connectionString);
        await con.OpenAsync();
        await ApplyPragmas(con);

        var createTable = con.CreateCommand();
        createTable.CommandText = """
        CREATE TABLE IF NOT EXISTS node_images (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            node_id INTEGER NOT NULL,
            file_name TEXT,
            mime_type TEXT NOT NULL,
            image_data BLOB NOT NULL,
            created_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
        );
        """;
        await createTable.ExecuteNonQueryAsync();

        var createIndex = con.CreateCommand();
        createIndex.CommandText = """
        CREATE INDEX IF NOT EXISTS idx_node_images_node_id
        ON node_images(node_id, id DESC);
        """;
        await createIndex.ExecuteNonQueryAsync();
    }

    public async Task<NodeImageSummary> GetSummaryAsync(int nodeId)
    {
        await using var con = new SqliteConnection(_connectionString);
        await con.OpenAsync();
        await ApplyPragmas(con);
        var cmd = con.CreateCommand();
        cmd.CommandText = """
        SELECT
            COUNT(1) AS total_count,
            (
                SELECT id
                FROM node_images
                WHERE node_id = $nodeId
                ORDER BY id DESC
                LIMIT 1
            ) AS latest_id
        FROM node_images
        WHERE node_id = $nodeId;
        """;
        cmd.Parameters.AddWithValue("$nodeId", nodeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        return new NodeImageSummary
        {
            Count = reader.GetInt32(0),
            LatestImageId = reader.IsDBNull(1) ? null : reader.GetInt64(1)
        };
    }

    public async Task<List<NodeImageInfo>> ListAsync(int nodeId)
    {
        await using var con = new SqliteConnection(_connectionString);
        await con.OpenAsync();
        await ApplyPragmas(con);
        var cmd = con.CreateCommand();
        cmd.CommandText = """
        SELECT id, file_name, mime_type, length(image_data) AS size, created_utc
        FROM node_images
        WHERE node_id = $nodeId
        ORDER BY id DESC;
        """;
        cmd.Parameters.AddWithValue("$nodeId", nodeId);

        var list = new List<NodeImageInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new NodeImageInfo
            {
                Id = reader.GetInt64(0),
                FileName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                MimeType = reader.GetString(2),
                Size = reader.GetInt64(3),
                CreatedUtc = reader.GetString(4)
            });
        }

        return list;
    }

    public async Task<long> InsertAsync(int nodeId, string? fileName, string mimeType, byte[] imageData)
    {
        await using var con = new SqliteConnection(_connectionString);
        await con.OpenAsync();
        await ApplyPragmas(con);
        var cmd = con.CreateCommand();
        cmd.CommandText = """
        INSERT INTO node_images(node_id, file_name, mime_type, image_data)
        VALUES($nodeId, $fileName, $mimeType, $imageData);
        SELECT last_insert_rowid();
        """;

        cmd.Parameters.AddWithValue("$nodeId", nodeId);
        cmd.Parameters.AddWithValue("$fileName", (object?)fileName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mimeType", mimeType);
        cmd.Parameters.Add("$imageData", SqliteType.Blob).Value = imageData;

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    public async Task<NodeImageBlob?> GetImageAsync(long id)
    {
        await using var con = new SqliteConnection(_connectionString);
        await con.OpenAsync();
        await ApplyPragmas(con);
        var cmd = con.CreateCommand();
        cmd.CommandText = """
        SELECT id, node_id, file_name, mime_type, image_data
        FROM node_images
        WHERE id = $id;
        """;
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new NodeImageBlob
        {
            Id = reader.GetInt64(0),
            NodeId = reader.GetInt32(1),
            FileName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            MimeType = reader.GetString(3),
            Data = (byte[])reader[4]
        };
    }

    public async Task<bool> DeleteAsync(long id)
    {
        await using var con = new SqliteConnection(_connectionString);
        await con.OpenAsync();
        await ApplyPragmas(con);
        var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM node_images WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }
}

public sealed class NodeImageSummary
{
    public int Count { get; set; }
    public long? LatestImageId { get; set; }
}

public sealed class NodeImageInfo
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
}

public sealed class NodeImageBlob
{
    public long Id { get; set; }
    public int NodeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
