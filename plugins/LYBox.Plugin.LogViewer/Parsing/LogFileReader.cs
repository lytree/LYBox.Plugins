using System.Runtime.InteropServices;
using System.Text;

namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>
/// 增量日志文件读取器：按字节切分（<c>\n</c> 不会出现在 UTF-8 / GBK 多字节序列内部），
/// 每消费一行就更新 <see cref="Offset"/>，因此可以在「实时跟踪」时从上次位置继续读取。
/// UTF-16 文件走整文件解码路径（偏移不精确，且不支持增量跟踪）。
/// </summary>
public sealed class LogFileReader : IDisposable
{
    private const int BufferSize = 128 * 1024;
    private const int MaxLineBytes = 1024 * 1024;

    private readonly FileStream _stream;
    private readonly Encoding _encoding;
    private readonly byte[] _buffer = new byte[BufferSize];
    private readonly List<byte> _lineBytes = new(1024);
    private readonly long _initialOffset;
    private readonly bool _wideMode;

    private List<string>? _wideLines;
    private int _wideLineIndex;

    private int _bufferLength;
    private int _bufferPosition;
    private long _totalRead;
    private bool _eof;

    public LogFileReader(string path, long offset = 0, int lineNumber = 0)
    {
        _stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.SequentialScan);

        _encoding = DetectEncoding(_stream, out var wideMode);
        _wideMode = wideMode;
        LineNumber = lineNumber;
        Offset = Math.Clamp(offset, 0, _stream.Length);
        _initialOffset = Offset;

        _stream.Seek(Offset, SeekOrigin.Begin);

        if (_wideMode)
        {
            LoadWideLines();
        }
    }

    /// <summary>已消费到的字节偏移。</summary>
    public long Offset { get; private set; }

    /// <summary>已消费到的行号（1 基）。</summary>
    public int LineNumber { get; private set; }

    public bool SupportsIncremental => !_wideMode;

    /// <summary>
    /// 读取最多 <paramref name="maxLines"/> 行追加到 <paramref name="into"/>。
    /// <paramref name="flushPartialLine"/> 为 true 时把文件末尾没有换行符的最后一行也输出
    /// （仅在首次完整加载时使用，避免实时跟踪重复读半行）。
    /// </summary>
    public void ReadLines(List<RawLine> into, int maxLines, bool flushPartialLine = false)
    {
        if (_wideMode)
        {
            ReadWideLines(into, maxLines);
            return;
        }

        while (into.Count < maxLines)
        {
            if (_bufferPosition >= _bufferLength && !Fill())
            {
                break;
            }

            var value = _buffer[_bufferPosition++];

            if (value == (byte)'\n')
            {
                EmitLine(into);
                continue;
            }

            if (value == (byte)'\r')
            {
                continue;
            }

            _lineBytes.Add(value);

            if (_lineBytes.Count >= MaxLineBytes)
            {
                EmitLine(into);
            }
        }

        if (flushPartialLine && _eof && _lineBytes.Count > 0 && into.Count < maxLines)
        {
            EmitLine(into);
        }
    }

    private bool Fill()
    {
        if (_eof)
        {
            return false;
        }

        _bufferLength = _stream.Read(_buffer, 0, _buffer.Length);
        _bufferPosition = 0;
        _totalRead += _bufferLength;

        if (_bufferLength == 0)
        {
            _eof = true;
            return false;
        }

        return true;
    }

    private void EmitLine(List<RawLine> into)
    {
        var text = _lineBytes.Count == 0
            ? string.Empty
            : _encoding.GetString(CollectionsMarshal.AsSpan(_lineBytes));

        _lineBytes.Clear();
        LineNumber++;
        into.Add(new RawLine(LineNumber, text));

        // 已消费位置 = 起始偏移 + 已读入总字节 - 缓冲区中尚未消费的字节。
        Offset = _initialOffset + _totalRead - (_bufferLength - _bufferPosition);
    }

    private void LoadWideLines()
    {
        var bytes = new byte[_stream.Length];
        _stream.Seek(0, SeekOrigin.Begin);
        var read = 0;

        while (read < bytes.Length)
        {
            var chunk = _stream.Read(bytes, read, bytes.Length - read);
            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        var text = _encoding.GetString(bytes, 0, read);
        _wideLines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        _wideLineIndex = 0;
    }

    private void ReadWideLines(List<RawLine> into, int maxLines)
    {
        if (_wideLines is null)
        {
            return;
        }

        while (into.Count < maxLines && _wideLineIndex < _wideLines.Count)
        {
            LineNumber++;
            into.Add(new RawLine(LineNumber, _wideLines[_wideLineIndex++]));
        }

        if (_wideLineIndex >= _wideLines.Count)
        {
            Offset = _stream.Length;
        }
    }

    private static Encoding DetectEncoding(FileStream stream, out bool wideMode)
    {
        var head = new byte[4];
        var read = stream.Read(head, 0, head.Length);
        stream.Seek(0, SeekOrigin.Begin);

        wideMode = false;

        if (read >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        if (read >= 2 && head[0] == 0xFF && head[1] == 0xFE)
        {
            wideMode = true;
            return Encoding.Unicode;
        }

        if (read >= 2 && head[0] == 0xFE && head[1] == 0xFF)
        {
            wideMode = true;
            return Encoding.BigEndianUnicode;
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    public void Dispose() => _stream.Dispose();
}
