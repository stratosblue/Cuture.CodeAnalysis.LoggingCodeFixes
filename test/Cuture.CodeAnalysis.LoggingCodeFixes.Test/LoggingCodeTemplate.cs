namespace Cuture.CodeAnalysis.LoggingCodeFixes.Test;

internal class LoggingCodeTemplate(string code)
{
    #region Public 属性

    public string Code { get; } = code;

    public string Postfix { get; set; } = @"
    }
}";

    public string Prefix { get; set; } = @"
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestNamespace;
class TestClass
{
    private ILogger _logger = NullLogger.Instance;

    public TestClass()
    {
";

    #endregion Public 属性

    #region Public 方法

    public static implicit operator LoggingCodeTemplate(string code) => new LoggingCodeTemplate(code);

    public static implicit operator string(LoggingCodeTemplate value) => value.ToString();

    public override string ToString() => $"{Prefix}{Code}{Postfix}";

    #endregion Public 方法
}
