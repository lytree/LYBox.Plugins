using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.ViteSample.Rpc;

/// <summary>
/// 示例 RPC 命令。前端通过源生成器产出的强类型客户端调用：
///   import { createSampleCommandsClient } from './.lybox/SampleCommands.client';
///   const client = createSampleCommandsClient(transport);
///   await client.greet({ name: 'world' });      // → GreetAsync
///   await client.add({ left: 1, right: 2 });    // → AddAsync
///   await client.getSampleInfo();               // → GetSampleInfoAsync
/// </summary>
public partial class SampleCommands
{
    /// <summary>基础问候：返回 Hello, {name}! 包含时间戳便于观察实时性。</summary>
    [RpcCommand]
    public Task<string> GreetAsync(GreetRequest request, CancellationToken ct)
        => Task.FromResult($"Hello, {request.Name}! 来自 ViteSample 的问候 @ {DateTime.Now:HH:mm:ss}");

    /// <summary>整数加法：演示多参数 DTO。</summary>
    [RpcCommand]
    public Task<int> AddAsync(AddRequest request, CancellationToken ct)
        => Task.FromResult(request.Left + request.Right);

    /// <summary>插件元信息：演示 record 强类型返回。</summary>
    [RpcCommand]
    public Task<SampleInfo> GetSampleInfoAsync(CancellationToken ct)
        => Task.FromResult(new SampleInfo(
            PluginId: "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
            Name: "Vite Sample",
            Version: "1.0.0-preview.1",
            ServerTime: DateTimeOffset.Now));
}

public sealed record GreetRequest(string Name);
public sealed record AddRequest(int Left, int Right);
public sealed record SampleInfo(string PluginId, string Name, string Version, DateTimeOffset ServerTime);