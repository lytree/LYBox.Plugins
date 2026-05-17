using Microsoft.Extensions.Logging;
using TdLib;
using TdLib.Bindings;

namespace Avalonia.Plugin.TDLSharp.Services;

public class TdlClientManager : IDisposable
{
    private TdClient? _client;
    private TdlUpdateHandler? _updateHandler;
    private readonly ManualResetEventSlim _ready = new();
    private readonly ILogger _logger;
    private readonly string _tdlRoot;
    private readonly string _apiId;
    private readonly string _apiHash;
    private readonly string _proxyServer;
    private readonly int _proxyPort;
    private readonly bool _enableProxy;

    public bool AuthNeeded => _updateHandler?.AuthNeeded ?? false;
    public bool PasswordNeeded => _updateHandler?.PasswordNeeded ?? false;
    public bool IsReady => _ready.IsSet;
    public TdClient Client => _client ?? throw new InvalidOperationException("Client not initialized. Call InitializeAsync first.");

    public event Func<string, Task>? AuthCodeRequested;
    public event Func<string, Task>? PasswordRequested;
    public event Func<TdApi.File, Task>? FileUpdated;

    public TdlClientManager(ILogger logger, string apiId, string apiHash,
        string proxyServer = "127.0.0.1", int proxyPort = 7897, bool enableProxy = true)
    {
        _logger = logger;
        _apiId = apiId;
        _apiHash = apiHash;
        _proxyServer = proxyServer;
        _proxyPort = proxyPort;
        _enableProxy = enableProxy;

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _tdlRoot = Path.Combine(userProfile, ".tdl");
        if (!Directory.Exists(_tdlRoot))
        {
            Directory.CreateDirectory(_tdlRoot);
        }
    }

    public async Task InitializeAsync()
    {
        _client = new TdClient();
        _client.Bindings.SetLogVerbosityLevel(TdLogLevel.Fatal);

        _updateHandler = new TdlUpdateHandler(_ready, _logger)
            .OnConfigureTdlibParameters(ConfigureTdlibParameters)
            .OnFileUpdate(HandleFileUpdate);

        _client.UpdateReceived += async (_, update) =>
        {
            await _updateHandler.ProcessUpdates(_client, update, _tdlRoot);
        };
    }

    public async Task WaitReadyAsync()
    {
        _ready.Wait();
    }

    public async Task AuthenticateAsync(string phoneNumber)
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");

        await _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber
        {
            PhoneNumber = phoneNumber
        });
    }

    public async Task SubmitAuthCode(string code)
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        await _client.ExecuteAsync(new TdApi.CheckAuthenticationCode { Code = code });
    }

    public async Task SubmitPassword(string password)
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        await _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword { Password = password });
    }

    public async Task<TdApi.User> GetCurrentUserAsync()
    {
        if (_client == null) throw new InvalidOperationException("Client not initialized.");
        return await _client.ExecuteAsync(new TdApi.GetMe());
    }

    public string GetTdlRoot() => _tdlRoot;

    private async Task ConfigureTdlibParameters(TdClient client, string outputPath, ILogger cbLogger)
    {
        await client.ExecuteAsync(new TdApi.SetTdlibParameters
        {
            ApiId = int.TryParse(_apiId, out var id) ? id : 0,
            ApiHash = _apiHash,
            DeviceModel = "PC",
            SystemLanguageCode = "en",
            ApplicationVersion = "1.0.0",
            DatabaseDirectory = Path.Combine(_tdlRoot, "db"),
            FilesDirectory = Path.Combine(_tdlRoot, "files"),
            UseFileDatabase = true,
            UseChatInfoDatabase = true,
            UseMessageDatabase = true,
        });

        if (_enableProxy)
        {
            cbLogger.LogInformation("正在尝试连接代理...");
            var proxy = await client.AddProxyAsync(
                new TdApi.Proxy
                {
                    Server = _proxyServer,
                    Port = _proxyPort,
                    Type = new TdApi.ProxyType.ProxyTypeSocks5()
                }, true);
            await client.EnableProxyAsync(proxy.Id);
            cbLogger.LogInformation("代理已启用。");
        }
    }

    private Task HandleFileUpdate(TdApi.File file, string outputPath, ILogger cbLogger)
    {
        if (file.Local.IsDownloadingCompleted)
        {
            cbLogger.LogInformation("文件下载完成！本地路径: {Path}", file.Local.Path);
            FileUpdated?.Invoke(file);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _ready.Dispose();
    }
}
