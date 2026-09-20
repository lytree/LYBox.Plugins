# LYBox.Plugin.ButtonsInputs

按钮与输入控件 UI 组件演示插件。

| 项 | 值 |
|---|---|
| PluginId | `0F2F7DB6-0E9B-D872-442F-2CBC3DAC1F56` |
| PluginName | Buttons & Inputs Plugin |
| PluginAuthor | AvaloniaPlugin |
| PluginVersion | `1.0.0-preview.1` |
| MinPluginSdkVersion | `2.0.0` |
| 类型 | 原生 Avalonia 插件 |

## 演示组件

按 Pages 目录组织，每个控件一个独立页面 + ViewModel：

| 控件 | 页面 | 作用 |
|---|---|---|
| `u:AutoCompleteBox` | `AutoCompleteBoxDemo` | 自动补全输入 |
| `u:ButtonGroup` | `ButtonGroupDemo` | 按钮组（互斥/多选） |
| `u:ClassInput` | `ClassInputDemo` | 类输入器 |
| `u:EnumSelector` | `EnumSelectorDemo` | 枚举选择器 |
| `u:Form` | `FormDemo` | 表单布局容器 |
| `u:IPv4Box` | `IPv4BoxDemo` | IPv4 地址输入 |
| `u:IconButton` | `IconButtonDemo` | 图标按钮 |
| `u:KeyGestureInput` | `KeyGestureInputDemo` | 快捷键录制 |
| `u:MultiAutoCompleteBox` | `MultiAutoCompleteBoxDemo` | 多值自动补全 |
| `u:MultiComboBox` | `MultiComboBoxDemo` | 多选下拉 |
| `u:NumPad` | `NumPadDemo` | 数字小键盘 |
| `u:NumericUpDown` | `NumericUpDownDemo` | 数值步进 |
| `u:PathPicker` | `PathPickerDemo` | 路径选择器 |
| `u:PinCode` | `PinCodeDemo` | PIN 码输入 |
| `u:RangeSlider` | `RangeSliderDemo` | 范围滑块 |
| `u:Rating` | `RatingDemo` | 评分 |
| `u:SelectionList` | `SelectionListDemo` | 选择列表 |
| `u:TagInput` | `TagInputDemo` | 标签输入 |
| `u:ThemeToggler` | `ThemeTogglerDemo` | 主题切换器 |
| `u:TreeComboBox` | `TreeComboBoxDemo` | 树形下拉 |

## 主要类型

| 类型 | 命名空间 | 作用 |
|---|---|---|
| `ButtonsInputsPlugin` | `LYBox.Plugin.ButtonsInputs` | 插件入口（仅 `[GenerateMetadata]`，无 DI 注册） |

## 调试

VS Code：`Debug Plugin - Buttons & Inputs`
