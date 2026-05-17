using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public class TdlForwardService
{
    readonly TdClient _client;
    readonly ILogger _logger;
    readonly string _tdlRoot;
    readonly TdlUpdateHandler _updateHandler;
    readonly ManualResetEventSlim _readyToAuthenticate;
    readonly Dictionary<long, TaskCompletionSource<TdApi.Error?>> _pendingSends = new();
    readonly object _pendingLock = new();

    public bool AuthNeeded => _updateHandler.AuthNeeded;
    public bool PasswordNeeded => _updateHandler.PasswordNeeded;

    public TdlForwardService(TdClient client, ILogger logger, string tdlRoot)
    {
        _client = client;
        _logger = logger;
        _tdlRoot = tdlRoot;
        _readyToAuthenticate = new ManualResetEventSlim();

        _updateHandler = new TdlUpdateHandler(_readyToAuthenticate, logger)
            .OnConfigureTdlibParameters(ConfigureTdlibParameters)
            .OnFileUpdate(HandleFileUpdate)
            .OnMessageUpdate(HandleMessageUpdate);

        _client.UpdateReceived += async (_, update) => { await _updateHandler.ProcessUpdates(_client, update, _tdlRoot); };
    }

    public async Task WaitReadyAsync()
    {
        _readyToAuthenticate.Wait();
    }

    public async Task<(long chatId, long messageId)> ResolveSourceLinkAsync(string link)
    {
        try
        {
            var linkInfo = await _client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message != null)
            {
                return (linkInfo.Message.ChatId, linkInfo.Message.Id);
            }
            _logger.LogWarning("源链接未关联到消息: {Link}", link);
        }
        catch (TdException ex)
        {
            _logger.LogError(ex, "无法解析源链接: {Link}", link);
        }
        return (0, 0);
    }

    public async Task<long> ResolveTargetLinkAsync(string link)
    {
        try
        {
            var linkInfo = await _client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message != null)
            {
                return linkInfo.Message.ChatId;
            }
        }
        catch (TdException) { }

        try
        {
            if (IsInviteLink(link))
            {
                var inviteInfo = await _client.CheckChatInviteLinkAsync(link);
                if (inviteInfo.ChatId != 0)
                {
                    _logger.LogInformation("邀请链接已关联到 ChatId: {ChatId}", inviteInfo.ChatId);
                    return inviteInfo.ChatId;
                }
                _logger.LogWarning("邀请链接有效但未关联到已有聊天: {Link}", link);
                return 0;
            }
        }
        catch (TdException ex)
        {
            _logger.LogError(ex, "无法解析邀请链接: {Link}", link);
            return 0;
        }

        try
        {
            var username = ExtractUsername(link);
            if (!string.IsNullOrEmpty(username))
            {
                var chat = await _client.SearchPublicChatAsync(username);
                if (chat != null)
                {
                    return chat.Id;
                }
            }
        }
        catch (TdException) { }

        if (long.TryParse(link.Trim(), out long chatId))
        {
            return chatId;
        }

        try
        {
            var foundChatId = await SearchChatByTitleAsync(link);
            if (foundChatId != 0)
            {
                return foundChatId;
            }
        }
        catch (TdException) { }

        _logger.LogWarning("目标链接未关联到聊天: {Link}", link);
        return 0;
    }

    public async Task<long> ResolveChatIdAsync(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return 0;

        try
        {
            var linkInfo = await _client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message != null)
            {
                return linkInfo.Message.ChatId;
            }
        }
        catch (TdException) { }

        try
        {
            if (IsInviteLink(link))
            {
                var inviteInfo = await _client.CheckChatInviteLinkAsync(link);
                if (inviteInfo.ChatId != 0)
                {
                    _logger.LogInformation("邀请链接已关联到 ChatId: {ChatId}", inviteInfo.ChatId);
                    return inviteInfo.ChatId;
                }
                return 0;
            }
        }
        catch (TdException) { }

        try
        {
            var username = ExtractUsername(link);
            if (!string.IsNullOrEmpty(username))
            {
                var chat = await _client.SearchPublicChatAsync(username);
                if (chat != null)
                {
                    return chat.Id;
                }
            }
        }
        catch (TdException) { }

        if (long.TryParse(link.Trim(), out long chatId))
        {
            return chatId;
        }

        try
        {
            var chatIds = await _client.GetChatsAsync(limit: 200);
            if (chatIds?.ChatIds != null)
            {
                foreach (var id in chatIds.ChatIds)
                {
                    try
                    {
                        var chat = await _client.GetChatAsync(id);
                        if (chat.Title.Contains(link, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("找到匹配聊天: [{Title}] ChatId={ChatId}", chat.Title, chat.Id);
                            return chat.Id;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return 0;
    }

    public async Task<int> DeepCopyForward(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments)
    {
        int totalForwarded = 0;
        int totalSkipped = 0;
        long fromMessageId = startMessageId;
        List<TdApi.Message>? pendingGroup = null;
        bool hasMore = true;

        _logger.LogInformation("开始向旧消息方向转发...");

        while (hasMore)
        {
            try
            {
                var history = await _client.GetChatHistoryAsync(sourceChatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                var messages = history.Messages_
                    .Where(m => m.Id <= fromMessageId && m.ForwardInfo != null)
                    .OrderBy(m => m.Id)
                    .ToList();

                if (messages.Count == 0)
                {
                    fromMessageId = history.Messages_.Last().Id;
                    continue;
                }

                if (pendingGroup != null && pendingGroup.Count > 0)
                {
                    messages = [.. pendingGroup, .. messages];
                    pendingGroup = null;
                }

                var (toProcess, pending) = ExtractPendingMediaGroup(messages);
                if (pending != null && pending.Count > 0)
                {
                    pendingGroup = pending;
                }

                var (forwarded, skipped) = await ForwardGroupedMessages(db, toProcess, sourceChatId, targetChatId, forwardComments);
                totalForwarded += forwarded;
                totalSkipped += skipped;

                if (limit > 0 && totalForwarded >= limit)
                {
                    _logger.LogInformation("已达到转发限制 {Limit}", limit);
                    break;
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(1000);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.LogWarning("触发频率限制，等待 {Seconds} 秒后继续...", retryAfter);
                await Task.Delay(retryAfter * 1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转发过程中发生异常");
                await Task.Delay(5000);
            }
        }

        if (pendingGroup != null && pendingGroup.Count > 0)
        {
            var (forwarded, skipped) = await ForwardGroupedMessages(db, pendingGroup, sourceChatId, targetChatId, forwardComments);
            totalForwarded += forwarded;
            totalSkipped += skipped;
        }

        if (totalSkipped > 0)
        {
            _logger.LogInformation("跳过已转发消息 {Count} 条", totalSkipped);
        }

        return totalForwarded;
    }

    public async Task<int> ForwardOlderDirection(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments)
    {
        int totalForwarded = 0;
        int totalSkipped = 0;
        long fromMessageId = startMessageId;
        List<TdApi.Message>? pendingGroup = null;
        bool hasMore = true;

        _logger.LogInformation("开始向旧消息方向转发...");

        while (hasMore)
        {
            try
            {
                var history = await _client.GetChatHistoryAsync(sourceChatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                var messages = history.Messages_
                    .Where(m => m.Id <= fromMessageId)
                    .OrderBy(m => m.Id)
                    .ToList();

                if (messages.Count == 0)
                {
                    fromMessageId = history.Messages_.Last().Id;
                    continue;
                }

                if (pendingGroup != null && pendingGroup.Count > 0)
                {
                    messages = [.. pendingGroup, .. messages];
                    pendingGroup = null;
                }

                var (toProcess, pending) = ExtractPendingMediaGroup(messages);
                if (pending != null && pending.Count > 0)
                {
                    pendingGroup = pending;
                }

                var (forwarded, skipped) = await ForwardGroupedMessages(db, toProcess, sourceChatId, targetChatId, forwardComments);
                totalForwarded += forwarded;
                totalSkipped += skipped;

                if (limit > 0 && totalForwarded >= limit)
                {
                    _logger.LogInformation("已达到转发限制 {Limit}", limit);
                    break;
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(1000);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.LogWarning("触发频率限制，等待 {Seconds} 秒后继续...", retryAfter);
                await Task.Delay(retryAfter * 1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转发过程中发生异常");
                await Task.Delay(5000);
            }
        }

        if (pendingGroup != null && pendingGroup.Count > 0)
        {
            var (forwarded, skipped) = await ForwardGroupedMessages(db, pendingGroup, sourceChatId, targetChatId, forwardComments);
            totalForwarded += forwarded;
            totalSkipped += skipped;
        }

        if (totalSkipped > 0)
        {
            _logger.LogInformation("跳过已转发消息 {Count} 条", totalSkipped);
        }

        return totalForwarded;
    }

    public async Task<int> ForwardNewerDirection(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments)
    {
        var newerMessages = new List<TdApi.Message>();
        long fromMessageId = 0;
        bool foundStart = false;

        _logger.LogInformation("开始向新消息方向转发（从最新消息往回搜索）...");

        while (!foundStart)
        {
            try
            {
                var history = await _client.GetChatHistoryAsync(sourceChatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    break;
                }

                foreach (var msg in history.Messages_)
                {
                    if (msg.Id >= startMessageId)
                    {
                        newerMessages.Add(msg);
                        if (limit > 0 && newerMessages.Count >= limit)
                        {
                            foundStart = true;
                            break;
                        }
                    }
                    else
                    {
                        foundStart = true;
                        break;
                    }
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索新消息时发生异常");
                break;
            }
        }

        newerMessages = newerMessages.OrderBy(m => m.Id).ToList();
        _logger.LogInformation("找到 {Count} 条消息，开始转发...", newerMessages.Count);

        var (totalForwarded, totalSkipped) = await ForwardGroupedMessages(db, newerMessages, sourceChatId, targetChatId, forwardComments);

        if (totalSkipped > 0)
        {
            _logger.LogInformation("跳过已转发消息 {Count} 条", totalSkipped);
        }

        return totalForwarded;
    }

    async Task ConfigureTdlibParameters(TdClient client, string outputPath, ILogger cbLogger)
    {
        await client.ExecuteAsync(new TdApi.SetTdlibParameters
        {
            ApiId = Convert.ToInt32(Environment.GetEnvironmentVariable("tdl_api_id", EnvironmentVariableTarget.User)),
            ApiHash = Environment.GetEnvironmentVariable("tdl_api_hash", EnvironmentVariableTarget.User),
            DeviceModel = "PC",
            SystemLanguageCode = "en",
            ApplicationVersion = "1.0.0",
            DatabaseDirectory = Path.Combine(_tdlRoot, "db"),
            FilesDirectory = Path.Combine(_tdlRoot, "files"),
            UseFileDatabase = true,
            UseChatInfoDatabase = true,
            UseMessageDatabase = true,
        });

        cbLogger.LogInformation("正在尝试连接代理...");
        var proxy = await client.AddProxyAsync(new TdApi.Proxy() { Server = "127.0.0.1", Port = 7897, Type = new TdApi.ProxyType.ProxyTypeSocks5() }, true);
        await client.EnableProxyAsync(proxy.Id);
        cbLogger.LogInformation("代理已启用。");
    }

    Task HandleFileUpdate(TdApi.File file, string outputPath, ILogger cbLogger)
    {
        if (file.Local.IsDownloadingCompleted)
        {
            cbLogger.LogInformation("文件下载完成！本地路径: {Path}", file.Local.Path);
        }
        return Task.CompletedTask;
    }

    Task HandleMessageUpdate(TdApi.Update update, ILogger cbLogger)
    {
        switch (update)
        {
            case TdApi.Update.UpdateMessageSendSucceeded umss:
                cbLogger.LogTrace("消息发送成功: MsgId={MsgId}", umss.Message.Id);
                RemovePendingSend(umss.Message.Id);
                break;
            case TdApi.Update.UpdateMessageSendFailed umsf:
                cbLogger.LogWarning("消息发送失败: MsgId={MsgId}, 错误: {Code} {Message}", umsf.Message.Id, umsf.Error.Code, umsf.Error.Message);
                NotifySendFailed(umsf.Message.Id, umsf.Error);
                break;
            case TdApi.Update.UpdateDeleteMessages udm:
                if (!udm.IsPermanent) break;
                cbLogger.LogTrace("消息永久删除: ChatId={ChatId}, 数量={Count}", udm.ChatId, udm.MessageIds.Length);
                break;
        }
        return Task.CompletedTask;
    }

    async Task<long> SearchChatByTitleAsync(string keyword)
    {
        _logger.LogInformation("在聊天列表中搜索: {Keyword}", keyword);
        var chatIds = await _client.GetChatsAsync(limit: 200);
        if (chatIds?.ChatIds == null) return 0;

        foreach (var id in chatIds.ChatIds)
        {
            try
            {
                var chat = await _client.GetChatAsync(id);
                if (chat.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("找到匹配聊天: [{Title}] ChatId={ChatId}", chat.Title, chat.Id);
                    return chat.Id;
                }
            }
            catch { }
        }

        return 0;
    }

    bool IsInviteLink(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();
        if (input.StartsWith("https://t.me/+", StringComparison.OrdinalIgnoreCase)) return true;
        if (input.StartsWith("https://t.me/joinchat/", StringComparison.OrdinalIgnoreCase)) return true;
        if (input.StartsWith("https://telegram.me/+", StringComparison.OrdinalIgnoreCase)) return true;
        if (input.StartsWith("https://telegram.me/joinchat/", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
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

    (List<TdApi.Message> toProcess, List<TdApi.Message>? pending) ExtractPendingMediaGroup(List<TdApi.Message> messages)
    {
        if (messages.Count == 0) return (messages, null);

        var lastMsg = messages[^1];
        if (lastMsg.MediaAlbumId == 0) return (messages, null);

        var pending = new List<TdApi.Message>();
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].MediaAlbumId == lastMsg.MediaAlbumId)
            {
                pending.Insert(0, messages[i]);
            }
            else
            {
                break;
            }
        }

        var toProcess = messages.Take(messages.Count - pending.Count).ToList();
        return (toProcess, pending);
    }

    async Task<(int forwarded, int skipped)> ForwardGroupedMessages(ForwardDbContext db, List<TdApi.Message> messages, long sourceChatId, long targetChatId, bool forwardComments)
    {
        if (messages.Count == 0) return (0, 0);

        int totalForwarded = 0;
        int totalSkipped = 0;
        var groups = GroupMessagesByAlbum(messages);

        foreach (var group in groups)
        {
            var (idsToForward, skippedIds) = await FilterAlreadyForwarded(db, sourceChatId, targetChatId, group);
            totalSkipped += skippedIds.Count;

            if (idsToForward.Count == 0)
            {
                continue;
            }

            int retryCount = 0;
            bool success = false;
            string? lastError = null;

            while (!success && retryCount < 5)
            {
                try
                {
                    var ids = idsToForward.OrderBy(id => id).ToArray();

                    var result = await _client.ForwardMessagesAsync(
                        chatId: targetChatId,
                        fromChatId: sourceChatId,
                        messageIds: ids,
                        sendCopy: true,
                        removeCaption: false
                    );

                    if (result.Messages_ != null)
                    {
                        foreach (var rMsg in result.Messages_)
                        {
                            RegisterPendingSend(rMsg.Id);
                        }
                    }

                    var sendError = await WaitForSendResultAsync(
                        result.Messages_?.Select(m => m.Id).ToArray() ?? [], 10);

                    if (sendError != null)
                    {
                        if (sendError.Code == 429 || (sendError.Message?.Contains("Too Many Requests") ?? false))
                        {
                            int retryAfter = ParseRetryAfterFromError(sendError);
                            retryCount++;
                            _logger.LogWarning("异步发送触发频率限制 (第{Retry}次)，等待 {Seconds} 秒后重试...", retryCount, retryAfter);
                            await Task.Delay(retryAfter * 1000);
                            continue;
                        }

                        lastError = $"{sendError.Code}: {sendError.Message}";
                        retryCount++;
                        _logger.LogError("消息异步发送失败 (第{Retry}次重试): {Error}", retryCount, lastError);
                        await Task.Delay(5000);
                        continue;
                    }
                    await Task.Delay(1000);
                    var forwardedMessages = group.Where(m => idsToForward.Contains(m.Id)).ToList();
                    await RecordForwardedMessages(db, sourceChatId, targetChatId, forwardedMessages, isSuccess: true);

                    totalForwarded += ids.Length;
                    var albumLabel = group.First().MediaAlbumId != 0 ? $"分组:{group.First().MediaAlbumId}" : $"独立消息 {group.First().Id}";
                    _logger.LogInformation("已转发 {Total} 条消息 ({Label}, 数量: {Count})", totalForwarded, albumLabel, ids.Length);

                    if (forwardComments && result.Messages_ != null)
                    {
                        await ForwardCommentsForMessages(db, sourceChatId, targetChatId, forwardedMessages, result.Messages_);
                    }

                    await Task.Delay(1000);
                    success = true;
                }
                catch (TdException ex) when (ex.Error.Code == 429)
                {
                    int retryAfter = ParseRetryAfter(ex);
                    retryCount++;
                    _logger.LogWarning("触发频率限制 (第{Retry}次)，等待 {Seconds} 秒后重试...", retryCount, retryAfter);
                    await Task.Delay(retryAfter * 1000);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    retryCount++;
                    _logger.LogError(ex, "转发消息组时出错 (第{Retry}次重试)", retryCount);
                    await Task.Delay(5000);
                }
            }

            if (!success)
            {
                var failedMessages = group.Where(m => idsToForward.Contains(m.Id)).ToList();
                await RecordForwardedMessages(db, sourceChatId, targetChatId, failedMessages, isSuccess: false, error: lastError);
                _logger.LogError("消息组转发失败，已跳过 (MediaAlbumId: {AlbumId})", group.First().MediaAlbumId);
            }
        }

        return (totalForwarded, totalSkipped);
    }

    async Task ForwardCommentsForMessages(ForwardDbContext db, long sourceChatId, long targetChatId, List<TdApi.Message> sourceMessages, TdApi.Message[] forwardedMessages)
    {
        for (int i = 0; i < sourceMessages.Count; i++)
        {
            var sourceMsg = sourceMessages[i];
            TdApi.Message? forwardedMsg = i < forwardedMessages.Length ? forwardedMessages[i] : null;

            if (forwardedMsg == null) continue;
            try
            {
                var replyInfo = sourceMsg.InteractionInfo?.ReplyInfo;
                if (replyInfo == null || replyInfo.ReplyCount == 0) continue;

                var allComments = new List<TdApi.Message>();
                long fromMsgId = 0;
                bool hasMore = true;

                while (hasMore)
                {
                    var page = await _client.GetMessageThreadHistoryAsync(
                        chatId: sourceChatId,
                        messageId: sourceMsg.Id,
                        fromMessageId: fromMsgId,
                        offset: 0,
                        limit: 100
                    );

                    if (page.Messages_ == null || page.Messages_.Length == 0)
                    {
                        hasMore = false;
                        break;
                    }

                    allComments.AddRange(page.Messages_);

                    if (page.Messages_.Length < 100)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        fromMsgId = page.Messages_.Min(m => m.Id);
                        await Task.Delay(300);
                    }
                }
                if (allComments.Count == 0) continue;

                var commentList = allComments.OrderBy(m => m.Id).ToList();
                var groups = GroupMessagesByAlbum(commentList);
                _logger.LogInformation("转发评论: MsgId={MsgId}, 评论数={Count}, 分组数={GroupCount}", sourceMsg.Id, commentList.Count, groups.Count);

                foreach (var group in groups)
                {
                    var (idsToForward, skippedIds) = await FilterAlreadyForwarded(db, sourceChatId, targetChatId, group);

                    if (idsToForward.Count == 0) continue;

                    var groupIds = group.Select(m => m.Id).OrderBy(id => id).ToArray();
                    var sourceCommonChatId = group.Select(m => m.ChatId).OrderBy(id => id).First();
                    var albumLabel = group[0].MediaAlbumId != 0 ? $"分组:{group[0].MediaAlbumId}" : $"独立评论 {group[0].Id}";

                    var result = await _client.ForwardMessagesAsync(
                         chatId: targetChatId,
                         fromChatId: sourceCommonChatId,
                         messageIds: groupIds,
                         sendCopy: true,
                         removeCaption: false
                     );
                    if (result.Messages_ != null)
                    {
                        foreach (var rMsg in result.Messages_)
                        {
                            RegisterPendingSend(rMsg.Id);
                        }
                    }

                    var sendError = await WaitForSendResultAsync(
                        result.Messages_?.Select(m => m.Id).ToArray() ?? [], 10);

                    if (sendError != null)
                    {
                        if (sendError.Code == 429 || (sendError.Message?.Contains("Too Many Requests") ?? false))
                        {
                            int retryAfter = ParseRetryAfterFromError(sendError);
                            _logger.LogWarning("异步发送触发频率限制，等待 {Seconds} 秒后重试...", retryAfter);
                            await Task.Delay(retryAfter * 1000);
                            continue;
                        }

                        _logger.LogError("消息异步发送失败: {Code}: {Message}", sendError.Code, sendError.Message);
                        await Task.Delay(5000);
                        continue;
                    }
                    await Task.Delay(1000);
                    var forwardedCommentsMessages = group.Where(m => groupIds.Contains(m.Id)).ToList();
                    await RecordForwardedMessages(db, sourceChatId, targetChatId, forwardedCommentsMessages, isSuccess: true);
                    _logger.LogInformation("已转发评论 {Label}, 数量: {Count}", albumLabel, groupIds.Length);
                    await Task.Delay(5000);
                }
            }
            catch (TdException ex)
            {
                _logger.LogWarning("转发评论失败: MsgId={MsgId}, 错误: {Error}", sourceMsg.Id, ex.Error.Message);
            }
        }
    }

    async Task<(List<long> toForward, List<long> skipped)> FilterAlreadyForwarded(ForwardDbContext db, long sourceChatId, long targetChatId, List<TdApi.Message> group)
    {
        var allIds = group.Select(m => m.Id).ToList();
        var existingRecords = await db.ForwardRecords
            .Where(r => r.SourceChatId == sourceChatId && r.TargetChatId == targetChatId && allIds.Contains(r.MessageId))
            .Select(r => r.MessageId)
            .ToListAsync();

        var skipped = existingRecords;
        var toForward = allIds.Where(id => !existingRecords.Contains(id)).ToList();

        if (skipped.Count > 0)
        {
            var albumLabel = group.First().MediaAlbumId != 0 ? $"分组:{group.First().MediaAlbumId}" : $"MsgId:{group.First().Id}";
            _logger.LogInformation("跳过已转发 {Count} 条消息 ({Label}), 待转发 {ForwardCount} 条", skipped.Count, albumLabel, toForward.Count);
        }

        return (toForward, skipped);
    }

    async Task RecordForwardedMessages(ForwardDbContext db, long sourceChatId, long targetChatId, List<TdApi.Message> messages, bool isSuccess, string? error = null)
    {
        foreach (var msg in messages)
        {
            var existing = await db.ForwardRecords.FindAsync(msg.Id);
            if (existing != null)
            {
                existing.IsSuccess = isSuccess;
                existing.ExtraData = ForwardRecord.BuildExtraData(msg, error);
                existing.ForwardedAt = DateTime.UtcNow;
            }
            else
            {
                db.ForwardRecords.Add(new ForwardRecord
                {
                    MessageId = msg.Id,
                    SourceChatId = sourceChatId,
                    TargetChatId = targetChatId,
                    MediaAlbumId = msg.MediaAlbumId,
                    IsSuccess = isSuccess,
                    ForwardedAt = DateTime.UtcNow,
                    ExtraData = ForwardRecord.BuildExtraData(msg, error)
                });
            }
        }

        await db.SaveChangesAsync();
    }

    int ParseRetryAfter(TdException ex)
    {
        if (ex.Error?.Message != null)
        {
            return ParseRetryAfterFromMessage(ex.Error.Message);
        }
        return 15;
    }

    int ParseRetryAfterFromError(TdApi.Error error)
    {
        if (error?.Message != null)
        {
            return ParseRetryAfterFromMessage(error.Message);
        }
        return 15;
    }

    int ParseRetryAfterFromMessage(string message)
    {
        var match = Regex.Match(message, @"(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int seconds) && seconds > 0)
        {
            return Math.Min(seconds + 2, 300);
        }
        return 15;
    }

    List<List<TdApi.Message>> GroupMessagesByAlbum(List<TdApi.Message> messages)
    {
        var result = new List<List<TdApi.Message>>();
        if (messages.Count == 0) return result;

        var currentGroup = new List<TdApi.Message> { messages[0] };
        long currentAlbumId = messages[0].MediaAlbumId;

        for (int i = 1; i < messages.Count; i++)
        {
            if (messages[i].MediaAlbumId != 0 && messages[i].MediaAlbumId == currentAlbumId)
            {
                currentGroup.Add(messages[i]);
            }
            else
            {
                result.Add(currentGroup);
                currentGroup = [messages[i]];
                currentAlbumId = messages[i].MediaAlbumId;
            }
        }

        result.Add(currentGroup);
        return result;
    }

    void RegisterPendingSend(long messageId)
    {
        lock (_pendingLock)
        {
            if (!_pendingSends.ContainsKey(messageId))
            {
                _pendingSends[messageId] = new TaskCompletionSource<TdApi.Error?>();
            }
        }
    }

    void RemovePendingSend(long messageId)
    {
        lock (_pendingLock)
        {
            if (_pendingSends.TryGetValue(messageId, out var tcs))
            {
                tcs.TrySetResult(null);
                _pendingSends.Remove(messageId);
            }
        }
    }

    void NotifySendFailed(long messageId, TdApi.Error error)
    {
        lock (_pendingLock)
        {
            if (_pendingSends.TryGetValue(messageId, out var tcs))
            {
                tcs.TrySetResult(error);
                _pendingSends.Remove(messageId);
            }
        }
    }

    async Task<TdApi.Error?> WaitForSendResultAsync(long[] messageIds, int timeoutSeconds = 3)
    {
        TaskCompletionSource<TdApi.Error?>[] tcsArray;
        lock (_pendingLock)
        {
            tcsArray = new TaskCompletionSource<TdApi.Error?>[messageIds.Length];
            for (int i = 0; i < messageIds.Length; i++)
            {
                if (!_pendingSends.TryGetValue(messageIds[i], out var tcs))
                {
                    _pendingSends[messageIds[i]] = new TaskCompletionSource<TdApi.Error?>();
                }
                tcsArray[i] = _pendingSends[messageIds[i]];
            }
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var allTasks = tcsArray.Select(tcs => tcs.Task).ToArray();
            var completed = await Task.WhenAny(Task.WhenAll(allTasks), Task.Delay(timeoutSeconds * 1000, cts.Token));

            foreach (var tcs in tcsArray)
            {
                if (tcs.Task.IsCompleted && tcs.Task.Result != null)
                {
                    return tcs.Task.Result;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
