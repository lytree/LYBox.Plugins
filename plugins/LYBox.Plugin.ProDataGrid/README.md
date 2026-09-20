# LYBox.Plugin.ProDataGrid

[ProDataGrid](https://github.com/bodong1987/ProDataGrid) 高级数据表格控件演示插件。

| 项 | 值 |
|---|---|
| PluginId | `0F2F7DB6-0E9B-D872-442F-2CBC3DAC1FA1` |
| PluginName | ProDataGrid Plugin |
| PluginAuthor | LYBoxPlugin |
| PluginVersion | `1.0.0-preview.1` |
| MinPluginSdkVersion | `2.0.0` |
| 类型 | 原生 Avalonia 插件 |

## 演示页面

| 页面 | 作用 |
|---|---|
| `BasicDataGridDemo` | 基础表格 + 列绑定 |
| `ColumnTypesDemo` | 列类型演示（文本/数值/日期/枚举/布尔/进度条/模板列） |
| `CustomDrawingEditingPage` | Skia 自定义绘制 + 编辑 |
| `CustomDrawingLiveUpdatesPage` | Skia 实时动画单元格绘制 |
| `DragDropDemo` | 拖拽排序与外部文件拖入 |
| `EditingDemoPage` | 行编辑（Alt+Click / DoubleClick 两种交互模型） |
| `FilteringGroupingDemo` | 过滤与分组 |
| `FormulaDataGridDemo` | 公式列（Excel 兼容公式引擎） |
| `RowDetailsSelectionDemo` | 行详情面板 + 多选模式 |

## 主要类型

| 类型 | 命名空间 | 作用 |
|---|---|---|
| `ProDataGridPlugin` | `LYBox.Plugin.ProDataGrid` | 插件入口（仅 `[GenerateMetadata]`） |
| `TestData` | `LYBox.Plugin.ProDataGrid` | 测试数据工厂 |
| `ExportHelper` | `LYBox.Plugin.ProDataGrid` | 数据导出助手 |
| `ObservableCollectionExtensions` | `LYBox.Plugin.ProDataGrid` | `ObservableCollection<T>` 扩展 |
| `Employee` / `Person` / `Product` / `TaskItem` | `LYBox.Plugin.ProDataGrid.Models` | 演示数据模型 |
| `EditingDemoRow` | 同上 | 编辑演示行模型 |
| `CustomDrawingEditingRow` | 同上 | 自定义绘制行模型 |
| `AltClickEditingInteractionModel` | `LYBox.Plugin.ProDataGrid.EditingModels` | Alt+Click 编辑交互 |
| `DoubleClickOnlyEditingInteractionModel` | 同上 | 仅双击编辑交互 |
| `SkiaTextCellDrawOperationFactory` | `LYBox.Plugin.ProDataGrid.CustomDrawing` | Skia 文本绘制工厂 |
| `SkiaAnimatedTextCellDrawOperationFactory` | 同上 | Skia 动画文本绘制工厂 |

## 关键依赖

| 包 | 版本 |
|---|---|
| `ProDataGrid` | `12.0.4` |
| `SkiaSharp` | `3.119.4` |
| `LYBox.Plugin.Generators` | `$(PluginSdkVersion)` |
| `LYBox.Plugin.Shared` | `$(PluginSdkVersion)` |

## 数据导出

`ExportHelper` 演示如何导出表格数据到 `Data/{PluginId}/Exports/`（通过 `IPluginDataDirectoryProvider` 解析）。

## 调试

VS Code：`Debug Plugin - ProDataGrid`
