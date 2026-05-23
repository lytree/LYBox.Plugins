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
            _logger.Log($"开始处理链接: {link}");
            await DownloadMediaFromLink(client, link, includeComments, outputPath, ct);
        }

        _logger.Log("等待所有下载完成...");
        await Task.Delay(10000, ct);

        _logger.Log($"全部下载完毕！已下载文件数: {_downloadedFileIds.Count}");
    }

    async Task DownloadMediaFromLink(TdClient client, string link, bool includeComments, string outputPath, CancellationToken ct)
    {
        try
        {
            var linkInfo = await client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message == null)
            {
                _logger.Log($"无法从链接获取消息: {link}");
                return;
            }

            var chatId = linkInfo.Message.ChatId;
            var messageId = linkInfo.Message.Id;
            var message = linkInfo.Message;

            var chat = await client.GetChatAsync(chatId);
            _logger.Log($"开始下载 {chat.Title} 的媒体组...");
            _logger.Log($"包含评论: {includeComments}");

            int totalDownloaded = 0;

            if (message.MediaAlbumId != 0)
            {
                _logger.Log($"发现媒体组: {message.MediaAlbumId}");
                totalDownloaded += await DownloadMediaGroupByAlbumId(client, chatId, message.MediaAlbumId, messageId, outputPath, messageId, ct);
            }
            else
            {
                totalDownloaded += await DownloadMessageMedia(client, message, outputPath, messageId);
            }

            if (includeComments)
            {
                _logger.Log("开始下载评论区媒体...");
                var comments = await GetMessageCommentsAsync(client, chatId, messageId);
                _logger.Log($"找到 {comments.Length} 条评论");

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

                _logger.Log($"评论区下载完成，共 {commentsDownloaded} 个新文件，{commentsSkipped} 个已跳过");
                totalDownloaded += commentsDownloaded;
            }

            _logger.Log($"下载完成！共下载 {totalDownloaded} 个媒体文件");
        }
        catch (Exception ex)
        {
            _logger.Log($"下载过程中发生错误: {ex.Message}");
        }
    }

    async Task<int> DownloadMediaGroupByAlbumId(TdClient client, long chatId, long mediaAlbumId, long startMessageId, string outputPath, long messageId, CancellationToken ct)
    {
        int totalDownloaded = 0;

        try
        {
            _logger.Log($"开始下载媒体组 {mediaAlbumId}");

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
                _logger.Log($"获取初始消息失败: {ex.Message}");
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
                    _logger.Log($"搜索媒体组消息失败: {ex.Message}");
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
                _logger.Log($"搜索后续媒体组消息失败: {ex.Message}");
            }

            _logger.Log($"媒体组搜索完成，共找到 {foundMessages.Count} 条消息");

            foreach (var msg in foundMessages.OrderBy(m => m.Id))
            {
                ct.ThrowIfCancellationRequested();
                var count = await DownloadMessageMedia(client, msg, outputPath, messageId);
                totalDownloaded += count;
            }

            _logger.Log($"媒体组 {mediaAlbumId} 下载完成，共 {totalDownloaded} 个文件");
        }
        catch (TdException ex)
        {
            _logger.Log($"下载媒体组失败: {ex.Message}");
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
            _logger.Log($"队列下载: FileId: {fileId}, LinkId: {messageId}, MediaAlbumId: {message.MediaAlbumId}");
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
            _logger.Log($"获取评论失败: {ex.Error.Message}");
            return [];
        }
    }
}
