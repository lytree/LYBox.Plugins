namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>从文件读出的原始文本行（附带行号）。</summary>
public readonly record struct RawLine(int Number, string Text);
