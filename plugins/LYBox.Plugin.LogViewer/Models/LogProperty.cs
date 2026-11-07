namespace LYBox.Plugin.LogViewer.Models;

/// <summary>
/// 一条结构化属性。Serilog 的 <c>Properties</c>、顶层 Enrich 属性、CSV 的额外列
/// 都会归一化成这个模型，便于在详情面板里逐条展示与检索。
/// </summary>
public sealed record LogProperty(string Name, string Value)
{
    public string Display => $"{Name} = {Value}";
}
