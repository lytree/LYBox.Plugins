using Microsoft.Data.Sqlite;
using LYBox.Plugin.DouyinDownloader.Config;

namespace LYBox.Plugin.DouyinDownloader.Storage;

/// <summary>
/// SQLite 历史/去重（与 storage/database.py 的 aweme 表对齐 + 增量模式 latest_time 查询）。
/// </summary>
public sealed class DownloadDatabase
{
    private readonly string _connStr;
    private bool _initialized;
    private readonly object _lock = new();

    public DownloadDatabase(PluginConfigStore paths, DownloaderSettingsStore settings)
    {
        var dbPath = string.IsNullOrWhiteSpace(settings.Current.DatabasePath)
            ? "dy_downloader.db"
            : settings.Current.DatabasePath;
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(paths.DataDir, dbPath);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connStr = $"Data Source={dbPath}";
    }

    public void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS aweme (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    aweme_id TEXT UNIQUE NOT NULL,
                    aweme_type TEXT NOT NULL,
                    title TEXT,
                    author_id TEXT,
                    author_name TEXT,
                    sec_uid TEXT,
                    create_time INTEGER,
                    download_time INTEGER,
                    file_path TEXT,
                    mode TEXT,
                    metadata TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_aweme_author ON aweme(author_name);
                CREATE INDEX IF NOT EXISTS idx_aweme_mode ON aweme(mode);
                CREATE INDEX IF NOT EXISTS idx_aweme_download_time ON aweme(download_time);";
            cmd.ExecuteNonQuery();
            _initialized = true;
        }
    }

    public bool Exists(string awemeId)
    {
        Initialize();
        using var conn = new SqliteConnection(_connStr); conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM aweme WHERE aweme_id=$id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", awemeId);
        return cmd.ExecuteScalar() != null;
    }

    public void Upsert(AwemeRecord r)
    {
        Initialize();
        using var conn = new SqliteConnection(_connStr); conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO aweme (aweme_id, aweme_type, title, author_id, author_name, sec_uid, create_time, download_time, file_path, mode, metadata)
            VALUES ($id,$type,$title,$aid,$aname,$sec,$ct,$dt,$fp,$mode,$meta)
            ON CONFLICT(aweme_id) DO UPDATE SET download_time=$dt, file_path=$fp";
        cmd.Parameters.AddWithValue("$id", r.AwemeId);
        cmd.Parameters.AddWithValue("$type", r.AwemeType);
        cmd.Parameters.AddWithValue("$title", r.Title ?? "");
        cmd.Parameters.AddWithValue("$aid", r.AuthorId ?? "");
        cmd.Parameters.AddWithValue("$aname", r.AuthorName ?? "");
        cmd.Parameters.AddWithValue("$sec", r.SecUid ?? "");
        cmd.Parameters.AddWithValue("$ct", r.CreateTime);
        cmd.Parameters.AddWithValue("$dt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$fp", r.FilePath ?? "");
        cmd.Parameters.AddWithValue("$mode", r.Mode ?? "");
        cmd.Parameters.AddWithValue("$meta", r.Metadata ?? "");
        cmd.ExecuteNonQuery();
    }

    public long? LatestAwemeTime(string authorName)
    {
        Initialize();
        using var conn = new SqliteConnection(_connStr); conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(create_time) FROM aweme WHERE author_name=$aname";
        cmd.Parameters.AddWithValue("$aname", authorName);
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? null : Convert.ToInt64(v);
    }

    public IEnumerable<HistoryRow> ListHistory(int limit = 200)
    {
        Initialize();
        using var conn = new SqliteConnection(_connStr); conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT aweme_id,title,author_name,download_time,file_path,mode FROM aweme
                            ORDER BY download_time DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            yield return new HistoryRow
            {
                AwemeId = rdr.GetString(0),
                Title = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                AuthorName = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                DownloadTime = rdr.IsDBNull(3) ? 0 : rdr.GetInt64(3),
                FilePath = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                Mode = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
            };
        }
    }
}

public sealed class AwemeRecord
{
    public string AwemeId { get; set; } = "";
    public string AwemeType { get; set; } = "video";
    public string? Title { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? SecUid { get; set; }
    public long CreateTime { get; set; }
    public string? FilePath { get; set; }
    public string? Mode { get; set; }
    public string? Metadata { get; set; }
}

public sealed class HistoryRow
{
    public string AwemeId { get; set; } = "";
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public long DownloadTime { get; set; }
    public string FilePath { get; set; } = "";
    public string Mode { get; set; } = "";
}
