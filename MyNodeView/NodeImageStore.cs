using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace MyNodeView;

public sealed class NodeImageStore : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly object _lock = new();
    private bool _disposed;

    static int s_newCount =0;

    public NodeImageStore()
    {
        
        if(Interlocked.Exchange(ref s_newCount, 1)!=0){
            throw new InvalidOperationException("类只能有一个实例");
        }


        var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeImages.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DataSource = dbPath
        }.ToString();

        _connection = new SqliteConnection(_connectionString);
        Initialize();
    }

    private Task<T> RunSerializedAsync<T>(Func<T> func)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                return func();
            }
        });
    }

    private Task RunSerializedAsync(Action action)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                action();
            }
        });
    }

    void ApplyPragmas(SqliteConnection con)
    {
        using var pragma = con.CreateCommand();

        pragma.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA cache_size = -20000;
            PRAGMA temp_store = MEMORY;
        ";
        pragma.ExecuteNonQuery();

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

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NodeImageStore));
        }
    }

    private void Initialize()
    {
        _connection.Open();
        ApplyPragmas(_connection);
        using var tr = _connection.BeginTransaction();

        using var createTable = _connection.CreateCommand();
        createTable.Transaction = tr;
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
        createTable.Prepare();
        createTable.ExecuteNonQuery();

        using var createIndex = _connection.CreateCommand();
        createIndex.Transaction = tr;
        createIndex.CommandText = """
        CREATE INDEX IF NOT EXISTS idx_node_images_node_id
        ON node_images(node_id, id DESC);
        """;
        createIndex.Prepare();
        createIndex.ExecuteNonQuery();

        tr.Commit();
    }

    public Task<NodeImageSummary> GetSummaryAsync(int nodeId)
    {
        return RunSerializedAsync(() =>
        {
            ThrowIfDisposed();
            using var tr = _connection.BeginTransaction();

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tr;
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
            cmd.Prepare();

            using var reader = cmd.ExecuteReader();
            reader.Read();

            var result = new NodeImageSummary
            {
                Count = reader.GetInt32(0),
                LatestImageId = reader.IsDBNull(1) ? null : reader.GetInt64(1)
            };

            tr.Commit();
            return result;
        });
    }

    public Task<List<NodeImageInfo>> ListAsync(int nodeId)
    {
        return RunSerializedAsync(() =>
        {
            ThrowIfDisposed();
            using var tr = _connection.BeginTransaction();

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tr;
            cmd.CommandText = """
            SELECT id, file_name, mime_type, length(image_data) AS size, created_utc
            FROM node_images
            WHERE node_id = $nodeId
            ORDER BY id DESC;
            """;
            cmd.Parameters.AddWithValue("$nodeId", nodeId);
            cmd.Prepare();

            var list = new List<NodeImageInfo>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
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

            tr.Commit();
            return list;
        });
    }

    public Task<long> InsertAsync(int nodeId, string? fileName, string mimeType, byte[] imageData)
    {
        return RunSerializedAsync(() =>
        {
            ThrowIfDisposed();
            using var tr = _connection.BeginTransaction();

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tr;
            cmd.CommandText = """
            INSERT INTO node_images(node_id, file_name, mime_type, image_data)
            VALUES($nodeId, $fileName, $mimeType, $imageData);
            SELECT last_insert_rowid();
            """;

            cmd.Parameters.AddWithValue("$nodeId", nodeId);
            cmd.Parameters.AddWithValue("$fileName", (object?)fileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mimeType", mimeType);
            cmd.Parameters.Add("$imageData", SqliteType.Blob).Value = imageData;
            cmd.Prepare();

            var result = cmd.ExecuteScalar();
            tr.Commit();
            return Convert.ToInt64(result);
        });
    }

    public Task<NodeImageBlob?> GetImageAsync(long id)
    {
        return RunSerializedAsync(() =>
        {
            ThrowIfDisposed();
            using var tr = _connection.BeginTransaction();

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tr;
            cmd.CommandText = """
            SELECT id, node_id, file_name, mime_type, image_data
            FROM node_images
            WHERE id = $id;
            """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Prepare();

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                tr.Commit();
                return null;
            }

            var result = new NodeImageBlob
            {
                Id = reader.GetInt64(0),
                NodeId = reader.GetInt32(1),
                FileName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                MimeType = reader.GetString(3),
                Data = (byte[])reader[4]
            };

            tr.Commit();
            return result;
        });
    }

    public Task<bool> DeleteAsync(long id)
    {
        return RunSerializedAsync(() =>
        {
            ThrowIfDisposed();
            using var tr = _connection.BeginTransaction();

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tr;
            cmd.CommandText = "DELETE FROM node_images WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Prepare();

            var affected = cmd.ExecuteNonQuery();
            tr.Commit();
            return affected > 0;
        });
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _connection.Dispose();
            _disposed = true;
        }
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
