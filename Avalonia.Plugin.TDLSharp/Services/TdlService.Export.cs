using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task ExportMessagesAsync(string channelLink, string? outputPath, bool exportComments, int limit, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;

        long chatId = await ResolveChatIdAsync(channelLink);
        if (chatId == 0)
        {
            _logger.Log($"无法解析频道: {channelLink}");
            return;
        }

        var chat = await client.GetChatAsync(chatId);
        _logger.Log($"目标: [{chat.Title}] ChatId={chatId}");

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tdl", "message");
            Directory.CreateDirectory(saveDir);
            outputPath = Path.Combine(saveDir, $"{chatId}.json");
        }

        var exportResult = await ExportChannelMessages(client, chatId, exportComments, limit, ct);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = JsonSerializer.Serialize(exportResult, jsonOptions);

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(outputPath, json);
        _logger.Log($"导出完成，共 {exportResult.TotalMessages} 条消息，{exportResult.Groups.Count} 个分组");
        _logger.Log($"文件已保存到: {outputPath}");
    }

    async Task<ChannelExport> ExportChannelMessages(TdClient client, long chatId, bool exportComments, int limit, CancellationToken ct)
    {
        long fromMessageId = 0;
        bool hasMore = true;
        var allMessages = new List<TdApi.Message>();
        int totalCount = 0;

        _logger.Log("开始导出频道消息...");

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var history = await client.GetChatHistoryAsync(chatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                allMessages.AddRange(history.Messages_);
                totalCount += history.Messages_.Length;

                fromMessageId = history.Messages_.Last().Id;
                _logger.Log($"已拉取 {totalCount} 条消息，当前进度 ID: {fromMessageId}");

                if (limit > 0 && totalCount >= limit)
                {
                    hasMore = false;
                }

                await Task.Delay(300, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000, ct);
            }
            catch (Exception ex)
            {
                _logger.Log($"拉取消息时发生异常: {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }

        if (limit > 0 && allMessages.Count > limit)
        {
            allMessages = allMessages.Take(limit).ToList();
        }

        var chat = await client.GetChatAsync(chatId);
        var export = new ChannelExport
        {
            ChatId = chatId,
            ChatTitle = chat.Title,
            ExportTime = DateTime.UtcNow,
            TotalMessages = allMessages.Count
        };

        var groups = GroupMessagesByAlbum(allMessages);

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            var exportGroup = new MessageGroup
            {
                MediaAlbumId = group.First().MediaAlbumId != 0 ? group.First().MediaAlbumId.ToString() : null,
                IsGrouped = group.Count > 1 && group.First().MediaAlbumId != 0
            };

            foreach (var msg in group)
            {
                var msgInfo = BuildMessageInfo(msg);

                if (exportComments)
                {
                    try
                    {
                        var comments = await client.GetMessageThreadHistoryAsync(
                            chatId: chatId,
                            messageId: msg.Id,
                            fromMessageId: 0,
                            offset: 0,
                            limit: 50
                        );

                        if (comments.Messages_ != null && comments.Messages_.Length > 0)
                        {
                            msgInfo.Comments = comments.Messages_.Select(BuildMessageInfo).ToList();
                        }
                    }
                    catch (TdException ex)
                    {
                        _logger.Log($"获取评论失败: MsgId={msg.Id}, 错误: {ex.Error.Message}");
                    }

                    await Task.Delay(200, ct);
                }

                exportGroup.Messages.Add(msgInfo);
            }

            export.Groups.Add(exportGroup);
            _logger.Log($"已处理分组 {export.Groups.Count}/{groups.Count} (消息数: {group.Count})");
        }

        return export;
    }

    MessageInfo BuildMessageInfo(TdApi.Message msg)
    {
        var info = new MessageInfo
        {
            MessageId = msg.Id,
            Date = DateTimeOffset.FromUnixTimeSeconds(msg.Date).DateTime,
            EditDate = msg.EditDate != 0 ? DateTimeOffset.FromUnixTimeSeconds(msg.EditDate).DateTime : null,
            Type = GetMessageType(msg.Content),
            Text = GetExportText(msg.Content),
            Media = GetMediaInfo(msg.Content),
            ForwardInfo = msg.ForwardInfo != null ? new ForwardInfoExport
            {
                FromChatId = msg.ForwardInfo.Source?.ChatId ?? 0,
                FromMessageId = msg.ForwardInfo.Source?.MessageId ?? 0,
                Date = msg.ForwardInfo.Date != 0 ? DateTimeOffset.FromUnixTimeSeconds(msg.ForwardInfo.Date).DateTime : null,
                Origin = msg.ForwardInfo.Origin switch
                {
                    TdApi.MessageOrigin.MessageOriginUser ou => $"User:{ou.SenderUserId}",
                    TdApi.MessageOrigin.MessageOriginChannel oc => $"Channel:{oc.ChatId}:{oc.MessageId}",
                    TdApi.MessageOrigin.MessageOriginHiddenUser ohu => $"Hidden:{ohu.SenderName}",
                    TdApi.MessageOrigin.MessageOriginChat oc => $"Chat:{oc.SenderChatId}",
                    _ => null
                }
            } : null
        };

        return info;
    }

    MediaInfo? GetMediaInfo(TdApi.MessageContent content)
    {
        return content switch
        {
            TdApi.MessageContent.MessagePhoto p => new MediaInfo
            {
                Type = "Photo",
                FileId = p.Photo.Sizes.LastOrDefault()?.Photo.Id.ToString(),
                Width = p.Photo.Sizes.LastOrDefault()?.Width,
                Height = p.Photo.Sizes.LastOrDefault()?.Height,
                FileSize = p.Photo.Sizes.LastOrDefault()?.Photo.ExpectedSize
            },
            TdApi.MessageContent.MessageVideo v => new MediaInfo
            {
                Type = "Video",
                FileId = v.Video.Video_.Id.ToString(),
                FileName = v.Video.FileName,
                Width = v.Video.Width,
                Height = v.Video.Height,
                Duration = v.Video.Duration,
                MimeType = v.Video.MimeType,
                FileSize = v.Video.Video_.ExpectedSize
            },
            TdApi.MessageContent.MessageAudio a => new MediaInfo
            {
                Type = "Audio",
                FileId = a.Audio.Audio_.Id.ToString(),
                FileName = a.Audio.FileName,
                Duration = a.Audio.Duration,
                MimeType = a.Audio.MimeType,
                FileSize = a.Audio.Audio_.ExpectedSize
            },
            TdApi.MessageContent.MessageDocument d => new MediaInfo
            {
                Type = "Document",
                FileId = d.Document.Document_.Id.ToString(),
                FileName = d.Document.FileName,
                MimeType = d.Document.MimeType,
                FileSize = d.Document.Document_.ExpectedSize
            },
            TdApi.MessageContent.MessageVoiceNote vn => new MediaInfo
            {
                Type = "VoiceNote",
                FileId = vn.VoiceNote.Voice.Id.ToString(),
                Duration = vn.VoiceNote.Duration,
                MimeType = vn.VoiceNote.MimeType,
                FileSize = vn.VoiceNote.Voice.ExpectedSize
            },
            TdApi.MessageContent.MessageVideoNote vn => new MediaInfo
            {
                Type = "VideoNote",
                FileId = vn.VideoNote.Video.Id.ToString(),
                Duration = vn.VideoNote.Duration,
                FileSize = vn.VideoNote.Video.ExpectedSize
            },
            TdApi.MessageContent.MessageAnimation ani => new MediaInfo
            {
                Type = "Animation",
                FileId = ani.Animation.Animation_.Id.ToString(),
                FileName = ani.Animation.FileName,
                Width = ani.Animation.Width,
                Height = ani.Animation.Height,
                Duration = ani.Animation.Duration,
                MimeType = ani.Animation.MimeType,
                FileSize = ani.Animation.Animation_.ExpectedSize
            },
            TdApi.MessageContent.MessageSticker s => new MediaInfo
            {
                Type = "Sticker",
                FileId = s.Sticker.Sticker_.Id.ToString(),
                Width = s.Sticker.Width,
                Height = s.Sticker.Height,
                FileSize = s.Sticker.Sticker_.ExpectedSize
            },
            _ => null
        };
    }

    string GetMessageType(TdApi.MessageContent content)
    {
        return content switch
        {
            TdApi.MessageContent.MessageText => "Text",
            TdApi.MessageContent.MessagePhoto => "Photo",
            TdApi.MessageContent.MessageVideo => "Video",
            TdApi.MessageContent.MessageAudio => "Audio",
            TdApi.MessageContent.MessageDocument => "Document",
            TdApi.MessageContent.MessageVoiceNote => "VoiceNote",
            TdApi.MessageContent.MessageVideoNote => "VideoNote",
            TdApi.MessageContent.MessageSticker => "Sticker",
            TdApi.MessageContent.MessageAnimation => "Animation",
            TdApi.MessageContent.MessageContact => "Contact",
            TdApi.MessageContent.MessageLocation => "Location",
            TdApi.MessageContent.MessageVenue => "Venue",
            TdApi.MessageContent.MessagePoll => "Poll",
            TdApi.MessageContent.MessageDice => "Dice",
            TdApi.MessageContent.MessageGame => "Game",
            TdApi.MessageContent.MessageInvoice => "Invoice",
            TdApi.MessageContent.MessageCall => "Call",
            TdApi.MessageContent.MessagePinMessage => "PinMessage",
            TdApi.MessageContent.MessageStory => "Story",
            TdApi.MessageContent.MessageUnsupported => "Unsupported",
            _ => content.GetType().Name.Replace("Message", "")
        };
    }

    string? GetExportText(TdApi.MessageContent content)
    {
        return content switch
        {
            TdApi.MessageContent.MessageText t => t.Text?.Text,
            TdApi.MessageContent.MessagePhoto p => p.Caption?.Text,
            TdApi.MessageContent.MessageVideo v => v.Caption?.Text,
            TdApi.MessageContent.MessageAudio a => a.Caption?.Text,
            TdApi.MessageContent.MessageDocument d => d.Caption?.Text,
            TdApi.MessageContent.MessageVoiceNote vn => vn.Caption?.Text,
            TdApi.MessageContent.MessageAnimation ani => ani.Caption?.Text,
            _ => null
        };
    }
}

public class ChannelExport
{
    public long ChatId { get; set; }
    public string ChatTitle { get; set; } = string.Empty;
    public DateTime ExportTime { get; set; }
    public int TotalMessages { get; set; }
    public List<MessageGroup> Groups { get; set; } = [];
}

public class MessageGroup
{
    public string? MediaAlbumId { get; set; }
    public bool IsGrouped { get; set; }
    public List<MessageInfo> Messages { get; set; } = [];
}

public class MessageInfo
{
    public long MessageId { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EditDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public MediaInfo? Media { get; set; }
    public ForwardInfoExport? ForwardInfo { get; set; }
    public List<MessageInfo>? Comments { get; set; }
}

public class MediaInfo
{
    public string Type { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
}

public class ForwardInfoExport
{
    public long FromChatId { get; set; }
    public long FromMessageId { get; set; }
    public DateTime? Date { get; set; }
    public string? Origin { get; set; }
}
