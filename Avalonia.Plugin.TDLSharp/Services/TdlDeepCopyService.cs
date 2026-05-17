using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public class TdlDeepCopyService
{
    private readonly TdlClientManager _clientManager;
    private readonly ILogger _logger;

    public TdlDeepCopyService(TdlClientManager clientManager, ILogger<TdlDeepCopyService> logger)
    {
        _clientManager = clientManager;
        _logger = logger;
    }

    public async Task ExecuteAsync(string? sourceLink, int limit, bool forwardComments, CancellationToken ct = default)
    {
        await _clientManager.InitializeAsync();
        await _clientManager.WaitReadyAsync();

        var client = _clientManager.Client;
        var tdlRoot = _clientManager.GetTdlRoot();
        var service = new TdlForwardService(client, _logger, tdlRoot);
        await service.WaitReadyAsync();

        var currentUser = await _clientManager.GetCurrentUserAsync();
        long myId = currentUser.Id;

        long sourceChatId = await service.ResolveChatIdAsync(sourceLink);
        if (sourceChatId == 0)
        {
            sourceChatId = myId;
            _logger.LogInformation("未指定源频道，默认使用收藏夹 (ChatId={ChatId})", myId);
        }

        var sourceChat = await client.GetChatAsync(sourceChatId);
        _logger.LogInformation("源: [{Title}] ChatId={ChatId}", sourceChat.Title, sourceChatId);

        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        using var db = new ForwardDbContext(sourceChatId, dataDir);
        await db.Database.EnsureCreatedAsync();
        _logger.LogInformation("数据库已就绪: forward-{ChatId}.db", sourceChatId);

        int totalProcessed = 0;
        int totalDeepCopied = 0;
        int totalDeleted = 0;
        int totalSkipped = 0;
        long fromMessageId = 0;
        bool hasMore = true;

        _logger.LogInformation("开始扫描浅转发消息，转换为深度Copy...");

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var history = await client.GetChatHistoryAsync(sourceChatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                var shallowForwards = history.Messages_
                    .Where(m => m.ForwardInfo != null)
                    .OrderBy(m => m.Id)
                    .ToList();

                if (shallowForwards.Count == 0)
                {
                    fromMessageId = history.Messages_.Last().Id;
                    continue;
                }

                foreach (var msg in shallowForwards)
                {
                    ct.ThrowIfCancellationRequested();

                    if (limit > 0 && totalProcessed >= limit)
                    {
                        _logger.LogInformation("已达到处理限制 {Limit}", limit);
                        hasMore = false;
                        break;
                    }

                    totalProcessed++;

                    var existingRecord = await db.ForwardRecords
                        .FirstOrDefaultAsync(r => r.SourceChatId == sourceChatId && r.MessageId == msg.Id);

                    if (existingRecord != null && existingRecord.IsSuccess)
                    {
                        totalSkipped++;
                        continue;
                    }

                    var forwardInfo = msg.ForwardInfo!;
                    long originChatId = 0;
                    long originMessageId = 0;

                    if (forwardInfo.Origin is TdApi.MessageOrigin.MessageOriginChannel oc)
                    {
                        originChatId = oc.ChatId;
                        originMessageId = oc.MessageId;
                    }
                    else if (forwardInfo.Source != null)
                    {
                        originChatId = forwardInfo.Source.ChatId;
                        originMessageId = forwardInfo.Source.MessageId;
                    }

                    if (originChatId == 0 || originMessageId == 0)
                    {
                        _logger.LogWarning("消息 {MsgId} 的转发来源信息不完整，跳过", msg.Id);
                        continue;
                    }

                    _logger.LogInformation("[{Index}] 消息 {MsgId}: 浅转发来源 ChatId={OriginChatId}, MsgId={OriginMsgId}",
                        totalProcessed, msg.Id, originChatId, originMessageId);

                    bool deepCopySuccess = false;

                    try
                    {
                        var originChat = await client.GetChatAsync(originChatId);
                        _logger.LogInformation("  来源频道: [{Title}]", originChat.Title);
                    }
                    catch (TdException ex)
                    {
                        _logger.LogWarning("  无法访问来源频道 ChatId={ChatId}: {Error}", originChatId, ex.Error.Message);
                    }

                    try
                    {
                        var forwardResult = await client.ForwardMessagesAsync(
                            chatId: sourceChatId,
                            fromChatId: originChatId,
                            messageIds: [originMessageId],
                            sendCopy: true,
                            removeCaption: false
                        );

                        if (forwardResult.Messages_ != null && forwardResult.Messages_.Length > 0)
                        {
                            deepCopySuccess = true;
                            totalDeepCopied++;
                            _logger.LogInformation("  深度Copy成功: 新消息 MsgId={NewMsgId}", forwardResult.Messages_[0].Id);
                        }
                    }
                    catch (TdException ex) when (ex.Error.Code == 429)
                    {
                        int retryAfter = ParseRetryAfter(ex);
                        _logger.LogWarning("  触发频率限制，等待 {Seconds} 秒后继续...", retryAfter);
                        await Task.Delay(retryAfter * 1000, ct);
                        continue;
                    }
                    catch (TdException ex)
                    {
                        _logger.LogError("  深度Copy失败: {Code}: {Message}", ex.Error.Code, ex.Error.Message);
                    }

                    if (deepCopySuccess)
                    {
                        try
                        {
                            await client.DeleteMessagesAsync(
                                chatId: sourceChatId,
                                messageIds: [msg.Id],
                                revoke: true
                            );

                            totalDeleted++;
                            _logger.LogInformation("  已删除旧浅转发消息 MsgId={MsgId}", msg.Id);

                            if (existingRecord != null)
                            {
                                existingRecord.IsSuccess = true;
                                existingRecord.ForwardedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                db.ForwardRecords.Add(new ForwardRecord
                                {
                                    MessageId = msg.Id,
                                    SourceChatId = sourceChatId,
                                    TargetChatId = sourceChatId,
                                    MediaAlbumId = msg.MediaAlbumId,
                                    IsSuccess = true,
                                    ForwardedAt = DateTime.UtcNow,
                                    ExtraData = ForwardRecord.BuildExtraData(msg, null)
                                });
                            }

                            await db.SaveChangesAsync();
                        }
                        catch (TdException ex)
                        {
                            _logger.LogError("  删除旧消息失败 MsgId={MsgId}: {Code}: {Message}", msg.Id, ex.Error.Code, ex.Error.Message);
                        }
                    }
                    else
                    {
                        if (existingRecord == null)
                        {
                            db.ForwardRecords.Add(new ForwardRecord
                            {
                                MessageId = msg.Id,
                                SourceChatId = sourceChatId,
                                TargetChatId = sourceChatId,
                                MediaAlbumId = msg.MediaAlbumId,
                                IsSuccess = false,
                                ForwardedAt = DateTime.UtcNow,
                                ExtraData = ForwardRecord.BuildExtraData(msg, "DeepCopyFailed")
                            });
                            await db.SaveChangesAsync();
                        }
                    }

                    if (forwardComments && deepCopySuccess)
                    {
                        await ForwardCommentsForMessage(client, db, sourceChatId, msg, ct);
                    }

                    await Task.Delay(1500, ct);
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.LogWarning("触发频率限制，等待 {Seconds} 秒后继续...", retryAfter);
                await Task.Delay(retryAfter * 1000, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理过程中发生异常");
                await Task.Delay(5000, ct);
            }
        }

        _logger.LogInformation("全部完成: 处理 {Processed} 条, 深度Copy {DeepCopied} 条, 删除旧消息 {Deleted} 条, 跳过 {Skipped} 条",
            totalProcessed, totalDeepCopied, totalDeleted, totalSkipped);
    }

    async Task ForwardCommentsForMessage(TdClient client, ForwardDbContext db, long chatId, TdApi.Message sourceMsg, CancellationToken ct)
    {
        try
        {
            var replyInfo = sourceMsg.InteractionInfo?.ReplyInfo;
            if (replyInfo == null || replyInfo.ReplyCount == 0) return;

            var allComments = new List<TdApi.Message>();
            long fromMsgId = 0;
            bool hasMore = true;

            while (hasMore)
            {
                ct.ThrowIfCancellationRequested();

                var page = await client.GetMessageThreadHistoryAsync(
                    chatId: chatId,
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
                    await Task.Delay(300, ct);
                }
            }

            if (allComments.Count == 0) return;

            _logger.LogInformation("  发现 {Count} 条评论待处理", allComments.Count);

            foreach (var comment in allComments.OrderBy(m => m.Id))
            {
                ct.ThrowIfCancellationRequested();

                if (comment.ForwardInfo == null) continue;

                var commentForwardInfo = comment.ForwardInfo;
                long originChatId = 0;
                long originMessageId = 0;

                if (commentForwardInfo.Origin is TdApi.MessageOrigin.MessageOriginChannel occ)
                {
                    originChatId = occ.ChatId;
                    originMessageId = occ.MessageId;
                }
                else if (commentForwardInfo.Source != null)
                {
                    originChatId = commentForwardInfo.Source.ChatId;
                    originMessageId = commentForwardInfo.Source.MessageId;
                }

                if (originChatId == 0 || originMessageId == 0) continue;

                try
                {
                    var forwardResult = await client.ForwardMessagesAsync(
                        chatId: chatId,
                        fromChatId: originChatId,
                        messageIds: [originMessageId],
                        sendCopy: true,
                        removeCaption: false
                    );

                    if (forwardResult.Messages_ != null && forwardResult.Messages_.Length > 0)
                    {
                        await client.DeleteMessagesAsync(
                            chatId: chatId,
                            messageIds: [comment.Id],
                            revoke: true
                        );

                        _logger.LogInformation("  评论深度Copy并删除: MsgId={OldMsgId} → {NewMsgId}", comment.Id, forwardResult.Messages_[0].Id);
                    }
                }
                catch (TdException ex) when (ex.Error.Code == 429)
                {
                    int retryAfter = ParseRetryAfter(ex);
                    _logger.LogWarning("  评论处理触发频率限制，等待 {Seconds} 秒...", retryAfter);
                    await Task.Delay(retryAfter * 1000, ct);
                }
                catch (TdException ex)
                {
                    _logger.LogWarning("  评论深度Copy失败 MsgId={MsgId}: {Error}", comment.Id, ex.Error.Message);
                }

                await Task.Delay(1500, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理评论时发生异常");
        }
    }

    int ParseRetryAfter(TdException ex)
    {
        if (ex.Error?.Message != null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(ex.Error.Message, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int seconds) && seconds > 0)
            {
                return Math.Min(seconds + 2, 300);
            }
        }
        return 15;
    }
}
