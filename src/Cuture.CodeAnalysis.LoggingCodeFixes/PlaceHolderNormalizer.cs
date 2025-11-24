using System.Buffers;
using System.Text.RegularExpressions;

namespace Cuture.CodeAnalysis.LoggingCodeFixes;

/// <summary>
/// 占位符规范化器
/// </summary>
public class PlaceHolderNormalizer
{
    #region Private 字段

    private static readonly Regex s_normalizeRegex = new(@"\?|\.|\(.*?\)|\[.*?\]", RegexOptions.Compiled);

    #endregion Private 字段

    #region Public 方法

    /// <summary>
    /// 创建规范化的占位符
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Normalize(string input)
    {
        input = input.Trim()
                     .TrimStart('{')
                     .TrimStart('@')
                     .TrimStart('_')
                     .TrimEnd('}')
                     .Trim();

        var segments = s_normalizeRegex.Split(input)
                                       .Where(static m => !string.IsNullOrWhiteSpace(m) && !string.Equals("ToString", m, StringComparison.Ordinal))
                                       .ToList();

        if (segments.Count < 3)    //两个或一个分段，只取最后的分段
        {
            return NormalizeSegment(segments[segments.Count - 1]);
        }

        //超过三个分段只取最后两个
        segments.RemoveAll(static m => string.Equals("this", m, StringComparison.Ordinal) || string.Equals("This", m, StringComparison.Ordinal));

        if (segments.Count > 1)
        {
            return $"{NormalizeSegment(segments[segments.Count - 2])}{NormalizeSegment(segments[segments.Count - 1])}";
        }
        return NormalizeSegment(segments[segments.Count - 1]);
    }

    #endregion Public 方法

    #region Private 方法

    private static string NormalizeSegment(string value)
    {
        var span = value.AsSpan().Trim('@');
        //以get开始，则去掉get
        if (span.StartsWith("Get", StringComparison.OrdinalIgnoreCase)
            && span.Length > 3)
        {
            span = span.Slice(3);
        }
        return FirstCharToUpper(span);

        static string FirstCharToUpper(ReadOnlySpan<char> input)
        {
            var bufferLength = 0;
            var buffer = ArrayPool<char>.Shared.Rent(input.Length + 4);
            try
            {
                Span<char> newSpan = buffer;
                var firstChar = input[0];
                newSpan[bufferLength++] = char.ToUpper(firstChar);
                input.Slice(1).CopyTo(newSpan.Slice(bufferLength));
                bufferLength += input.Length - 1;

                return newSpan.Slice(0, bufferLength).ToString();
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
    }

    #endregion Private 方法
}
