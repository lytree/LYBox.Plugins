# LYBox.Plugin.ScottPlot

[ScottPlot](https://github.com/ScottPlot/ScottPlot) 图表绘制控件演示插件。

| 项 | 值 |
|---|---|
| PluginId | `0F2F7DB6-0E9B-D872-442F-2CBC3DAC1FA0` |
| PluginName | ScottPlot Plugin |
| PluginAuthor | LYBoxPlugin |
| PluginVersion | `1.0.0-preview.1` |
| MinPluginSdkVersion | `2.0.0` |
| 类型 | 原生 Avalonia 插件 |

## 演示页面

| 页面 | 作用 |
|---|---|
| `QuickStartDemo` | 快速入门：单序列折线 |
| `BarChartDemo` | 柱状图 |
| `ScatterPlotDemo` | 散点图 |
| `SignalPlotDemo` | 信号图（大数据量） |
| `WaveformSpectrumTrendPage` | 波形 / 频谱 / 趋势综合 |

## 主要类型

| 类型 | 命名空间 | 作用 |
|---|---|---|
| `ScottPlotPlugin` | `LYBox.Plugin.ScottPlot` | 插件入口（仅 `[GenerateMetadata]`） |
| `PlotView` | `LYBox.Plugin.ScottPlot.Controls` | Avalonia ScottPlot 控件封装 |
| `PlotMenu` | 同上 | 图表右键菜单 |
| `PlotExtensions` | 同上 | 图表扩展方法 |
| `BarChartDemoViewModel` | `LYBox.Plugin.ScottPlot.ViewModels` | 柱状图演示 VM |

## 关键依赖

| 包 | 版本 |
|---|---|
| `ScottPlot` | `5.1.59` |
| `SkiaSharp` | `3.119.4` |
| `LYBox.Plugin.Generators` | `$(PluginSdkVersion)` |
| `LYBox.Plugin.Shared` | `$(PluginSdkVersion)` |

## 调试

VS Code：`Debug Plugin - ScottPlot`
