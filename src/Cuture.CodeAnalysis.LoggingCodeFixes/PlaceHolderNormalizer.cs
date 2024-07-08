using System.Buffers;
using System.Text.RegularExpressions;

namespace Cuture.CodeAnalysis.LoggingCodeFixes;

/// <summary>
/// 占位符规范化器
/// </summary>
public class PlaceHolderNormalizer
{
    #region Private 字段

    private static readonly Regex s_normalizeRegex = new("\\?|\\.|\\(.*?\\)|\\[.*?\\]|this\\.|This\\.|@", RegexOptions.Compiled);

    private static readonly char[] s_shouldUpperFollowChars = ['.', '@'];

    private static ReadOnlySpan<char> ShouldUpperFollowChars => s_shouldUpperFollowChars;

    #endregion Private 字段

    #region Public 方法

    /// <summary>
    /// 创建规范化的占位符
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Normalize(string input) => Normalize(input.AsSpan());

    /// <summary>
    /// 创建规范化的占位符
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Normalize(ReadOnlySpan<char> input)
    {
        input = input.Trim()
                     .TrimStart('{')
                     .TrimStart('@')
                     .TrimStart('_')
                     .TrimEnd('}')
                     .Trim();

        var bufferLength = 0;
        var buffer = ArrayPool<char>.Shared.Rent(input.Length + 4);
        try
        {
            Span<char> newSpan = buffer;
            var firstChar = input[0];
            newSpan[bufferLength++] = char.ToUpper(firstChar);
            input.Slice(1).CopyTo(newSpan.Slice(bufferLength));
            bufferLength += input.Length - 1;

            var index = 0;
            while (newSpan.Slice(index).IndexOfAny(ShouldUpperFollowChars) is { } nextIndex
                   && nextIndex >= 0)
            {
                nextIndex = nextIndex + index + 1;
                if (nextIndex >= newSpan.Length)
                {
                    break;
                }

                newSpan[nextIndex] = char.ToUpper(newSpan[nextIndex]);
                index = nextIndex;
            }

            return s_normalizeRegex.Replace(newSpan.Slice(0, bufferLength).ToString(), "");
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    #endregion Public 方法
}
