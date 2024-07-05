using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cuture.CodeAnalysis.LoggingCodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LoggingCodeFixesProvider)), Shared]
public class LoggingCodeFixesProvider : CodeFixProvider
{
    #region Public 属性

    public override sealed ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CA1727", "CA2253", "CA2254");

    #endregion Public 属性

    #region Public 方法

    public override sealed FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override sealed async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case "CA1727":  //占位符应该为Pascal命名
                    await RegisterPascalCaseCodeFixAsync(context, diagnostic).ConfigureAwait(false);
                    break;

                case "CA2253":  //占位符为纯数字
                    await RegisterNumericPlaceHolderCodeFixAsync(context, diagnostic).ConfigureAwait(false);
                    break;

                case "CA2254":  //内插字符串日志
                    await RegisterFixAsStructuredLoggingCodeFixAsync(context, diagnostic).ConfigureAwait(false);
                    break;
            }
        }
    }

    #endregion Public 方法

    #region Private 方法

    private async Task RegisterFixAsStructuredLoggingCodeFixAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var invocationExpressionSyntax = syntaxRoot.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

        //前三个表达式必须为标识符表达式或内插字符串表达式
        if (invocationExpressionSyntax.ArgumentList.Arguments.Take(3).All(m => m.Expression is IdentifierNameSyntax or InterpolatedStringExpressionSyntax))
        {
            var codeAction = CodeAction.Create(title: "修正结构化日志",
                                               createChangedDocument: cancellationToken => LoggingInvocationFixer.FixAsStructuredLoggingAsync(context.Document, invocationExpressionSyntax, cancellationToken),
                                               equivalenceKey: "FixAsStructuredLogging");
            context.RegisterCodeFix(codeAction, diagnostic);
        }
    }

    private async Task RegisterNumericPlaceHolderCodeFixAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var syntaxToken = syntaxRoot.FindToken(diagnosticSpan.Start);

        var kind = syntaxToken.Kind();
        if (kind is SyntaxKind.StringLiteralToken)
        {
            var codeAction = CodeAction.Create(title: "修正日志数字占位符",
                                               createChangedDocument: cancellationToken => LoggingInvocationFixer.FixNumericPlaceHolderAsync(context.Document, syntaxToken, cancellationToken),
                                               equivalenceKey: "FixNumericPlaceHolder");
            context.RegisterCodeFix(codeAction, diagnostic);
        }
    }

    private async Task RegisterPascalCaseCodeFixAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var syntaxToken = syntaxRoot.FindToken(diagnosticSpan.Start);

        var codeAction = CodeAction.Create(title: "修正日志Pascal占位符",
                                           createChangedDocument: cancellationToken => LoggingInvocationFixer.FixPascalCasePlaceHolderAsync(context.Document, syntaxToken, cancellationToken),
                                           equivalenceKey: "FixPascalCasePlaceHolder");
        context.RegisterCodeFix(codeAction, diagnostic);
    }

    #endregion Private 方法
}
