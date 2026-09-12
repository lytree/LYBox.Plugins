using System.Security.Cryptography;
using System.Text;

namespace LYBox.Plugin.DouyinDownloader.Utils;

/// <summary>
/// X-Bogus 签名 — 1:1 移植自原项目 utils/xbogus.py。
/// 用于给抖音 web API URL 追加 <c>&amp;X-Bogus=...</c>。
/// </summary>
public sealed class XBogus
{
    private static readonly byte[] UaKey = { 0x00, 0x01, 0x0c };

    private static readonly int[] Array = BuildArray();
    private const string Character = "Dkdpgh4ZKsQB80/Mfvw36XI1R25-WUAlEi7NLboqYTOPuzmFjJnryx9HVGcaStCe=";

    private static int[] BuildArray()
    {
        var a = new int[79];
        for (int i = 0; i < 48; i++) a[i] = -1;
        for (int i = 48; i < 58; i++) a[i] = i - 48;
        for (int i = 58; i < 65; i++) a[i] = -1;
        for (int i = 65; i < 71; i++) a[i] = 10 + (i - 65);
        for (int i = 71; i < 79; i++) a[i] = -1;
        return a;
    }

    private readonly byte[] _userAgentBytesIso;

    public XBogus(string userAgent)
    {
        UserAgent = userAgent;
        _userAgentBytesIso = Encoding.GetEncoding("ISO-8859-1").GetBytes(userAgent);
    }

    public string UserAgent { get; }

    /// <summary>计算 X-Bogus 并返回 (signed_url, xbogus, ua)。</summary>
    public (string SignedUrl, string XBogus, string UserAgent) Build(string url)
    {
        var uaRc4 = Rc4(UaKey, _userAgentBytesIso);
        var uaB64 = Convert.ToBase64String(uaRc4);
        var uaMd5Bytes = Md5Bytes(Encoding.GetEncoding("ISO-8859-1").GetBytes(uaB64));
        var uaMd5Array = BytesToNibbles(uaMd5Bytes);

        var emptyMd5Array = BytesToNibbles(Md5Bytes(HexToBytes("d41d8cd98f00b204e9800998ecf8427e")));
        var urlMd5Array = Md5Encrypt(url);

        var timer = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        const int ct = 536919696;

        var newArray = new int[]
        {
            64,
            0x01,                     // 0.00390625 截断 → 0
            1,
            12,
            urlMd5Array[14],
            urlMd5Array[15],
            emptyMd5Array[14],
            emptyMd5Array[15],
            uaMd5Array[14],
            uaMd5Array[15],
            (timer >> 24) & 255,
            (timer >> 16) & 255,
            (timer >> 8) & 255,
            timer & 255,
            (ct >> 24) & 255,
            (ct >> 16) & 255,
            (ct >> 8) & 255,
            ct & 255,
        };

        int xor = newArray[0];
        foreach (var v in newArray.Skip(1)) xor ^= v;
        newArray = newArray.Concat(new[] { xor }).ToArray();

        var array3 = new List<int>();
        var array4 = new List<int>();
        for (int i = 0; i < newArray.Length; i++)
        {
            array3.Add(newArray[i]);
            if (i + 1 < newArray.Length) array4.Add(newArray[i + 1]);
            i++;
        }
        var merged = array3.Concat(array4).ToArray();

        var payload = Encoding.GetEncoding("ISO-8859-1").GetBytes(EncodingConcat(merged));
        var garbled = ((char)2).ToString() + ((char)255).ToString()
            + Encoding.GetEncoding("ISO-8859-1").GetString(Rc4(Encoding.GetEncoding("ISO-8859-1").GetBytes("ÿ"), payload));

        var sb = new StringBuilder();
        for (int i = 0; i < garbled.Length; i += 3)
        {
            sb.Append(Calculation((int)garbled[i], (int)garbled[i + 1], (int)garbled[i + 2]));
        }
        var xb = sb.ToString();
        return ($"{url}&X-Bogus={xb}", xb, UserAgent);
    }

    private static string EncodingConcat(int[] v)
    {
        // 与 Python _encoding_conversion 一致的字节拼接顺序:
        // payload = [a] + [int(i)] + [b, _, c, x, e, u, d, s, t, l, f, v, r, h, n, p, o]
        // v[0..18] 对应 (a, b, c, e, d, t, f, r, n, o, i, _, x, u, s, l, v, h, p)
        var sb = new StringBuilder();
        sb.Append((char)v[0]);    // a
        sb.Append((char)v[10]);   // i
        sb.Append((char)v[1]);    // b
        sb.Append((char)v[11]);   // _
        sb.Append((char)v[2]);    // c
        sb.Append((char)v[12]);   // x
        sb.Append((char)v[3]);    // e
        sb.Append((char)v[13]);   // u
        sb.Append((char)v[4]);    // d
        sb.Append((char)v[14]);   // s
        sb.Append((char)v[5]);    // t
        sb.Append((char)v[15]);   // l
        sb.Append((char)v[6]);    // f
        sb.Append((char)v[16]);   // v
        sb.Append((char)v[7]);    // r
        sb.Append((char)v[17]);   // h
        sb.Append((char)v[8]);    // n
        sb.Append((char)v[18]);   // p
        sb.Append((char)v[9]);    // o
        return sb.ToString();
    }

    private static string Calculation(int a, int b, int c)
    {
        var x3 = ((a & 255) << 16) | ((b & 255) << 8) | (c & 255);
        return new string(new[]
        {
            Character[(x3 & 16515072) >> 18],
            Character[(x3 & 258048) >> 12],
            Character[(x3 & 4032) >> 6],
            Character[x3 & 63],
        });
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var s = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        int j = 0;
        for (int ii = 0; ii < 256; ii++)
        {
            j = (j + s[ii] + key[ii % key.Length]) & 255;
            (s[ii], s[j]) = (s[j], s[ii]);
        }
        int iIdx = 0, jIdx = 0;
        var output = new byte[data.Length];
        for (int k = 0; k < data.Length; k++)
        {
            iIdx = (iIdx + 1) & 255;
            jIdx = (jIdx + s[iIdx]) & 255;
            (s[iIdx], s[jIdx]) = (s[jIdx], s[iIdx]);
            output[k] = (byte)(data[k] ^ s[(s[iIdx] + s[jIdx]) & 255]);
        }
        return output;
    }

    private static byte[] Md5Bytes(byte[] input)
    {
        using var md5 = MD5.Create();
        return md5.ComputeHash(input);
    }

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static int[] BytesToNibbles(byte[] bytes)
    {
        var arr = new int[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            arr[i * 2] = (bytes[i] >> 4) & 0xF;
            arr[i * 2 + 1] = bytes[i] & 0xF;
        }
        return arr;
    }

    private int[] Md5Encrypt(string urlPath)
    {
        // url_path: 字符串 → 拼接为字节数组 (python 行为: 长度 > 32 时直接取 ord 拼接)
        var first = Md5Bytes(Encoding.UTF8.GetBytes(urlPath));
        var firstArr = first.Length > 32 ? first.Select(b => (int)b).ToArray() : BytesToNibbles(first);
        var secondHash = Md5Bytes(firstArr.Select(i => (byte)i).ToArray());
        return BytesToNibbles(secondHash);
    }
}
