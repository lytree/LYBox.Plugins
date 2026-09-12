using System.Reflection;
using LYBox.Plugin.DouyinDownloader.Auth;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core;

/// <summary>
/// 浏览器兜底（与原项目 api_client.collect_user_post_ids_via_browser 等价）：
/// 当 API 翻页受限时,启动 Chromium 滚动作者主页,通过 page.evaluate 抽取 /video/{id} 链接,
/// 同时监听 /aweme/v1/web/aweme/post/ 响应,提取响应中的 aweme_id 集合。
///
/// Microsoft.Playwright 通过反射调用,因此项目编译时不需要强依赖 Playwright;
/// 运行时若未安装 Playwright + Chromium,会优雅降级为空结果。
/// </summary>
public sealed class PlaywrightFallback
{
    private readonly DownloaderSettingsStore _settings;
    private readonly CookieManager _cookies;

    public PlaywrightFallback(DownloaderSettingsStore settings, CookieManager cookies)
    {
        _settings = settings;
        _cookies = cookies;
    }

    public sealed class BrowserResult
    {
        public List<string> AwemeIds { get; set; } = new();
        public int Merged { get; set; }
        public int FromPostApi { get; set; }
        public int PostPages { get; set; }
        public string? StopReason { get; set; }
        public bool VerificationPrompted { get; set; }
    }

    public bool IsAvailable()
    {
        try
        {
            var asm = Assembly.Load("Microsoft.Playwright");
            return asm != null;
        }
        catch { return false; }
    }

    public async Task<BrowserResult> CollectUserPostIdsAsync(string secUid, int expectedCount, CancellationToken ct)
    {
        var res = new BrowserResult();
        if (!_settings.Current.BrowserFallbackEnabled) { res.StopReason = "disabled"; return res; }
        if (!IsAvailable())
        {
            Logger.Warn("未检测到 Microsoft.Playwright 程序集,跳过浏览器兜底。请运行: dotnet add package Microsoft.Playwright && playwright install chromium");
            res.StopReason = "playwright_not_installed";
            return res;
        }
        return await Task.Run(() => CollectInternal(secUid, expectedCount, ct), ct);
    }

