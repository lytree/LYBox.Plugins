using Microsoft.Extensions.Logging;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public class TdlBatchForwardService
{
    private readonly TdlClientManager _clientManager;
    private readonly ILogger _logger;

    public TdlBatchForwardService(TdlClientManager clientManager, ILogger<TdlBatchForwardService> logger)
    {
        _clientManager = clientManager;
        _logger = logger;
    }

    public async Task ExecuteAsync(string sourceLink, string? sourceId, string targetLink,
        bool older, int limit, bool forwardComments, CancellationToken ct = default)
    {
        await _clientManager.InitializeAsync();
        await _clientManager.WaitReadyAsync();

        var client = _clientManager.Client;
        var tdlRoot = _clientManager.GetTdlRoot();
        var service = new TdlForwardService(client, _logger, tdlRoot);
        await service.WaitReadyAsync();

        var (sourceChatId, startMessageId) = await service.ResolveSourceLinkAsync(sourceLink);
        if (sourceChatId == 0)
        {
            _logger.LogError("无法解析源链接: {Link}", sourceLink);
            return;
        }

        if (!string.IsNullOrEmpty(sourceId) && long.TryParse(sourceId, out var sid))
        {
            startMessageId = sid;
        }

        var targetChatId = await service.ResolveTargetLinkAsync(targetLink);
        if (targetChatId == 0)
        {
            _logger.LogError("无法解析目标链接: {Link}", targetLink);
            return;
        }

        var sourceChat = await client.GetChatAsync(sourceChatId);
        var targetChat = await client.GetChatAsync(targetChatId);
        _logger.LogInformation("源: [{Title}] ChatId={ChatId}, StartMsgId={MsgId}", sourceChat.Title, sourceChatId, startMessageId);
        _logger.LogInformation("目标: [{Title}] ChatId={ChatId}", targetChat.Title, targetChatId);
        _logger.LogInformation("方向: {Direction}, 限制: {Limit}, 评论: {Comments}",
            older ? "向旧消息" : "向新消息",
            limit > 0 ? limit.ToString() : "无限制",
            forwardComments ? "是" : "否");

        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        using var db = new ForwardDbContext(sourceChatId, dataDir);
        await db.Database.EnsureCreatedAsync();
        _logger.LogInformation("数据库已就绪: forward-{ChatId}.db", sourceChatId);

        int totalForwarded;
        if (older)
        {
            totalForwarded = await service.ForwardOlderDirection(db, sourceChatId, startMessageId, targetChatId, limit, forwardComments);
        }
        else
        {
            totalForwarded = await service.ForwardNewerDirection(db, sourceChatId, startMessageId, targetChatId, limit, forwardComments);
        }

        _logger.LogInformation("全部转发完成，共转发 {Count} 条消息", totalForwarded);
    }
}
