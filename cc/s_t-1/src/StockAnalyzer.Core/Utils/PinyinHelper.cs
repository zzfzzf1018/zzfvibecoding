using System.Text;

namespace StockAnalyzer.Core.Utils;

/// <summary>
/// 汉字拼音首字母提取（基于 GB2312 区位码分段），用于「gzmt → 贵州茅台」式模糊检索。
/// </summary>
public static class PinyinHelper
{
    private static readonly int[] SectionBoundaries =
    {
        1601, 1637, 1833, 2078, 2274, 2302, 2433, 2594, 2787,
        3106, 3212, 3472, 3635, 3722, 3730, 3858, 4027, 4086,
        4390, 4558, 4684, 4925, 5249, 5590
    };

    private static readonly char[] Letters =
    {
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
        'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'W', 'X', 'Y', 'Z'
    };

    /// <summary>
    /// 区位码分段法只能取到多音字的默认读音，这里为证券名称中高频出现的字做修正。
    /// 例：“行”在股票名称中几乎总是“银行(háng)”，而默认读音为 xíng。
    /// </summary>
    private static readonly Dictionary<char, char> Overrides = new()
    {
        ['行'] = 'H',
        ['藏'] = 'Z',
        ['厦'] = 'X'
    };

    private static Encoding? _gb2312;

    static PinyinHelper()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _gb2312 = Encoding.GetEncoding("GB2312");
        }
        catch (Exception)
        {
            _gb2312 = null;
        }
    }

    /// <summary>提取字符串的拼音首字母（大写）。非汉字字符原样保留（字母大写，其余忽略）。</summary>
    public static string GetInitials(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);

        foreach (char ch in text)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
                continue;
            }

            char? initial = GetCharInitial(ch);
            if (initial.HasValue)
            {
                builder.Append(initial.Value);
            }
        }

        return builder.ToString();
    }

    private static char? GetCharInitial(char ch)
    {
        if (Overrides.TryGetValue(ch, out char mapped))
        {
            return mapped;
        }

        if (_gb2312 is null || ch < 0x4E00 || ch > 0x9FA5)
        {
            return null;
        }

        byte[] bytes = _gb2312.GetBytes(ch.ToString());
        if (bytes.Length < 2)
        {
            return null;
        }

        int sectionCode = (bytes[0] - 160) * 100 + (bytes[1] - 160);

        if (sectionCode < SectionBoundaries[0] || sectionCode >= SectionBoundaries[^1])
        {
            return null;
        }

        for (int i = 0; i < Letters.Length; i++)
        {
            if (sectionCode >= SectionBoundaries[i] && sectionCode < SectionBoundaries[i + 1])
            {
                return Letters[i];
            }
        }

        return null;
    }
}
