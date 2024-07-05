using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cuture.CodeAnalysis.LoggingCodeFixes;

public static class LoggingInvocationFixer
{
    #region Private 字段

    private static readonly Regex s_ignoreRegex = new("\\.|\\(.*?\\)|\\[.*?\\]|this\\.|This\\.", RegexOptions.Compiled);

    private static readonly Regex s_numericPlaceHolderRegex = new("\\{[\\d ]+\\}", RegexOptions.Compiled);

    private static readonly Regex s_placeHolderRegex = new("(?<=\\{)(.+?)(?=\\})", RegexOptions.Compiled);

    #endregion Private 字段

    #region Public 方法

    public static async Task<Document> FixAsStructuredLoggingAsync(Document document, InvocationExpressionSyntax logInvocationExpressionSyntax, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        var newLogInvocationExpressionSyntax = logInvocationExpressionSyntax;

        var logArguments = logInvocationExpressionSyntax.ArgumentList.Arguments;

        for (var argumentIndex = 0; argumentIndex < logArguments.Count; argumentIndex++)
        {
            var argumentSyntax = logArguments[argumentIndex];
            switch (argumentSyntax.Expression)
            {
                case InterpolatedStringExpressionSyntax interpolatedStringExpressionSyntax:
                    var (invocationExpressionSyntax, variableExpressions) = ProcessWithInterpolatedStringExpressionSyntax(newLogInvocationExpressionSyntax, interpolatedStringExpressionSyntax);

                    newLogInvocationExpressionSyntax = invocationExpressionSyntax;

                    if (argumentIndex == 0) //首参数是内插字符串，检查是否有 Exception
                    {
                        ExpressionSyntax? exceptionVariableExpression = null;
                        foreach (var variableExpression in variableExpressions)
                        {
                            var typeInfo = semanticModel.GetTypeInfo(variableExpression, cancellationToken);
                            var typeSymbol = typeInfo.Type;
                            var isException = false;
                            while (typeSymbol.BaseType is not null)
                            {
                                if (SymbolEqualityComparer.Default.Equals(typeSymbol, semanticModel.Compilation.GetTypeByMetadataName("System.Exception")))
                                {
                                    isException = true;
                                    break;
                                }
                                typeSymbol = typeSymbol.BaseType;
                            }
                            if (isException)
                            {
                                exceptionVariableExpression = variableExpression;
                                break;
                            }
                        }

                        if (exceptionVariableExpression is not null)    //将异常插入到第一个参数
                        {
                            var argumentSyntaxes = newLogInvocationExpressionSyntax.ArgumentList.Arguments.Insert(0, SyntaxFactory.Argument(exceptionVariableExpression));
                            newLogInvocationExpressionSyntax = newLogInvocationExpressionSyntax.WithArgumentList(SyntaxFactory.ArgumentList(argumentSyntaxes));
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = oldRoot.ReplaceNode(logInvocationExpressionSyntax, newLogInvocationExpressionSyntax);

        return document.WithSyntaxRoot(newRoot);
    }

    public static async Task<Document> FixNumericPlaceHolderAsync(Document document, SyntaxToken syntaxToken, CancellationToken cancellationToken)
    {
        var syntaxNode = syntaxToken.Parent;

        var stringArgument = syntaxNode.Parent.AncestorsAndSelf().OfType<ArgumentSyntax>().First();

        if (syntaxNode is LiteralExpressionSyntax expressionSyntax)
        {
            var invocationExpressionSyntax = expressionSyntax.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

            var arguments = invocationExpressionSyntax.ArgumentList.Arguments.ToList();
            arguments = arguments.SkipWhile(m => !ReferenceEquals(stringArgument, m)).Skip(1).ToList();
            var text = syntaxToken.ValueText;

            var index = 0;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            text = s_numericPlaceHolderRegex.Replace(text, match =>
            {
                var argumet = arguments[index++];
                var result = match.Value;
                switch (argumet.Expression)
                {
                    case InvocationExpressionSyntax invocationExpressionSyntax:
                        switch (invocationExpressionSyntax.Expression)
                        {
                            case IdentifierNameSyntax invocationIdentifierNameSyntax:
                                result = GetPlaceHolderByIdentifierNameSyntax(invocationIdentifierNameSyntax) ?? match.Value;
                                break;

                            case MemberAccessExpressionSyntax invocationMemberAccessExpressionSyntax:
                                result = CreatePlaceHolderName(invocationMemberAccessExpressionSyntax.ToString()) ?? match.Value;
                                break;
                        }
                        break;

                    case IdentifierNameSyntax identifierNameSyntax:
                        result = GetPlaceHolderByIdentifierNameSyntax(identifierNameSyntax) ?? match.Value;
                        break;

                    case LiteralExpressionSyntax literalExpressionSyntax:
                        result = CreatePlaceHolderName(semanticModel.GetTypeInfo(literalExpressionSyntax).Type.Name) ?? match.Value;
                        break;

                    case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                        result = CreatePlaceHolderName(memberAccessExpressionSyntax.ToString()) ?? match.Value;
                        break;
                }
                return $"{{{result}}}";
            });

            var newExpressionSyntax = CreateLiteralExpressionSyntax($"\"{text}\"");

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(expressionSyntax, newExpressionSyntax);

            return document.WithSyntaxRoot(newRoot);
        }

        //暂不支持修改
        return document;
    }

    public static async Task<Document> FixPascalCasePlaceHolderAsync(Document document, SyntaxToken syntaxToken, CancellationToken cancellationToken)
    {
        var syntaxNode = syntaxToken.Parent;
        if (syntaxNode is LiteralExpressionSyntax expressionSyntax)
        {
            var text = syntaxToken.ValueText;

            text = s_placeHolderRegex.Replace(text, match =>
            {
                return CreatePlaceHolderName(match.Value);
            });

            var newExpressionSyntax = CreateLiteralExpressionSyntax($"\"{text}\"");

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(expressionSyntax, newExpressionSyntax);

            return document.WithSyntaxRoot(newRoot);
        }

        //暂不支持修改
        return document;
    }

    #endregion Public 方法

    #region Private 方法

    private static InterpolatedStringTextSyntax CreateHolderInterpolatedStringTextSyntax(string valueText)
    {
        return CreateInterpolatedStringTextSyntax($"{{{{{CreatePlaceHolderName(valueText)}}}}}");
    }

    private static string CreatePlaceHolderName(string valueText)
    {
        var valueSpan = valueText.AsSpan()
                                 .Trim()
                                 .TrimStart('{')
                                 .TrimStart('@')
                                 .TrimStart('_')
                                 .TrimEnd('}')
                                 .Trim();

        var index = 0;
        Span<char> newSpan = new char[valueSpan.Length + 4];
        var firstChar = valueSpan[0];
        newSpan[index++] = char.IsLetter(firstChar) ? char.ToUpper(firstChar) : firstChar;
        valueSpan.Slice(1).CopyTo(newSpan.Slice(index));
        index += valueSpan.Length - 1;

        return s_ignoreRegex.Replace(newSpan.Slice(0, index).ToString(), "");
    }

    private static IEnumerable<InterpolatedStringContentSyntaxDescriptor> EnumerateProcessedInterpolatedStringContentSyntaxes(IEnumerable<InterpolatedStringContentSyntax> contentSyntaxes)
    {
        foreach (var contentSyntax in contentSyntaxes)
        {
            if (contentSyntax is not InterpolationSyntax interpolationSyntax)
            {
                yield return contentSyntax;
                continue;
            }

            switch (interpolationSyntax.Expression)
            {
                case LiteralExpressionSyntax literalExpressionSyntax:
                    yield return CreateInterpolatedStringTextSyntax(literalExpressionSyntax.Token.ValueText);
                    break;

                case InvocationExpressionSyntax invocationExpressionSyntax:
                    switch (invocationExpressionSyntax.Expression)
                    {
                        case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                            var memberAccessExpression = memberAccessExpressionSyntax.Expression;
                            if (memberAccessExpressionSyntax.Name.Identifier.ValueText == "ToString")   //ToString
                            {
                                if (invocationExpressionSyntax.ArgumentList.Arguments.Count == 0)   //无参ToString()
                                {
                                    yield return (CreateHolderInterpolatedStringTextSyntax(memberAccessExpression.ToString()), CreateArgumentSyntax(memberAccessExpression), memberAccessExpression);
                                }
                                else   //有参ToString()
                                {
                                    yield return (CreateHolderInterpolatedStringTextSyntax(memberAccessExpression.ToString()), CreateArgumentSyntax(invocationExpressionSyntax), memberAccessExpression);
                                }
                            }
                            else
                            {
                                yield return (CreateHolderInterpolatedStringTextSyntax(memberAccessExpressionSyntax.ToString()), CreateArgumentSyntax(invocationExpressionSyntax), memberAccessExpression);
                            }
                            break;

                        case IdentifierNameSyntax identifierNameSyntax:
                            if (identifierNameSyntax.Identifier.ValueText == "nameof")
                            {
                                yield return contentSyntax;
                            }
                            else
                            {
                                yield return (contentSyntax, identifierNameSyntax);
                            }
                            break;
                    }
                    break;

                case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                    {
                        yield return (CreateHolderInterpolatedStringTextSyntax(memberAccessExpressionSyntax.ToString()), CreateArgumentSyntax(memberAccessExpressionSyntax), memberAccessExpressionSyntax.Expression);
                    }
                    break;

                default:
                    yield return (CreateHolderInterpolatedStringTextSyntax(interpolationSyntax.ToString()), CreateArgumentSyntax(interpolationSyntax.Expression));
                    break;
            }
        }
    }

    private static string? GetPlaceHolderByIdentifierNameSyntax(IdentifierNameSyntax identifierNameSyntax)
    {
        if (identifierNameSyntax.Identifier.ValueText == "nameof")
        {
            if (identifierNameSyntax.Parent is InvocationExpressionSyntax invocationExpressionSyntax)
            {
                var text = invocationExpressionSyntax.ArgumentList.Arguments[0].Expression.ToString();
                return CreatePlaceHolderName(text);
            }
        }
        else
        {
            return CreatePlaceHolderName(identifierNameSyntax.Identifier.ValueText);
        }
        return null;
    }

    private static (InvocationExpressionSyntax InvocationExpressionSyntax, IEnumerable<ExpressionSyntax> VariableExpressions) ProcessWithInterpolatedStringExpressionSyntax(InvocationExpressionSyntax targetLogInvocationExpressionSyntax,
                                                                                                                                                                            InterpolatedStringExpressionSyntax interpolatedStringExpressionSyntax)
    {
        var contentSyntaxDescriptors = EnumerateProcessedInterpolatedStringContentSyntaxes(interpolatedStringExpressionSyntax.Contents).ToList();

        var contents = contentSyntaxDescriptors.Select(m => m.InterpolatedStringContentSyntax).ToList();

        var hasInterpolation = contents.Any(m => m is InterpolationSyntax);
        if (!hasInterpolation)   //没有内插内容，去除内插
        {
            for (var i = 0; i < interpolatedStringExpressionSyntax.Contents.Count; i++)
            {
                var newItem = contents[i] as InterpolatedStringTextSyntax;
                if (newItem is not null
                    && newItem.TextToken.ValueText is { } valueText
                    && valueText.StartsWith("{{")) //有修改
                {
                    contents[i] = CreateInterpolatedStringTextSyntax(valueText.Substring(1, valueText.Length - 2).ToString());
                }
            }
        }

        var contentSyntaxList = new SyntaxList<InterpolatedStringContentSyntax>(contents);

        SyntaxNode newLogStringSyntax = interpolatedStringExpressionSyntax.WithContents(contentSyntaxList);

        if (!hasInterpolation)  //去除内插符号
        {
            newLogStringSyntax = CreateLiteralExpressionSyntax(newLogStringSyntax.ToString().TrimStart('$'));
        }

        var appendArgumentSyntaxes = contentSyntaxDescriptors.Where(m => m.Argument is not null)
                                                             .Select(m => m.Argument)
                                                             .ToArray();

        targetLogInvocationExpressionSyntax = targetLogInvocationExpressionSyntax.ReplaceNode(interpolatedStringExpressionSyntax, newLogStringSyntax)
                                                                                 .AddArgumentListArguments(appendArgumentSyntaxes);

        return (targetLogInvocationExpressionSyntax, contentSyntaxDescriptors.Where(m => m.VariableExpression is not null).Select(m => m.VariableExpression!));
    }

    #endregion Private 方法

    #region CreateSyntax

    private static ArgumentSyntax CreateArgumentSyntax(ExpressionSyntax expression)
    {
        return SyntaxFactory.Argument(expression);
    }

    private static InterpolatedStringTextSyntax CreateInterpolatedStringTextSyntax(string valueText)
    {
        var token = SyntaxFactory.Token(default, SyntaxKind.InterpolatedStringTextToken, valueText, valueText, default);
        return SyntaxFactory.InterpolatedStringText(token);
    }

    private static LiteralExpressionSyntax CreateLiteralExpressionSyntax(string valueText)
    {
        var token = SyntaxFactory.Token(default, SyntaxKind.StringLiteralToken, valueText, valueText, default);
        return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, token);
    }

    #endregion CreateSyntax
}
