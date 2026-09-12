namespace LYBox.Plugin.DouyinDownloader.Models;

/// <summary>URL 类型分类（与原项目 url_parser.py 对齐）。</summary>
public enum UrlKind
{
    Unknown,
    Video,
    Note,        // 图文 (note/gallery/slides)
    User,        // 用户主页 / 当前登录账号
    Collection,  // 合集 mix
    Music,
    Live,
    LiveReplay,
    Short,       // 短链 (未解析前)
}

public enum DownloadMode
{
    Post,
    Like,
    Mix,
    Music,
    Collect,     // 当前登录账号收藏 (aweme)
    CollectMix,  // 当前登录账号收藏合集
}

/// <summary>URL 解析结果。</summary>
public sealed class ParsedUrl
{
    public required UrlKind Kind { get; init; }
    public required string OriginalUrl { get; init; }
    public string? AwemeId { get; init; }
    public string? SecUid { get; init; }
    public string? MixId { get; init; }
    public string? NoteId { get; init; }
    public string? MusicId { get; init; }
    public string? RoomId { get; init; }
    public string? RoomIdKind { get; init; }
    public string? SecUserId { get; init; }
    public string? EpisodeId { get; init; }
    public string? ReplayId { get; init; }
}

/// <summary>统一下载任务描述（页面/队列/数据库 通用）。</summary>
public sealed class DownloadJobSpec
{
    public required Guid JobId { get; init; } = Guid.NewGuid();
    public required string OriginalUrl { get; init; }
    public required ParsedUrl Parsed { get; init; }
    public DownloadMode Mode { get; init; } = DownloadMode.Post;
    public int Number { get; init; }       // 0 = 不限
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public string? AuthorName { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

public enum JobStatus
{
    Queued,
    Running,
    Success,
    Failed,
    Skipped,
    Cancelled,
    Paused,
}

public sealed class DownloadJobRow
{
    public Guid JobId { get; init; }
    public string Url { get; init; } = "";
    public UrlKind UrlKind { get; set; }
    public DownloadMode Mode { get; set; }
    public int Number { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public string StatusText { get; set; } = "就绪";
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? AuthorName { get; set; }
    public string OutputDir { get; set; } = "";
    public long BytesDownloaded { get; set; }
    public double ElapsedSeconds { get; set; }
    public double SpeedBytesPerSec { get; set; }
}

/// <summary>作品详情统一结构（API/UI 共用）。</summary>
public sealed class AwemeDetail
{
    public string AwemeId { get; set; } = "";
    public string AwemeType { get; set; } = "";   // video / image
    public string Title { get; set; } = "";
    public string Desc { get; set; } = "";
    public long CreateTime { get; set; }          // unix seconds
    public string AuthorId { get; set; } = "";
    public string AuthorSecUid { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorAvatar { get; set; } = "";
    public List<string> CoverUrls { get; set; } = new();
    public List<string> VideoUrls { get; set; } = new();      // 全部清晰度,带水印
    public List<string> NoWatermarkUrls { get; set; } = new();
    public List<ImageAsset> Images { get; set; } = new();
    public MusicAsset? Music { get; set; }
    public long BitRate { get; set; }              // 已选最高码率
    public string BestVideoUrl { get; set; } = "";
    public string BestNoWatermarkUrl { get; set; } = "";
}

public sealed class ImageAsset
{
    public List<string> NoWatermarkUrls { get; set; } = new();   // origin/display
    public List<string> WatermarkUrls { get; set; } = new();     // download_url/owner_watermark
    public string BestUrl => NoWatermarkUrls.FirstOrDefault() ?? WatermarkUrls.FirstOrDefault() ?? "";
}

public sealed class MusicAsset
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string PlayUrl { get; set; } = "";
    public string CoverUrl { get; set; } = "";
}

/// <summary>用户主页批量结果单条。</summary>
public sealed class AwemeListItem
{
    public string AwemeId { get; set; } = "";
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorSecUid { get; set; } = "";
    public long CreateTime { get; set; }
}

/// <summary>分页响应。</summary>
public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public bool HasMore { get; set; }
    public long MaxCursor { get; set; }
    public int StatusCode { get; set; }
}