    private BrowserResult CollectInternal(string secUid, int expectedCount, CancellationToken ct)
    {
        _settingsHolder = _settings;
        var res = new BrowserResult();
        try
        {
            var asm = Assembly.Load("Microsoft.Playwright");
            dynamic playwright = CreatePlaywrightAsync(asm).GetAwaiter().GetResult();
            try
            {
                dynamic browser = LaunchBrowserAsync(playwright, asm).GetAwaiter().GetResult();
                try
                {
                    dynamic context = NewContextAsync(browser, asm).GetAwaiter().GetResult();
                    try
                    {
                        AddCookies(context, asm);

                        // newPage + 监听
                        dynamic page = NewPageAsync(context, asm).GetAwaiter().GetResult();
                        var postApis = new System.Collections.Concurrent.ConcurrentBag<string>();
                        var postPages = 0;
                        HookResponse(page, asm, postApis, (Action)(() => System.Threading.Interlocked.Increment(ref postPages)));

                        // 跳到用户主页
                        GotoAsync(page, asm, $"https://www.douyin.com/user/{secUid}").GetAwaiter().GetResult();

                        // 检测验证码
                        if (HasVerificationTitle(page, asm))
                        {
                            if (_settings.Current.BrowserFallbackHeadless)
                            {
                                Logger.Warn("检测到验证码页面且当前为 headless,请将 BrowserFallbackHeadless 设为 false");
                                res.VerificationPrompted = false;
                                res.StopReason = "verification_required";
                            }
                            else
                            {
                                Logger.Warn("检测到验证码,请在浏览器中完成验证,程序会自动继续");
                                res.VerificationPrompted = true;
                                WaitForManualVerification(page, asm, _settings.Current.BrowserFallbackWaitTimeoutSeconds);
                            }
                        }

                        // 滚动加载
                        var stableRounds = 0;
                        var merged = new HashSet<string>();
                        var fromDom = new HashSet<string>();
                        for (int i = 0; i < _settings.Current.BrowserFallbackMaxScrolls; i++)
                        {
                            if (ct.IsCancellationRequested) break;
                            WheelAsync(page, asm, 0f, 3800f).GetAwaiter().GetResult();
                            System.Threading.Thread.Sleep(1200);

                            int before = merged.Count;
                            try
                            {
                                var script = @"() => { const result = []; const seen = new Set();
                                    for (const a of document.querySelectorAll('a[href]')) {
                                        const m = (a.getAttribute('href')||'').match(/\/video\/(\d{15,20})/);
                                        if (m && !seen.has(m[1])) { seen.add(m[1]); result.push(m[1]); }
                                    }
                                    return result; }";
                                var arr = EvaluateAsync(page, asm, script).GetAwaiter().GetResult();
                                if (arr is System.Collections.IEnumerable en)
                                    foreach (var v in en) if (v != null) fromDom.Add(v.ToString()!);
                            }
                            catch { }
                            merged.UnionWith(fromDom);
                            merged.UnionWith(postApis);
                            if (merged.Count == before) stableRounds++; else stableRounds = 0;
                            if (expectedCount > 0 && merged.Count >= expectedCount) break;
                            if (stableRounds >= _settings.Current.BrowserFallbackIdleRounds) break;
                        }
                        res.AwemeIds = merged.ToList();
                        res.Merged = merged.Count;
                        res.FromPostApi = postApis.Distinct().Count();
                        res.PostPages = postPages;
                        res.StopReason = "completed";
                    }
                    finally
                    {
                        TryAsync(context, "CloseAsync");
                    }
                }
                finally
                {
                    TryAsync(browser, "CloseAsync");
                }
            }
            finally
            {
                TryAsync(playwright, "DisposeAsync");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"浏览器兜底失败: {ex.Message}");
            res.StopReason = "exception:" + ex.Message;
        }
        return res;
    }

    private static async Task<object> CreatePlaywrightAsync(Assembly asm)
    {
        var t = asm.GetType("Microsoft.Playwright.Playwright", true)!;
        var m = t.GetMethod("CreateAsync", BindingFlags.Public | BindingFlags.Static)!;
        var task = (Task)m.Invoke(null, new object?[] { null })!;
        await task;
        var resultProp = task.GetType().GetProperty("Result")!;
        return resultProp.GetValue(task)!;
    }

    private static async Task<object> LaunchBrowserAsync(dynamic playwright, Assembly asm)
    {
        var chromiumProp = playwright.GetType().GetProperty("Chromium")!;
        var chromium = chromiumProp.GetValue(playwright)!;
        var optsType = asm.GetType("Microsoft.Playwright.LaunchOptions", true)!;
        var opts = Activator.CreateInstance(optsType)!;
        optsType.GetProperty("Headless")!.SetValue(opts, _settingsHolder!.Current.BrowserFallbackHeadless);
        var argsListType = typeof(List<string>);
        var argsList = (IList<string>?)optsType.GetProperty("Args")!.GetValue(opts) ?? new List<string>();
        argsList.Add("--disable-blink-features=AutomationControlled");
        argsList.Add("--disable-dev-shm-usage");
        argsList.Add("--no-sandbox");
        optsType.GetProperty("Args")!.SetValue(opts, argsList);
        var launchMethod = chromium.GetType().GetMethod("LaunchAsync", new[] { optsType })!;
        var task = (Task)launchMethod.Invoke(chromium, new[] { opts })!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    // 静态设置持有 (避免 dynamic 扩展里再传 settings)
    private static DownloaderSettingsStore? _settingsHolder;

    private static async Task<object> NewContextAsync(dynamic browser, Assembly asm)
    {
        var ctxType = asm.GetType("Microsoft.Playwright.IBrowser", true)!;
        var ctxOptsType = asm.GetType("Microsoft.Playwright.BrowserNewContextOptions", true)!;
        var ctxOpts = Activator.CreateInstance(ctxOptsType)!;
        ctxOptsType.GetProperty("Locale")!.SetValue(ctxOpts, "zh-CN");
        var vpType = asm.GetType("Microsoft.Playwright.ViewportSize", true)!;
        ctxOptsType.GetProperty("Viewport")!.SetValue(ctxOpts, Activator.CreateInstance(vpType, new object[] { 1600, 900 }));
        ctxOptsType.GetProperty("UserAgent")!.SetValue(ctxOpts, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
        var newContextMethod = browser.GetType().GetMethod("NewContextAsync", new[] { ctxOptsType })!;
        var task = (Task)newContextMethod.Invoke(browser, new[] { ctxOpts })!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private void AddCookies(dynamic context, Assembly asm)
    {
        var cookiesProp = typeof(CookieManager).GetProperty("Cookies");
        var cookiesDict = (System.Collections.IEnumerable?)cookiesProp!.GetValue(_cookies);
        if (cookiesDict == null) return;
        var cookieList = new List<object>();
        foreach (var kv in cookiesDict)
        {
            var keyProp = kv.GetType().GetProperty("Key");
            var valProp = kv.GetType().GetProperty("Value");
            var k = (string?)keyProp!.GetValue(kv);
            var v = (string?)valProp!.GetValue(kv);
            if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) continue;
            if (DouyinApiClient.BrowserCookieBlocklist.Contains(k)) continue;
            cookieList.Add(new Dictionary<string, object?>
            {
                ["name"] = k, ["value"] = v,
                ["url"] = "https://www.douyin.com/",
            });
        }
        var addMethod = context.GetType().GetMethod("AddCookiesAsync", new[] { typeof(IEnumerable<object>) })!;
        var task = (Task)addMethod.Invoke(context, new object?[] { cookieList })!;
        task.GetAwaiter().GetResult();
    }

    private static async Task<object> NewPageAsync(dynamic context, Assembly asm)
    {
        var m = context.GetType().GetMethod("NewPageAsync", Type.EmptyTypes)!;
        var task = (Task)m.Invoke(context, null)!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static void HookResponse(dynamic page, Assembly asm, System.Collections.Concurrent.ConcurrentBag<string> bag, Action onPage)
    {
        var onMethod = page.GetType().GetMethod("On", new[] { typeof(string), typeof(Delegate) })!;
        Action<object, object> handler = (_, response) =>
        {
            try
            {
                var urlProp = response.GetType().GetProperty("Url");
                var url = (string?)urlProp?.GetValue(response);
                if (string.IsNullOrEmpty(url) || !url.Contains("/aweme/v1/web/aweme/post/")) return;
                var jsonAsync = response.GetType().GetMethod("JsonAsync");
                if (jsonAsync == null) return;
                var jt = jsonAsync.MakeGenericMethod(typeof(System.Text.Json.JsonElement));
                var jtTask = (Task)jt.Invoke(response, null)!;
                jtTask.GetAwaiter().GetResult();
                var je = (System.Text.Json.JsonElement)jtTask.GetType().GetProperty("Result")!.GetValue(jtTask)!;
                if (je.TryGetProperty("aweme_list", out var list) && list.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    onPage();
                    foreach (var item in list.EnumerateArray())
                        if (item.TryGetProperty("aweme_id", out var aid))
                            bag.Add(aid.ToString());
                }
            }
            catch { }
        };
        onMethod.Invoke(page, new object[] { "response", handler });
    }

    private static async Task GotoAsync(dynamic page, Assembly asm, string url)
    {
        var gotoOptsType = asm.GetType("Microsoft.Playwright.PageGotoOptions", true)!;
        var gotoOpts = Activator.CreateInstance(gotoOptsType)!;
        gotoOptsType.GetProperty("WaitUntil")!.SetValue(gotoOpts, Enum.Parse(gotoOptsType.GetProperty("WaitUntil")!.PropertyType, "Domcontentloaded"));
        gotoOptsType.GetProperty("Timeout")!.SetValue(gotoOpts, (float)(Math.Max(30, _settingsHolder!.Current.BrowserFallbackWaitTimeoutSeconds) * 1000));
        var m = page.GetType().GetMethod("GotoAsync", new[] { typeof(string), gotoOptsType })!;
        var task = (Task)m.Invoke(page, new object?[] { url, gotoOpts })!;
        await task;
    }

    private static bool HasVerificationTitle(dynamic page, Assembly asm)
    {
        try
        {
            var m = page.GetType().GetMethod("TitleAsync", Type.EmptyTypes)!;
            var task = (Task)m.Invoke(page, null)!;
            task.GetAwaiter().GetResult();
            var title = (string?)task.GetType().GetProperty("Result")!.GetValue(task);
            return !string.IsNullOrEmpty(title) && title.Contains("验证码");
        }
        catch { return false; }
    }

    private static async Task WheelAsync(dynamic page, Assembly asm, float dx, float dy)
    {
        var mouseProp = page.GetType().GetProperty("Mouse");
        var mouse = mouseProp!.GetValue(page)!;
        var m = mouse.GetType().GetMethod("WheelAsync", new[] { typeof(float), typeof(float) })!;
        var task = (Task)m.Invoke(mouse, new object?[] { dx, dy })!;
        await task;
    }

    private static async Task<object?> EvaluateAsync(dynamic page, Assembly asm, string script)
    {
        var m = page.GetType().GetMethod("EvaluateAsync", new[] { typeof(string), typeof(object) })!;
        var task = (Task)m.Invoke(page, new object?[] { script, null! })!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    private static void TryAsync(dynamic target, string methodName)
    {
        try
        {
            var m = target.GetType().GetMethod(methodName);
            if (m == null) return;
            var task = (Task)m.Invoke(target, null)!;
            task.GetAwaiter().GetResult();
        }
        catch { }
    }

    private static void WaitForManualVerification(dynamic page, Assembly asm, int timeoutSec)
    {
        var titleMethod = page.GetType().GetMethod("TitleAsync", Type.EmptyTypes)!;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, timeoutSec));
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var task = (Task)titleMethod.Invoke(page, null)!;
                task.GetAwaiter().GetResult();
                var title = (string?)task.GetType().GetProperty("Result")!.GetValue(task);
                if (!string.IsNullOrEmpty(title) && !title.Contains("验证码"))
                {
                    Logger.Info("验证码页面已退出,继续采集");
                    return;
                }
            }
            catch { }
            System.Threading.Thread.Sleep(1000);
        }
        Logger.Warn($"等待手动验证超时 ({timeoutSec}s)");
    }

    public void SetSettingsHolder(DownloaderSettingsStore settings)
    {
        _settingsHolder = settings;
    }

    // 在 CollectUserPostIdsAsync 入口设置 holder
    public async Task<BrowserResult> CollectAsync(string secUid, int expectedCount, CancellationToken ct)
    {
        SetSettingsHolder(_settings);
        return await CollectUserPostIdsAsync(secUid, expectedCount, ct);
    }
}
