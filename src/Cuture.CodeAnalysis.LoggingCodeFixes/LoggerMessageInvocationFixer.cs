using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

using static Cuture.CodeAnalysis.LoggingCodeFixes.PlaceHolderNormalizer;

namespace Cuture.CodeAnalysis.LoggingCodeFixes;

internal static class LoggerMessageInvocationFixer
{
    #region Public 方法

    public static async Task<Document> FixAsync(Document document, InvocationExpressionSyntax logInvocationExpressionSyntax, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (syntaxRoot is not CompilationUnitSyntax compilationUnitSyntax || semanticModel is null)
        {
            return document;
        }

        if (semanticModel.GetOperation(logInvocationExpressionSyntax, cancellationToken) is not IInvocationOperation invocationOperation)
        {
            return document;
        }

        if (!TryGetMessageArgument(invocationOperation, out var messageArgument)
            || !TryGetLoggerArgument(invocationOperation, semanticModel.Compilation, out var loggerArgument)
            || !TryGetLogLevelExpression(invocationOperation.TargetMethod, out var logLevelExpressionSyntax)
            || !TryGetMessageTemplate(messageArgument, out var messageTemplate))
        {
            return document;
        }

        var currentTypeDeclaration = logInvocationExpressionSyntax.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (currentTypeDeclaration is null)
        {
            return document;
        }

        var currentTypeSymbol = semanticModel.GetDeclaredSymbol(currentTypeDeclaration, cancellationToken);

        var invocationAnnotation = new SyntaxAnnotation();
        syntaxRoot = syntaxRoot.ReplaceNode(logInvocationExpressionSyntax, logInvocationExpressionSyntax.WithAdditionalAnnotations(invocationAnnotation));
        currentTypeDeclaration = syntaxRoot.FindToken(currentTypeDeclaration.SpanStart)
                                          .Parent
                                          .AncestorsAndSelf()
                                          .OfType<TypeDeclarationSyntax>()
                                          .FirstOrDefault(m => m.SpanStart == currentTypeDeclaration.SpanStart);
        if (currentTypeDeclaration is null)
        {
            return document;
        }

        var requiredNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "Microsoft.Extensions.Logging"
        };

        var placeholders = ParseTemplatePlaceholders(messageTemplate);
        var methodBaseName = CreateMethodBaseName(logLevelExpressionSyntax, placeholders.FirstOrDefault());
        var methodName = CreateUniqueMethodName(currentTypeDeclaration, currentTypeSymbol, methodBaseName);

        var usedParameterNames = new HashSet<string>(StringComparer.Ordinal);
        var parameters = new List<ParameterSyntax>();
        var invocationArguments = new List<ArgumentSyntax>();

        var loggerTypeName = loggerArgument.Parameter?.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "ILogger";
        parameters.Add(CreateParameterSyntax(CreateUniqueParameterName("logger", usedParameterNames), loggerTypeName));
        invocationArguments.Add(SyntaxFactory.Argument((ExpressionSyntax)loggerArgument.Value.Syntax));
        CollectRequiredNamespaces(loggerArgument.Parameter?.Type, requiredNamespaces);

        if (TryGetExceptionArgument(invocationOperation, semanticModel.Compilation, out var exceptionArgument))
        {
            var exceptionParameterName = CreateUniqueParameterName("exception", usedParameterNames);
            var exceptionTypeName = exceptionArgument!.Parameter?.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "Exception";
            parameters.Add(CreateParameterSyntax(exceptionParameterName, exceptionTypeName));
            invocationArguments.Add(SyntaxFactory.Argument((ExpressionSyntax)exceptionArgument.Value.Syntax));
            CollectRequiredNamespaces(exceptionArgument.Parameter?.Type, requiredNamespaces);
        }

        var argumentIndex = 0;
        foreach (var logArgument in invocationOperation.Arguments.Where(static m => m.Parameter?.IsParams == true).OrderBy(static m => m.Syntax.SpanStart))
        {
            if (logArgument.Value is IArrayCreationOperation arrayCreationOperation && arrayCreationOperation.Initializer is not null)
            {
                foreach (var elementOperation in arrayCreationOperation.Initializer.ElementValues)
                {
                    if (elementOperation.Syntax is not ExpressionSyntax expressionSyntax)
                    {
                        continue;
                    }

                    AddLogParameter(expressionSyntax,
                                    elementOperation.Type,
                                    placeholders,
                                    ref argumentIndex,
                                    semanticModel,
                                    cancellationToken,
                                    usedParameterNames,
                                    parameters,
                                    invocationArguments,
                                    requiredNamespaces);
                }

                continue;
            }

            if (logArgument.Syntax is not ArgumentSyntax { Expression: ExpressionSyntax expressionSyntaxFromArgument })
            {
                continue;
            }

            AddLogParameter(expressionSyntaxFromArgument,
                            logArgument.Value.Type ?? logArgument.Parameter?.Type,
                            placeholders,
                            ref argumentIndex,
                            semanticModel,
                            cancellationToken,
                            usedParameterNames,
                            parameters,
                            invocationArguments,
                            requiredNamespaces);
        }

