using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

public partial class TdlService
{
    /// <summary>
    /// 删除指定源 chat 的本地转发历史记录（ForwardRecords 表）。
    /// 用于将某源/某目标/某次转发的"已转发"标记清理掉，以便下次重新转发。
    /// 不会删除 Telegram 上的实际消息。
    /// </summary>
    /// <param name="sourceLink">源链接/用户名/chatId，用于定位源 DB 文件。留空=操作所有源 DB。</param>
    /// <param name="targetLink">可选的目标过滤。0=不过滤。</param>
    /// <param name="scope">清理范围：all=全部, success=仅成功, failed=仅失败。</param>
    /// <param name="fromMessageId">可选，仅清理 MessageId &gt;= fromMessageId 的记录。0=不过滤。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>删除的记录数。</returns>
    public async Task<int> ClearForwardHistoryAsync(
        string? sourceLink,
        string? targetLink = null,
        string scope = "all",
        long fromMessageId = 0,
        CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var sourceChatId = 0L;
        if (!string.IsNullOrWhiteSpace(sourceLink))
        {
            // 源链接可能指向单条消息（含 messageId），先尝试解析，否则按 chatId/username 解析。
            var (resolvedChatId, _) = await ResolveSourceLinkAsync(sourceLink);
            sourceChatId = resolvedChatId;
            if (sourceChatId == 0)
            {
                sourceChatId = await ResolveChatIdAsync(sourceLink);
            }
        }

        long targetChatId = 0;
        if (!string.IsNullOrWhiteSpace(targetLink))
        {
            targetChatId = await ResolveChatIdAsync(targetLink);
        }

        var scopeNorm = (scope ?? "all").Trim().ToLowerInvariant();
        bool? onlySuccess = scopeNorm switch
        {
            "success" => true,
            "failed" => false,
            _ => null,
        };

        // 收集要操作的 db 文件路径。sourceChatId>0 时只操作单个 db，否则遍历所有 db 文件。
        var dbFiles = new List<string>();
        if (sourceChatId > 0)
        {
            dbFiles.Add(Path.Combine(TdlPaths.ForwardDbDir, $"forward-{sourceChatId}.db"));
        }
        else
        {
            Directory.CreateDirectory(TdlPaths.ForwardDbDir);
            dbFiles.AddRange(Directory.EnumerateFiles(TdlPaths.ForwardDbDir, "forward-*.db"));
        }

        if (dbFiles.Count == 0)
        {
            _logger.Log("未找到任何转发记录数据库");
            return 0;
        }

        int totalDeleted = 0;
        foreach (var path in dbFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(path)) continue;

            try
            {
                using var db = ForwardDbContext.OpenFromPath(path);
                await db.EnsureSchemaInitializedAsync();

                var deleted = await db.DeleteForwardRecordsAsync(sourceChatId, targetChatId, onlySuccess, fromMessageId);
                totalDeleted += deleted;

                _logger.Log($"已清理 {Path.GetFileName(path)}: {deleted} 条 (累计 {totalDeleted})");
            }
            catch (Exception ex)
            {
                _logger.Log($"清理 {Path.GetFileName(path)} 失败: {ex.Message}");
                PluginLoggers.For<TdlService>().LogWarning(ex, "[TdlService] ClearForwardHistory 异常: {Exception}");
            }
        }

        _logger.Log($"清理转发历史完成，共删除 {totalDeleted} 条记录");
        return totalDeleted;
    }
    public async Task ClearMessagesAsync(string? channelLink, string containsText, bool silent, int limit, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;
        var currentUser = await GetCurrentUserAsync();
        long myId = currentUser.Id;

        long chatId = await ResolveChatIdAsync(channelLink);
        if (chatId == 0)
        {
            chatId = myId;
            _logger.Log($"未指定频道，默认使用收藏夹 (ChatId={myId})");
        }

        var chat = await client.GetChatAsync(chatId);
        _logger.Log($"目标: [{chat.Title}] ChatId={chatId}");
        _logger.Log($"匹配内容: \"{containsText}\"");
        _logger.Log($"删除模式: {(silent ? "静默删除" : "交互确认")}");

        int totalDeleted = await CleanMessages(client, chatId, containsText, silent, limit, ct);
        _logger.Log($"清理完成，共删除 {totalDeleted} 条消息");
    }

    async Task<int> CleanMessages(TdClient client, long chatId, string containsText, bool silent, int limit, CancellationToken ct)
    {
        int totalDeleted = 0;
        long fromMessageId = 0;
        bool hasMore = true;
        var matchedMessages = new List<(long MsgId, string Text)>();

        _logger.Log("开始扫描消息...");

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

                foreach (var msg in history.Messages_)
                {
                    var text = MessageContentInspector.GetText(msg.Content);
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
                _logger.Log($"扫描消息时发生异常: {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }

        if (matchedMessages.Count == 0)
        {
            _logger.Log($"未找到包含 \"{containsText}\" 的消息");
            return 0;
        }

        _logger.Log($"共找到 {matchedMessages.Count} 条匹配消息");

        const int batchSize = 100;
        for (int i = 0; i < matchedMessages.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = matchedMessages.Skip(i).Take(batchSize).Select(m => m.MsgId).ToArray();
            try
            {
                await client.DeleteMessagesAsync(chatId, batch, revoke: true);
                totalDeleted += batch.Length;
                _logger.Log($"已删除 {totalDeleted}/{matchedMessages.Count} 条消息");
                await Task.Delay(500, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000, ct);
                i -= batchSize;
            }
            catch (Exception ex)
            {
                _logger.Log($"批量删除消息时发生异常: {ex.Message}");
            }
        }

        return totalDeleted;
    }
}
