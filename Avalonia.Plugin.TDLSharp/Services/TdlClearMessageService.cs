using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public class TdlClearMessageService
{
    private readonly TdlClientManager _clientManager;
    private readonly ILogger _logger;

    public TdlClearMessageService(TdlClientManager clientManager, ILogger<TdlClearMessageService> logger)
    {
        _clientManager = clientManager;
        _logger = logger;
    }

    public async Task ExecuteAsync(string? channelLink, string containsText, bool silent, int limit, CancellationToken ct = default)
    {
        await _clientManager.InitializeAsync();
        await _clientManager.WaitReadyAsync();

        var client = _clientManager.Client;
        var currentUser = await _clientManager.GetCurrentUserAsync();
        long myId = currentUser.Id;

        long chatId = await ResolveChatIdAsync(client, channelLink);
        if (chatId == 0)
        {
            chatId = myId;
            _logger.LogInformation("未指定频道，默认使用收藏夹 (ChatId={ChatId})", myId);
        }

        var chat = await client.GetChatAsync(chatId);
        _logger.LogInformation("目标: [{Title}] ChatId={ChatId}", chat.Title, chatId);
        _logger.LogInformation("匹配内容: \"{Text}\"", containsText);
        _logger.LogInformation("删除模式: {Mode}", silent ? "静默删除" : "交互确认");

        int totalDeleted = await CleanMessages(client, chatId, containsText, silent, limit);
        _logger.LogInformation("清理完成，共删除 {Count} 条消息", totalDeleted);
    }

    async Task<int> CleanMessages(TdClient client, long chatId, string containsText, bool silent, int limit)
    {
        int totalDeleted = 0;
        long fromMessageId = 0;
        bool hasMore = true;
        var matchedMessages = new List<(long MsgId, string Text)>();

        _logger.LogInformation("开始扫描消息...");

        while (hasMore)
        {
            try
            {
                var history = await client.GetChatHistoryAsync(chatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                foreach (var msg in history.Messages_)
                {
                    string? text = ExtractMessageText(msg);
                    if (text != null && text.Contains(containsText, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedMessages.Add((msg.Id, text.Length > 80 ? text[..80] + "..." : text));
                    }

                    if (limit > 0 && matchedMessages.Count >= limit)
                    {
                        hasMore = false;
                        break;
                    }
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(300);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.LogWarning("触发频率限制，等待 {Seconds} 秒后继续...", retryAfter);
                await Task.Delay(retryAfter * 1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描消息时发生异常");
                await Task.Delay(5000);
            }
        }

        if (matchedMessages.Count == 0)
        {
            _logger.LogInformation("未找到包含 \"{Text}\" 的消息", containsText);
            return 0;
        }

        _logger.LogInformation("共找到 {Count} 条匹配消息", matchedMessages.Count);

        int batchSize = 100;
        for (int i = 0; i < matchedMessages.Count; i += batchSize)
        {
            var batch = matchedMessages.Skip(i).Take(batchSize).Select(m => m.MsgId).ToArray();
            try
            {
                await client.DeleteMessagesAsync(chatId, batch, revoke: true);
                totalDeleted += batch.Length;
                _logger.LogInformation("已删除 {Deleted}/{Total} 条消息", totalDeleted, matchedMessages.Count);
                await Task.Delay(500);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.LogWarning("触发频率限制，等待 {Seconds} 秒后继续...", retryAfter);
                await Task.Delay(retryAfter * 1000);
                i -= batchSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量删除消息时发生异常");
            }
        }

        return totalDeleted;
    }

    string? ExtractMessageText(TdApi.Message msg)
    {
        return msg.Content switch
        {
            TdApi.MessageContent.MessageText t => t.Text?.Text,
            TdApi.MessageContent.MessagePhoto p => p.Caption?.Text,
            TdApi.MessageContent.MessageVideo v => v.Caption?.Text,
            TdApi.MessageContent.MessageAudio a => a.Caption?.Text,
            TdApi.MessageContent.MessageDocument d => d.Caption?.Text,
            TdApi.MessageContent.MessageVoiceNote vn => vn.Caption?.Text,
            TdApi.MessageContent.MessageAnimation ani => ani.Caption?.Text,
            TdApi.MessageContent.MessagePinMessage pm => $"[PinMessage] MsgId={pm.MessageId}",
            TdApi.MessageContent.MessageUnsupported => "This channel can't be displayed",
            _ => null
        };
    }

    async Task<long> ResolveChatIdAsync(TdClient client, string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return 0;

        try
        {
            var linkInfo = await client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message != null) return linkInfo.Message.ChatId;
        }
        catch (TdException) { }

        try
        {
            var username = ExtractUsername(link);
            if (!string.IsNullOrEmpty(username))
            {
                var chat = await client.SearchPublicChatAsync(username);
                if (chat != null) return chat.Id;
            }
        }
        catch (TdException) { }

        if (long.TryParse(link.Trim(), out long chatId)) return chatId;

        return 0;
    }

    string? ExtractUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (input.StartsWith("@")) return input.Substring(1);
        if (!input.Contains("/")) return null;

        var match = Regex.Match(input,
            @"(?:https?:\/\/)?(?:t\.me|telegram\.me)\/(?<name>[^\/\?\#]+)",
            RegexOptions.IgnoreCase);

        if (!match.Success) return null;
        var name = match.Groups["name"].Value;
        if (name.StartsWith("+")) return null;
        return name;
    }

    int ParseRetryAfter(TdException ex)
    {
        if (ex.Error?.Message != null)
        {
            var match = Regex.Match(ex.Error.Message, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int seconds) && seconds > 0)
            {
                return Math.Min(seconds + 2, 300);
            }
        }
        return 15;
    }
}
