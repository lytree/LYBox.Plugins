using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.DouyinDownloader.Auth;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.WebServer;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.DouyinDownloader.ViewModels;

[NavigationItem("Douyin_Login")]
[Menu("NAV_DouyinLogin", "Douyin_Login", ParentKey = "NAV_DouyinRoot", Order = 5)]
[ViewMap(typeof(Pages.LoginPage))]
public partial class LoginViewModel : ViewModelBase
{
    private readonly CookieManager _cookies;
    private readonly WebConsoleServer _web;
    private readonly DownloaderSettingsStore _settings;

    [ObservableProperty] private string _qrHint = "";
    [ObservableProperty] private string _statusText = "未登录";
    [ObservableProperty] private string _manualCookie = "";
    [ObservableProperty] private bool _isLoggedIn;

    public LoginViewModel()
    {
        _cookies = ServiceLocator.TryGetService<CookieManager>(out var c) ? c! : throw new InvalidOperationException();
        _web = ServiceLocator.TryGetService<WebConsoleServer>(out var w) ? w! : throw new InvalidOperationException();
        _settings = ServiceLocator.TryGetService<DownloaderSettingsStore>(out var s) ? s! : throw new InvalidOperationException();
        IsLoggedIn = _cookies.Validate();
        StatusText = IsLoggedIn ? "已登录" : "未登录";
    }

    [RelayCommand]
    private void OpenLoginPage()
    {
        try
        {
            // 抖音 web 登录页 (扫码登录入口)
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.douyin.com/login",
                UseShellExecute = true,
            });
            QrHint = "请在打开的页面中完成扫码登录。登录完成后,在下面粘贴 Cookie 或点击'刷新 Cookie 检测'。";
        }
        catch (Exception ex) { QrHint = "打开失败: " + ex.Message; }
    }

    [RelayCommand]
    private void ImportManualCookie()
    {
        if (string.IsNullOrWhiteSpace(ManualCookie)) { StatusText = "请粘贴 Cookie"; return; }
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in ManualCookie.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var k = part[..idx].Trim();
            var v = part[(idx + 1)..].Trim();
            if (!string.IsNullOrEmpty(k)) dict[k] = v;
        }
        if (dict.Count == 0) { StatusText = "Cookie 解析为空"; return; }
        _cookies.Set(dict);
        IsLoggedIn = _cookies.Validate();
        StatusText = IsLoggedIn ? $"已登录,导入 {dict.Count} 个键" : "已导入但缺少必要键 (ttwid/odin_tt/passport_csrf_token)";
    }

    [RelayCommand]
    private void Recheck()
    {
        IsLoggedIn = _cookies.Validate();
        StatusText = IsLoggedIn ? "Cookie 有效" : "Cookie 缺失必要字段,请重新登录";
    }

    [RelayCommand]
    private void Clear()
    {
        _cookies.Clear();
        IsLoggedIn = false;
        StatusText = "已清空 Cookie";
    }
}
