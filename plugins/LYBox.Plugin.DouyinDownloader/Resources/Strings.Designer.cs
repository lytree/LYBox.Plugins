#nullable enable
namespace LYBox.Plugin.DouyinDownloader.Resources;

public static class Strings
{
    private static global::System.Resources.ResourceManager? _resourceManager;

    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new global::System.Resources.ResourceManager(
                    "LYBox.Plugin.DouyinDownloader.Resources.Strings",
                    typeof(Strings).Assembly);
            }
            return _resourceManager;
        }
    }

    private static global::System.Globalization.CultureInfo? _culture;

    public static global::System.Globalization.CultureInfo Culture
    {
        get => _culture ?? global::System.Globalization.CultureInfo.CurrentUICulture;
        set => _culture = value;
    }

    /// <summary>插件主标题。</summary>
    public static string Plugin_Title => ResourceManager.GetString(nameof(Plugin_Title), Culture)!;

    public static string Plugin_Description => ResourceManager.GetString(nameof(Plugin_Description), Culture)!;

    public static string Nav_DouyinRoot => ResourceManager.GetString(nameof(Nav_DouyinRoot), Culture)!;
    public static string Nav_DouyinSubmit => ResourceManager.GetString(nameof(Nav_DouyinSubmit), Culture)!;
    public static string Nav_DouyinJobs => ResourceManager.GetString(nameof(Nav_DouyinJobs), Culture)!;
    public static string Nav_DouyinHistory => ResourceManager.GetString(nameof(Nav_DouyinHistory), Culture)!;
    public static string Nav_DouyinSettings => ResourceManager.GetString(nameof(Nav_DouyinSettings), Culture)!;
    public static string Nav_DouyinLogin => ResourceManager.GetString(nameof(Nav_DouyinLogin), Culture)!;

    public static string Submit_UrlHint => ResourceManager.GetString(nameof(Submit_UrlHint), Culture)!;
    public static string Submit_Mode => ResourceManager.GetString(nameof(Submit_Mode), Culture)!;
    public static string Submit_Number => ResourceManager.GetString(nameof(Submit_Number), Culture)!;
    public static string Submit_StartDate => ResourceManager.GetString(nameof(Submit_StartDate), Culture)!;
    public static string Submit_EndDate => ResourceManager.GetString(nameof(Submit_EndDate), Culture)!;
    public static string Submit_Button => ResourceManager.GetString(nameof(Submit_Button), Culture)!;

    public static string Status_Ready => ResourceManager.GetString(nameof(Status_Ready), Culture)!;
    public static string Status_Running => ResourceManager.GetString(nameof(Status_Running), Culture)!;
    public static string Status_Failed => ResourceManager.GetString(nameof(Status_Failed), Culture)!;
    public static string Status_Success => ResourceManager.GetString(nameof(Status_Success), Culture)!;
    public static string Status_Skipped => ResourceManager.GetString(nameof(Status_Skipped), Culture)!;

    public static string Login_Tip => ResourceManager.GetString(nameof(Login_Tip), Culture)!;
    public static string Login_OpenQR => ResourceManager.GetString(nameof(Login_OpenQR), Culture)!;
    public static string Login_ReloadQR => ResourceManager.GetString(nameof(Login_ReloadQR), Culture)!;
    public static string Login_Manual => ResourceManager.GetString(nameof(Login_Manual), Culture)!;

    public static string Settings_SignerUrl => ResourceManager.GetString(nameof(Settings_SignerUrl), Culture)!;
    public static string Settings_DownloadPath => ResourceManager.GetString(nameof(Settings_DownloadPath), Culture)!;
    public static string Settings_Concurrency => ResourceManager.GetString(nameof(Settings_Concurrency), Culture)!;
    public static string Settings_Proxy => ResourceManager.GetString(nameof(Settings_Proxy), Culture)!;
    public static string Settings_Webhook => ResourceManager.GetString(nameof(Settings_Webhook), Culture)!;

    public static string Msg_NoConfig => ResourceManager.GetString(nameof(Msg_NoConfig), Culture)!;
    public static string Msg_DuplicateUrl => ResourceManager.GetString(nameof(Msg_DuplicateUrl), Culture)!;

    public static string Get(string key, params object[] args)
    {
        var value = ResourceManager.GetString(key, Culture) ?? key;
        return args.Length > 0 ? string.Format(Culture, value, args) : value;
    }
}