        ExpressionSyntax? eventIdExpressionSyntax = null;
        var eventIdArgument = invocationOperation.Arguments.FirstOrDefault(static m => string.Equals(m.Parameter?.Name, "eventId", StringComparison.Ordinal));
        if (eventIdArgument is not null)
        {
            eventIdExpressionSyntax = (ExpressionSyntax)eventIdArgument.Value.Syntax;
        }

        var generatedMethod = CreateLoggerMessageMethodDeclaration(methodName, logLevelExpressionSyntax, messageTemplate, eventIdExpressionSyntax, parameters);

        var updatedCurrentType = EnsurePartial(currentTypeDeclaration).AddMembers(generatedMethod);
        var newRoot = syntaxRoot.ReplaceNode(currentTypeDeclaration, updatedCurrentType);

        foreach (var parentType in currentTypeDeclaration.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            var foundType = newRoot.FindToken(parentType.SpanStart).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault(m => m.SpanStart == parentType.SpanStart);
            if (foundType is not null)
            {
                newRoot = newRoot.ReplaceNode(foundType, EnsurePartial(foundType));
            }
        }

        var targetInvocation = newRoot.GetAnnotatedNodes(invocationAnnotation).OfType<InvocationExpressionSyntax>().FirstOrDefault();
        if (targetInvocation is null)
        {
            return document.WithSyntaxRoot(AddUsingDirectives((CompilationUnitSyntax)newRoot, requiredNamespaces));
        }

