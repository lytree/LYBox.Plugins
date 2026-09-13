using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.Downloader.ViewModels;

/// <summary>
/// 抖音下载单页面 Home：4 个 Tab（Submit / Jobs / History / Account）+ 顶部 2 个按钮（登录 / 设置）弹窗。
/// 是整个 Douyin 子模块唯一的导航入口 <c>[NavigationItem("Douyin_Home")]</c>。
/// 菜单直接挂在下载器根菜单 <c>NAV_Downloader</c> 之下，不再单独占用顶级菜单根（原 <c>NAV_DouyinRoot</c>）。
///
/// 设计说明：为了与 [GenerateMetadata] 生成的"无参构造"工厂兼容（生成器对每个 [ViewMap] VM 生成
/// <c>services.GetService(typeof(T)) ?? new T()</c>），本 VM 仅持有一个 IServiceProvider，
/// 子 VM 在首次访问各属性时通过它解析（懒加载）。
///
/// 本地化策略：XAML 不直接引用 <c>{x:Static resources:Strings.DYN_xxx}</c>
/// （因为 Strings 仅暴露通用的 <c>Get(key)</c> 方法，没有逐 key 静态属性），
/// 而是通过 VM 上暴露的 <c>string</c> 属性 + Binding 实现。这样随当前 UI culture 切换自动多语言。
/// </summary>
[NavigationItem("Douyin_Home")]
[Menu("NAV_Downloader_Douyin", "Douyin_Home", ParentKey = "NAV_Downloader", Order = 4)]
[ViewMap(typeof(Pages.DouyinHomePage))]
public partial class DouyinHomeViewModel : ViewModelBase
{
    private readonly IServiceProvider _sp;

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isLoginDialogOpen;
    [ObservableProperty] private bool _isSettingsDialogOpen;

    // =============== 本地化字符串（XAML 绑定这些属性，跟随当前 UI culture） ===============
    public string TitleText => Strings.Get("DYN_Plugin_Title");
    public string LoginButtonText => Strings.Get("DYN_Home_Btn_Login");
    public string SettingsButtonText => Strings.Get("DYN_Home_Btn_Settings");
    public string SubmitTabHeader => Strings.Get("DYN_Home_Tab_Submit");
    public string JobsTabHeader => Strings.Get("DYN_Home_Tab_Jobs");
    public string HistoryTabHeader => Strings.Get("DYN_Home_Tab_History");
    public string AccountTabHeader => Strings.Get("DYN_Home_Tab_Account");
    public string AccountStatusText => Strings.Get("DYN_Home_Account_Status");
    public string LoginDialogTitle => Strings.Get("DYN_Dialog_Login_Title");
    public string SettingsDialogTitle => Strings.Get("DYN_Dialog_Settings_Title");
    public string CloseButtonText => Strings.Get("DYN_Btn_Close");

    private SubmitViewModel? _submit;
    public SubmitViewModel Submit => _submit ??= _sp.GetRequiredService<SubmitViewModel>();

    private JobsViewModel? _jobs;
    public JobsViewModel Jobs => _jobs ??= _sp.GetRequiredService<JobsViewModel>();

    private HistoryViewModel? _history;
    public HistoryViewModel History => _history ??= _sp.GetRequiredService<HistoryViewModel>();

    private LoginViewModel? _login;
    public LoginViewModel Login => _login ??= _sp.GetRequiredService<LoginViewModel>();

    private SettingsViewModel? _settings;
    public SettingsViewModel Settings => _settings ??= _sp.GetRequiredService<SettingsViewModel>();

    public DouyinHomeViewModel() : this(ServiceLocator.GetServiceProvider()) { }

    public DouyinHomeViewModel(IServiceProvider sp)
    {
        _sp = sp;
    }

    [RelayCommand]
    private void OpenLogin() => IsLoginDialogOpen = true;

    [RelayCommand]
    private void CloseLogin() => IsLoginDialogOpen = false;

    [RelayCommand]
    private void OpenSettings() => IsSettingsDialogOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsDialogOpen = false;
}
