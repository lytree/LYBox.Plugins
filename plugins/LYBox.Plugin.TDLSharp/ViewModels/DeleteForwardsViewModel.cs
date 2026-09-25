using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace LYBox.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeleteForwards")]
[Menu("NAV_TDL_DeleteForwards", "TDL_DeleteForwards", ParentKey = "NAV_TDL", Order = 10)]
[ViewMap(typeof(Pages.DeleteForwardsPage))]
public partial class DeleteForwardsViewModel : TdlViewModelBase
{
    protected override ScriptDescriptor CreateScript() => new()
    {
        Id = "delete-forwards",
        Name = Strings.Get("SCRIPT_DeleteForwards_Name"),
        Description = Strings.Get("SCRIPT_DeleteForwards_Desc"),
        Parameters =
        [
            ScriptParameter.HistoryText("channel", Strings.Get("PARAM_Channel"), Strings.Get("PARAM_ChannelDesc"), required: false),
            ScriptParameter.HistoryText("fromLink", Strings.Get("PARAM_FromLink"), Strings.Get("PARAM_FromLinkDesc"), required: false),
            ScriptParameter.Number("limit", Strings.Get("PARAM_MaxDelete"), Strings.Get("PARAM_MaxDeleteDesc"), 0),

            // ── 清理本地转发历史（不删除 Telegram 消息）──
            ScriptParameter.HistoryText("historySource", Strings.Get("PARAM_HistorySource"), Strings.Get("PARAM_HistorySourceDesc"), required: false),
            ScriptParameter.HistoryText("historyTarget", Strings.Get("PARAM_HistoryTarget"), Strings.Get("PARAM_HistoryTargetDesc"), required: false),
            ScriptParameter.Text("historyScope", Strings.Get("PARAM_HistoryScope"), Strings.Get("PARAM_HistoryScopeDesc"), "all"),
            ScriptParameter.Number("historyFromMsgId", Strings.Get("PARAM_HistoryFromMsgId"), Strings.Get("PARAM_HistoryFromMsgIdDesc"), 0),
        ]
    };

    protected override async Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var bag = new ScriptParameterBag(paramValues);
        paramValues.TryGetValue("channel", out var channel);
        paramValues.TryGetValue("fromLink", out var fromLink);
        paramValues.TryGetValue("historySource", out var historySource);
        paramValues.TryGetValue("historyTarget", out var historyTarget);
        paramValues.TryGetValue("historyScope", out var historyScope);

        // 简化判断：只要任一历史相关参数非默认，则执行历史清理；否则执行删除 Telegram 消息。
        bool wantClearHistory =
            !string.IsNullOrWhiteSpace(historySource)
            || !string.IsNullOrWhiteSpace(historyTarget)
            || bag.GetInt("historyFromMsgId") > 0;

        if (wantClearHistory)
        {
            var scope = string.IsNullOrWhiteSpace(historyScope) ? "all" : historicalScopeIsAll(historyScope);
            var confirm = await OverlayMessageBox.ShowAsync(
                Strings.Get("FMT_ClearHistoryConfirm", historySource ?? "(全部源)"),
                Strings.Get("LOG_ClearHistoryTitle"),
                button: MessageBoxButton.YesNo,
                icon: MessageBoxIcon.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            await tdlService.ClearForwardHistoryAsync(
                string.IsNullOrWhiteSpace(historySource) ? null : historySource,
                string.IsNullOrWhiteSpace(historyTarget) ? null : historyTarget,
                scope,
                bag.GetInt("historyFromMsgId"),
                ct);
            return;
        }

        await tdlService.DeleteAllForwardMessagesAsync(channel, fromLink, bag.GetInt("limit"), ct);
    }

    /// <summary>
    /// 一键清理按钮：直接使用页面参数中的 historySource/historyTarget/historyScope/historyFromMsgId
    /// 调用本地转发历史清理，不需要用户先按"执行"。点击前会弹窗确认。
    /// </summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (IsRunning) return;

        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        if (clientManager == null) return;

        if (!clientManager.HasTdlRoot)
        {
            StatusText = Strings.Get("LOGIN_TdlRootNotSet");
            return;
        }

        await clientManager.EnsureReadyForAuthCheckAsync();
        if (clientManager.NeedsLogin)
        {
            StatusText = Strings.Get("LOGIN_NotInitializedWarning");
            return;
        }

        // 读取参数面板的当前值（页面参数由基类构建并填充）。
        string? source = null, target = null, scopeRaw = null;
        long fromMsgId = 0;
        foreach (var p in Parameters)
        {
            switch (p.Key)
            {
                case "historySource": source = string.IsNullOrWhiteSpace(p.DefaultValue) ? null : p.DefaultValue; break;
                case "historyTarget": target = string.IsNullOrWhiteSpace(p.DefaultValue) ? null : p.DefaultValue; break;
                case "historyScope": scopeRaw = string.IsNullOrWhiteSpace(p.DefaultValue) ? "all" : p.DefaultValue; break;
                case "historyFromMsgId": long.TryParse(p.DefaultValue, out fromMsgId); break;
            }
        }

        var scope = historicalScopeIsAll(scopeRaw ?? "all");
        var scopeLabel = scope switch
        {
            "success" => Strings.Get("WORD_HistoryScope_Success"),
            "failed" => Strings.Get("WORD_HistoryScope_Failed"),
            _ => Strings.Get("WORD_HistoryScope_All"),
        };

        var confirm = await OverlayMessageBox.ShowAsync(
            Strings.Get("FMT_ClearHistoryQuickConfirm", source ?? "(全部源)", scopeLabel),
            Strings.Get("LOG_ClearHistoryTitle"),
            button: MessageBoxButton.YesNo,
            icon: MessageBoxIcon.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsRunning = true;
        StatusText = Strings.Get("STATUS_Running", Strings.Get("LOG_ClearHistoryTitle"));
        // 单独管理本操作的取消令牌：不重写基类的 _cts，避免与 ExecuteScript 流程冲突。
        using var localCts = new CancellationTokenSource();
        try
        {
            var tdlService = CreateTdlService();
            await tdlService.ClearForwardHistoryAsync(source, target, scope, fromMsgId, localCts.Token);
            StatusText = Strings.Get("STATUS_Completed");
        }
        catch (OperationCanceledException)
        {
            StatusText = Strings.Get("STATUS_Cancelled");
        }
        catch (Exception ex)
        {
            StatusText = $"{Strings.Get("STATUS_Failed")}: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// 归一化 scope 输入：接受 all/success/failed 三种关键字（大小写、空格不敏感），
    /// 以及中文"全部/成功/失败"和常见别名。其余输入回落为 "all"。
    /// </summary>
    static string historicalScopeIsAll(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "all";
        var s = raw.Trim().ToLowerInvariant();
        return s switch
        {
            "all" or "全部" or "所有" or "*" => "all",
            "success" or "ok" or "成功" or "已成功" => "success",
            "failed" or "fail" or "error" or "失败" or "已失败" => "failed",
            _ => "all",
        };
    }
}