        var newInvocationExpressionSyntax = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methodName), SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(invocationArguments))).WithTriviaFrom(targetInvocation);
        newRoot = newRoot.ReplaceNode(targetInvocation, newInvocationExpressionSyntax);

        return document.WithSyntaxRoot(AddUsingDirectives((CompilationUnitSyntax)newRoot, requiredNamespaces));
    }

    #endregion Public 方法

    #region Private 方法

    private static void AddLogParameter(ExpressionSyntax expressionSyntax,
                                        ITypeSymbol? fallbackType,
                                        IReadOnlyList<string> placeholders,
                                        ref int argumentIndex,
                                        SemanticModel semanticModel,
                                        CancellationToken cancellationToken,
                                        HashSet<string> usedParameterNames,
                                        List<ParameterSyntax> parameters,
                                        List<ArgumentSyntax> invocationArguments,
                                        HashSet<string> requiredNamespaces)
    {
        var typeInfo = semanticModel.GetTypeInfo(expressionSyntax, cancellationToken);
        var parameterType = typeInfo.Type;
        if (parameterType is null || parameterType.SpecialType == SpecialType.System_Void)
        {
            parameterType = typeInfo.ConvertedType ?? fallbackType;
        }
        if (parameterType?.SpecialType == SpecialType.System_Void)
        {
            parameterType = null;
        }

        var parameterTypeName = parameterType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "object";

        var placeholder = argumentIndex < placeholders.Count ? placeholders[argumentIndex] : string.Empty;
        var baseParameterName = !string.IsNullOrWhiteSpace(placeholder) ? Normalize(placeholder) : Normalize(expressionSyntax.ToString());
        var parameterName = CreateUniqueParameterName(baseParameterName, usedParameterNames, argumentIndex);

        parameters.Add(CreateParameterSyntax(parameterName, parameterTypeName));
        invocationArguments.Add(SyntaxFactory.Argument(expressionSyntax));
        CollectRequiredNamespaces(parameterType, requiredNamespaces);

        argumentIndex++;
    }

    private static CompilationUnitSyntax AddUsingDirectives(CompilationUnitSyntax compilationUnitSyntax, IEnumerable<string> requiredNamespaces)
    {
        var existingNamespaces = new HashSet<string>(compilationUnitSyntax.Usings.Select(static m => m.Name?.ToString()).Where(static m => !string.IsNullOrWhiteSpace(m))!, StringComparer.Ordinal);
        var missingUsings = requiredNamespaces.Where(static m => !string.IsNullOrWhiteSpace(m))
                                              .Distinct(StringComparer.Ordinal)
                                              .Where(m => !existingNamespaces.Contains(m))
                                              .OrderBy(static m => m)
                                              .Select(m => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(m)))
                                              .ToArray();
        if (missingUsings.Length == 0)
        {
            return compilationUnitSyntax;
        }

        return compilationUnitSyntax.AddUsings(missingUsings);
    }

    private static void CollectRequiredNamespaces(ITypeSymbol? typeSymbol, HashSet<string> namespaces)
    {
        if (typeSymbol is null)
        {
            return;
        }

        switch (typeSymbol)
        {
            case IArrayTypeSymbol arrayTypeSymbol:
                CollectRequiredNamespaces(arrayTypeSymbol.ElementType, namespaces);
                break;

            case INamedTypeSymbol namedTypeSymbol:
                var namespaceString = namedTypeSymbol.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrWhiteSpace(namespaceString))
                {
                    namespaces.Add(namespaceString!);
                }

                foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                {
                    CollectRequiredNamespaces(typeArgument, namespaces);
                }
                break;
        }
    }

    private static MethodDeclarationSyntax CreateLoggerMessageMethodDeclaration(string methodName,
                                                                                ExpressionSyntax logLevelExpressionSyntax,
                                                                                string messageTemplate,
                                                                                ExpressionSyntax? eventIdExpressionSyntax,
                                                                                IEnumerable<ParameterSyntax> parameters)
    {
        var loggerMessageAttributeArguments = new List<AttributeArgumentSyntax>
        {
            CreateNamedAttributeArgument("Level", logLevelExpressionSyntax),
            CreateNamedAttributeArgument("Message", SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(messageTemplate)))
        };

        if (eventIdExpressionSyntax is not null)
        {
            loggerMessageAttributeArguments.Add(CreateNamedAttributeArgument("EventId", eventIdExpressionSyntax));
        }

        return SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), methodName)
                            //修正单元测试后，恢复默认生成方法的访问修饰符为private，避免生成的LoggerMessage方法被外部调用
                            //.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                            .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("LoggerMessage"))
                                                                                                                              .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(loggerMessageAttributeArguments))))))
                            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private static string CreateMethodBaseName(ExpressionSyntax logLevelExpressionSyntax, string? placeholder)
    {
        var logLevelName = logLevelExpressionSyntax is MemberAccessExpressionSyntax memberAccessExpressionSyntax
            ? memberAccessExpressionSyntax.Name.Identifier.ValueText
            : "Information";

        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            return $"Log{logLevelName}{Normalize(placeholder!)}";
        }
        return $"Log{logLevelName}Message";
    }

    private static AttributeArgumentSyntax CreateNamedAttributeArgument(string name, ExpressionSyntax valueExpressionSyntax)
    {
        return SyntaxFactory.AttributeArgument(valueExpressionSyntax)
                            .WithNameEquals(SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(name)));
    }

    private static ParameterSyntax CreateParameterSyntax(string parameterName, string typeName)
    {
        return SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)).WithType(SyntaxFactory.ParseTypeName(typeName));
    }

    private static string CreateUniqueMethodName(TypeDeclarationSyntax typeDeclarationSyntax, INamedTypeSymbol? typeSymbol, string baseName)
    {
        baseName = NormalizeIdentifier(baseName, "LogMessage");
        var existingMethodNames = new HashSet<string>(typeDeclarationSyntax.Members.OfType<MethodDeclarationSyntax>().Select(static m => m.Identifier.ValueText), StringComparer.Ordinal);

        if (typeSymbol is not null)
        {
            foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                existingMethodNames.Add(member.Name);
            }
        }

        if (!existingMethodNames.Contains(baseName))
        {
            return baseName;
        }

        var index = 1;
        while (existingMethodNames.Contains($"{baseName}{index}"))
        {
            index++;
        }
        return $"{baseName}{index}";
    }

    private static string CreateUniqueParameterName(string baseName, HashSet<string> usedNames, int index = -1)
    {
        var fallbackName = index < 0 ? "value" : $"value{index}";
        var normalizedName = NormalizeIdentifier(baseName, fallbackName);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            normalizedName = fallbackName;
        }

        normalizedName = char.ToLower(normalizedName[0]) + normalizedName.Substring(1);

        if (SyntaxFacts.GetKeywordKind(normalizedName) != SyntaxKind.None)
        {
            normalizedName = $"@{normalizedName}";
        }

        if (usedNames.Add(normalizedName))
        {
            return normalizedName;
        }

        var suffix = 1;
        while (!usedNames.Add($"{normalizedName}{suffix}"))
        {
            suffix++;
        }
        return $"{normalizedName}{suffix}";
    }

    private static TypeDeclarationSyntax EnsurePartial(TypeDeclarationSyntax typeDeclarationSyntax)
    {
        if (typeDeclarationSyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            return typeDeclarationSyntax;
        }
        return typeDeclarationSyntax.WithModifiers(typeDeclarationSyntax.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));
    }

    private static bool IsExceptionType(ITypeSymbol? typeSymbol, Compilation compilation)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        var exceptionType = compilation.GetTypeByMetadataName("System.Exception");
        for (var current = typeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, exceptionType))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsLoggerType(ITypeSymbol? typeSymbol, Compilation compilation)
    {
        var loggerType = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger");
        return loggerType is not null && typeSymbol is not null && SymbolEqualityComparer.Default.Equals(typeSymbol, loggerType);
    }

    private static string NormalizeIdentifier(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var chars = value.Where(static m => char.IsLetterOrDigit(m) || m == '_').ToArray();
        if (chars.Length == 0)
        {
            return fallback;
        }

        if (!char.IsLetter(chars[0]) && chars[0] != '_')
        {
            return $"_{new string(chars)}";
        }

        return new string(chars);
    }

    private static List<string> ParseTemplatePlaceholders(string messageTemplate)
    {
        var placeholders = new List<string>();
        for (var index = 0; index < messageTemplate.Length; index++)
        {
            if (messageTemplate[index] != '{')
            {
                continue;
            }
            if (index + 1 < messageTemplate.Length && messageTemplate[index + 1] == '{')
            {
                index++;
                continue;
            }

            var closeIndex = messageTemplate.IndexOf('}', index + 1);
            if (closeIndex < 0)
            {
                break;
            }

            var rawPlaceholder = messageTemplate.Substring(index + 1, closeIndex - index - 1);
            var separatorIndex = rawPlaceholder.IndexOfAny([',', ':']);
            if (separatorIndex >= 0)
            {
                rawPlaceholder = rawPlaceholder.Substring(0, separatorIndex);
            }

            placeholders.Add(rawPlaceholder.Trim());
            index = closeIndex;
        }
        return placeholders;
    }

    private static bool TryGetExceptionArgument(IInvocationOperation invocationOperation, Compilation compilation, out IArgumentOperation? exceptionArgument)
    {
        exceptionArgument = invocationOperation.Arguments.FirstOrDefault(m => IsExceptionType(m.Parameter?.Type, compilation));
        return exceptionArgument is not null;
    }

    private static bool TryGetLoggerArgument(IInvocationOperation invocationOperation, Compilation compilation, out IArgumentOperation loggerArgument)
    {
        loggerArgument = invocationOperation.Arguments.FirstOrDefault(m => IsLoggerType(m.Parameter?.Type, compilation));
        return loggerArgument is not null;
    }

    private static bool TryGetLogLevelExpression(IMethodSymbol methodSymbol, out ExpressionSyntax logLevelExpressionSyntax)
    {
        logLevelExpressionSyntax = methodSymbol.Name switch
        {
            "LogTrace" => SyntaxFactory.ParseExpression("LogLevel.Trace"),
            "LogDebug" => SyntaxFactory.ParseExpression("LogLevel.Debug"),
            "LogInformation" => SyntaxFactory.ParseExpression("LogLevel.Information"),
            "LogWarning" => SyntaxFactory.ParseExpression("LogLevel.Warning"),
            "LogError" => SyntaxFactory.ParseExpression("LogLevel.Error"),
            "LogCritical" => SyntaxFactory.ParseExpression("LogLevel.Critical"),
            _ => null!
        };
        return logLevelExpressionSyntax is not null;
    }

    private static bool TryGetMessageArgument(IInvocationOperation invocationOperation, out IArgumentOperation messageArgument)
    {
        messageArgument = invocationOperation.Arguments.FirstOrDefault(static m => string.Equals(m.Parameter?.Name, "message", StringComparison.Ordinal));
        return messageArgument is not null;
    }

    private static bool TryGetMessageTemplate(IArgumentOperation messageArgument, out string messageTemplate)
    {
        messageTemplate = string.Empty;

        if (messageArgument.Value.ConstantValue.HasValue && messageArgument.Value.ConstantValue.Value is string constantMessage)
        {
            messageTemplate = constantMessage;
            return true;
        }

        if (messageArgument.Value.Syntax is LiteralExpressionSyntax literalExpressionSyntax && literalExpressionSyntax.IsKind(SyntaxKind.StringLiteralExpression))
        {
            messageTemplate = literalExpressionSyntax.Token.ValueText;
            return true;
        }

        return false;
    }

    #endregion Private 方法
}
