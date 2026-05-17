using Microsoft.Extensions.Logging;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    private readonly HashSet<int> _downloadedFileIds = [];
    private readonly Dictionary<int, long> _fileIdToAlbumId = new();

    public async Task GroupMediaDownloadAsync(string linksRaw, string? outputPath, bool includeComments, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        }
        Directory.CreateDirectory(outputPath);

        var links = linksRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var link in links)
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("开始处理链接: {Link}", link);
            await DownloadMediaFromLink(client, link, includeComments, outputPath, ct);
        }

        _logger.LogInformation("等待所有下载完成...");
        await Task.Delay(10000, ct);

        _logger.LogInformation("全部下载完毕！已下载文件数: {Count}", _downloadedFileIds.Count);
    }

    async Task DownloadMediaFromLink(TdClient client, string link, bool includeComments, string outputPath, CancellationToken ct)
    {
        try
        {
            var linkInfo = await client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message == null)
            {
                _logger.LogError("无法从链接获取消息: {Link}", link);
                return;
            }

            var chatId = linkInfo.Message.ChatId;
            var messageId = linkInfo.Message.Id;
            var message = linkInfo.Message;

            var chat = await client.GetChatAsync(chatId);
            _logger.LogInformation("开始下载 {Title} 的媒体组...", chat.Title);
            _logger.LogInformation("包含评论: {IncludeComments}", includeComments);

            int totalDownloaded = 0;

            if (message.MediaAlbumId != 0)
            {
                _logger.LogInformation("发现媒体组: {AlbumId}", message.MediaAlbumId);
                totalDownloaded += await DownloadMediaGroupByAlbumId(client, chatId, message.MediaAlbumId, messageId, outputPath, messageId, ct);
            }
            else
            {
                totalDownloaded += await DownloadMessageMedia(client, message, outputPath, messageId);
            }

            if (includeComments)
            {
                _logger.LogInformation("开始下载评论区媒体...");
                var comments = await GetMessageCommentsAsync(client, chatId, messageId);
                _logger.LogInformation("找到 {Count} 条评论", comments.Length);

                int commentsDownloaded = 0;
                int commentsSkipped = 0;
                foreach (var comment in comments)
                {
                    ct.ThrowIfCancellationRequested();
                    var fileId = GetFileIdFromMessage(comment);
                    if (fileId > 0)
                    {
                        if (_downloadedFileIds.Contains(fileId))
                        {
                            commentsSkipped++;
                        }
                        else
                        {
                            commentsDownloaded += await DownloadMessageMedia(client, comment, outputPath, messageId);
                        }
                    }
                }

                _logger.LogInformation("评论区下载完成，共 {Downloaded} 个新文件，{Skipped} 个已跳过", commentsDownloaded, commentsSkipped);
                totalDownloaded += commentsDownloaded;
            }

            _logger.LogInformation("下载完成！共下载 {Count} 个媒体文件", totalDownloaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载过程中发生错误");
        }
    }

    async Task<int> DownloadMediaGroupByAlbumId(TdClient client, long chatId, long mediaAlbumId, long startMessageId, string outputPath, long messageId, CancellationToken ct)
    {
        int totalDownloaded = 0;

        try
        {
            _logger.LogInformation("开始下载媒体组 {AlbumId}", mediaAlbumId);

            var foundMessages = new List<TdApi.Message>();

            try
            {
                var firstMessage = await client.GetMessageAsync(chatId, startMessageId);
                if (firstMessage != null && !foundMessages.Any(m => m.Id == firstMessage.Id))
                {
                    foundMessages.Add(firstMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取初始消息失败");
            }

            long searchBackwardId = startMessageId;
            int backwardAttempts = 0;
            while (backwardAttempts < 5)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var messages = await client.GetChatHistoryAsync(chatId, searchBackwardId, 0, 50, false);
                    if (messages.Messages_ == null || messages.Messages_.Length == 0) break;

                    bool foundMore = false;
                    foreach (var msg in messages.Messages_)
                    {
                        if (msg.MediaAlbumId == mediaAlbumId && !foundMessages.Any(m => m.Id == msg.Id))
                        {
                            foundMessages.Add(msg);
                            foundMore = true;
                        }
                        searchBackwardId = msg.Id;
                    }

                    if (!foundMore) backwardAttempts++;
                    else backwardAttempts = 0;

                    await Task.Delay(100, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "搜索媒体组消息失败");
                    break;
                }
            }

            try
            {
                var initialMessages = await client.GetChatHistoryAsync(chatId, startMessageId, -20, 40, false);
                if (initialMessages.Messages_ != null)
                {
                    foreach (var msg in initialMessages.Messages_)
                    {
                        if (msg.MediaAlbumId == mediaAlbumId && !foundMessages.Any(m => m.Id == msg.Id))
                        {
                            foundMessages.Add(msg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("搜索后续媒体组消息失败: {Message}", ex.Message);
            }

            _logger.LogInformation("媒体组搜索完成，共找到 {Count} 条消息", foundMessages.Count);

            foreach (var msg in foundMessages.OrderBy(m => m.Id))
            {
                ct.ThrowIfCancellationRequested();
                var count = await DownloadMessageMedia(client, msg, outputPath, messageId);
                totalDownloaded += count;
            }

            _logger.LogInformation("媒体组 {AlbumId} 下载完成，共 {Count} 个文件", mediaAlbumId, totalDownloaded);
        }
        catch (TdException ex)
        {
            _logger.LogWarning(ex, "下载媒体组失败");
        }

        return totalDownloaded;
    }

    async Task<int> DownloadMessageMedia(TdClient client, TdApi.Message message, string outputPath, long messageId)
    {
        int fileId = GetFileIdFromMessage(message);
        int downloadedCount = 0;

        if (fileId > 0 && !_downloadedFileIds.Contains(fileId))
        {
            _downloadedFileIds.Add(fileId);
            _fileIdToAlbumId[fileId] = messageId;
            await client.DownloadFileAsync(fileId, 32, 0, 0, true);
            downloadedCount++;
            _logger.LogInformation("队列下载: FileId: {FileId}, LinkId: {LinkId}, MediaAlbumId: {AlbumId}",
                fileId, messageId, message.MediaAlbumId);
        }

        return downloadedCount;
    }

    int GetFileIdFromMessage(TdApi.Message message)
    {
        return message.Content switch
        {
            TdApi.MessageContent.MessageDocument d => d.Document.Document_.Id,
            TdApi.MessageContent.MessageVideo v => v.Video.Video_.Id,
            TdApi.MessageContent.MessagePhoto p => p.Photo.Sizes.LastOrDefault()?.Photo.Id ?? 0,
            TdApi.MessageContent.MessageAudio a => a.Audio.Audio_.Id,
            TdApi.MessageContent.MessageAnimation ani => ani.Animation.Animation_.Id,
            TdApi.MessageContent.MessageVideoNote vn => vn.VideoNote.Video.Id,
            TdApi.MessageContent.MessageVoiceNote vce => vce.VoiceNote.Voice.Id,
            _ => 0
        };
    }

    async Task<TdApi.Message[]> GetMessageCommentsAsync(TdClient client, long chatId, long messageId)
    {
        try
        {
            var comments = await client.GetMessageThreadHistoryAsync(
                chatId: chatId,
                messageId: messageId,
                fromMessageId: 0,
                offset: 0,
                limit: 50
            );
            return comments.Messages_ ?? [];
        }
        catch (TdException ex)
        {
            _logger.LogWarning("获取评论失败: {Message}", ex.Error.Message);
            return [];
        }
    }
}
