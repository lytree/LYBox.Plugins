using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TdLib;
using static TdLib.TdApi;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task DeepCopyAsync(string? sourceLink, int limit, bool forwardComments, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;
        var currentUser = await GetCurrentUserAsync();
        long myId = currentUser.Id;

        long sourceChatId = await ResolveChatIdAsync(sourceLink);
        if (sourceChatId == 0)
        {
            sourceChatId = myId;
            _logger.Log($"未指定源频道，默认使用收藏夹 (ChatId={myId})");
        }

        var sourceChat = await client.GetChatAsync(sourceChatId);
        _logger.Log($"源: [{sourceChat.Title}] ChatId={sourceChatId}");

        using var db = CreateForwardDbContext(sourceChatId);
        await db.Database.EnsureCreatedAsync();
        _logger.Log($"数据库已就绪: forward-{sourceChatId}.db");

        int totalForwarded = 0;
        int totalSkipped = 0;
        long fromMessageId = 0;
        List<TdApi.Message>? pendingGroup = null;
        bool hasMore = true;

        _logger.Log("开始扫描浅转发消息，转换为深度Copy...");

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

                var messages = history.Messages_
                    .Where(m => m.ForwardInfo != null)
                    .OrderBy(m => m.Id)
                    .ToList();

                if (messages.Count == 0)
                {
                    var remainingHasShallow = await CheckRemainingShallowForwards(client, sourceChatId, history.Messages_.Last().Id, ct);
                    if (!remainingHasShallow)
                    {
                        _logger.Log("后续消息中不再存在浅转发消息，停止扫描");
                        hasMore = false;
                        break;
                    }

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
                var (forwarded, skipped) = await ForwardGroupedMessages(db, toProcess, sourceChatId, sourceChatId, forwardComments);
                totalForwarded += forwarded;
                totalSkipped += skipped;

                if (limit > 0 && totalForwarded >= limit)
                {
                    _logger.Log($"已达到转发限制 {limit}");
                    break;
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(1000);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000);
            }
            catch (Exception ex)
            {
                _logger.Log($"转发过程中发生异常: {ex.Message}");
                await Task.Delay(5000);
            }

        }

        _logger.Log($"全部完成: 处理 {totalForwarded} 条,  跳过 {totalSkipped} 条");
    }
    async Task<bool> CheckRemainingShallowForwards(TdClient client, long chatId, long fromMessageId, CancellationToken ct)
    {
        try
        {
            long checkFromId = fromMessageId;
            for (int i = 0; i < 3; i++)
            {
                ct.ThrowIfCancellationRequested();
                var history = await client.GetChatHistoryAsync(chatId, checkFromId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    return false;
                }

                if (history.Messages_.Any(m => m.ForwardInfo != null))
                {
                    return true;
                }

                checkFromId = history.Messages_.Last().Id;
                await Task.Delay(300, ct);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> DeleteShallowForwardMessagesAsync(long chatId, CancellationToken ct = default)
    {
        await EnsureReadyAsync();
        var client = Client;

        using var db = CreateForwardDbContext(chatId);
        await db.Database.EnsureCreatedAsync();

        var successRecordIds = await db.ForwardRecords
            .Where(r => r.SourceChatId == chatId && r.TargetChatId == chatId && r.IsSuccess && r.NewMessageId != 0)
            .Select(r => r.MessageId)
            .ToHashSetAsync();

        if (successRecordIds.Count == 0)
        {
            _logger.Log("数据库中没有已成功深Copy的记录");
            return 0;
        }

        _logger.Log($"数据库中有 {successRecordIds.Count} 条已深Copy记录，开始扫描频道消息...");

        int totalDeleted = 0;
        int totalScanned = 0;
        long fromMessageId = 0;
        bool hasMore = true;

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

                var forwardedMessages = history.Messages_
                    .Where(m => m.ForwardInfo != null)
                    .ToList();

                totalScanned += history.Messages_.Length;

                if (forwardedMessages.Count > 0)
                {
                    var toDelete = forwardedMessages
                        .Where(m => successRecordIds.Contains(m.Id))
                        .Select(m => m.Id)
                        .ToArray();

                    if (toDelete.Length > 0)
                    {
                        await client.DeleteMessagesAsync(
                            chatId: chatId,
                            messageIds: toDelete,
                            revoke: true
                        );

                        totalDeleted += toDelete.Length;
                        _logger.Log($"删除 {toDelete.Length} 条浅转发消息 (累计: {totalDeleted})");
                        await Task.Delay(1000, ct);
                    }
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(500, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒...");
                await Task.Delay(retryAfter * 1000, ct);
            }
            catch (Exception ex)
            {
                _logger.Log($"扫描消息时发生异常: {ex.Message}");
                await Task.Delay(3000, ct);
            }
        }

        _logger.Log($"删除完成: 扫描 {totalScanned} 条消息, 删除 {totalDeleted} 条浅转发消息");
        return totalDeleted;
    }
}
