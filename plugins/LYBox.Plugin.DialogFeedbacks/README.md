# LYBox.Plugin.DialogFeedbacks

对话框与用户反馈 UI 组件演示插件。

| 项 | 值 |
|---|---|
| PluginId | `0F2F7DB6-0E9B-D872-442F-2CBC3DAC1F59` |
| PluginName | Dialog & Feedback Plugin |
| PluginAuthor | LYBoxPlugin |
| PluginVersion | `1.0.0-preview.1` |
| MinPluginSdkVersion | `2.0.0` |
| 类型 | 原生 Avalonia 插件 |

## 演示组件

| 组件 | 页面 | 作用 |
|---|---|---|
| `u:Drawer` | `DrawerDemo` | 抽屉 |
| `u:Loading` | `LoadingDemo` | 加载指示 |
| `u:OverlayMessageBox` | `MessageBoxDemo` | 消息框 |
| `u:Notification` | `NotificationDemo` | 通知 |
| `u:Dialog` (overlay) | `OverlayDialogDemo` | 覆盖层对话框 |
| `u:PopConfirm` | `PopConfirmDemo` | 弹出确认 |
| `u:Skeleton` | `SkeletonDemo` | 骨架屏 |
| `u:Toast` | `ToastDemo` | Toast 轻提示 |
| `u:Dialog` (window) | `WindowDialogDemo` | 窗口对话框 |

## 自定义对话框

| 文件 | 作用 |
|---|---|
| `Dialogs/CustomDemoDialog.axaml` + `.cs` | 自定义内容对话框示例 |
| `Dialogs/DefaultDemoDialog.axaml` + `.cs` | 默认对话框示例 |
| `Dialogs/CustomDemoDialogViewModel.cs` | 自定义对话框 VM |
| `Dialogs/DefaultDemoDialogViewModel.cs` | 默认对话框 VM |

## 主要类型

| 类型 | 命名空间 | 作用 |
|---|---|---|
| `DialogFeedbacksPlugin` | `LYBox.Plugin.DialogFeedbacks` | 插件入口（仅 `[GenerateMetadata]`） |

## 调试

VS Code：`Debug Plugin - DialogFeedbacks`
