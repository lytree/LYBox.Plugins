using Avalonia.Plugin.TDLSharp.Models;
using Microsoft.Extensions.Logging;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public static class ScriptDefinitions
{
    public static List<ScriptDescriptor> All { get; } =
    [
        new ScriptDescriptor
        {
            Id = "batch-forward",
            Name = "批量深度转发",
            Description = "将源频道/群聊的消息批量深度转发到目标频道/群聊",
            Parameters =
            [
                ScriptParameter.Text("source", "源消息链接", "源频道/群聊消息链接", required: true),
                ScriptParameter.Text("sourceId", "源消息ID", "指定源消息ID (可选)", required: false),
                ScriptParameter.Text("target", "目标链接", "目标频道/群聊链接或用户名", required: true),
                ScriptParameter.Switch("older", "向旧消息方向", "true=向旧消息转发, false=向新消息转发", true),
                ScriptParameter.Number("limit", "最大转发数量", "0=全部", 0),
                ScriptParameter.Switch("comments", "转发评论", "是否转发评论", true),
            ]
        },
        new ScriptDescriptor
        {
            Id = "clear-message",
            Name = "清理消息",
            Description = "清理频道中包含指定内容的消息",
            Parameters =
            [
                ScriptParameter.Text("channel", "频道/群聊", "频道/群聊链接或用户名 (留空=收藏夹)", required: false),
                ScriptParameter.Text("contains", "匹配文本", "匹配消息中包含的文本内容", "This channel can't be displayed"),
                ScriptParameter.Switch("silent", "静默删除", "静默删除，不询问确认", false),
                ScriptParameter.Number("limit", "最大处理数量", "0=全部", 0),
            ]
        },
        new ScriptDescriptor
        {
            Id = "forward",
            Name = "深度Copy转发",
            Description = "将频道中的浅转发消息转换为深度Copy（从原始来源重新发送副本，然后删除旧浅转发）",
            Parameters =
            [
                ScriptParameter.Text("source", "源频道", "源频道/群聊链接或用户名 (留空=收藏夹)", required: false),
                ScriptParameter.Number("limit", "最大处理数量", "0=全部", 0),
                ScriptParameter.Switch("comments", "处理评论", "是否同时处理评论中的浅转发", true),
            ]
        },
        new ScriptDescriptor
        {
            Id = "group-media-download",
            Name = "群组媒体下载",
            Description = "下载群组/频道中的媒体文件",
            Parameters =
            [
                ScriptParameter.Text("link", "消息链接", "Telegram消息链接 (多个用逗号分隔)", required: true),
                ScriptParameter.Text("output", "输出目录", "下载文件保存目录", required: false),
                ScriptParameter.Switch("includeComments", "包含评论", "是否下载评论区媒体", true),
            ]
        },
        new ScriptDescriptor
        {
            Id = "message-export",
            Name = "消息导出",
            Description = "导出频道消息为JSON (支持分组和评论)",
            Parameters =
            [
                ScriptParameter.Text("channel", "频道/群聊", "频道/群聊链接或用户名", required: true),
                ScriptParameter.Text("output", "输出路径", "输出文件路径 (留空=自动)", required: false),
                ScriptParameter.Switch("comments", "导出评论", "是否导出评论", false),
                ScriptParameter.Number("limit", "最大导出数量", "0=全部", 0),
            ]
        }
    ];
}
