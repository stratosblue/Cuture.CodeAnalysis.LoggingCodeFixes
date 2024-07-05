using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Cuture.CodeAnalysis.LoggingCodeFixes.Test.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.LoggerMessageDefineAnalyzer
    , Cuture.CodeAnalysis.LoggingCodeFixes.LoggingCodeFixesProvider>;

namespace Cuture.CodeAnalysis.LoggingCodeFixes.Test;

[TestClass]
public class StructuredLoggingFixTest
{
    #region Public 方法

    [TestMethod]
    public async Task Should_Generate_Name_Success_For_MemberAccess()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            _logger.LogInformation({|#0:$"Value: {type.Name}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            var type = _logger.GetType();
            _logger.LogInformation("Value: {TypeName}", type.Name);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Generate_Name_Success_For_MemberAccess_ML()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            _logger.LogInformation({|#0:$"Value: {type.Assembly.EntryPoint.Name}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            var type = _logger.GetType();
            _logger.LogInformation("Value: {TypeAssemblyEntryPointName}", type.Assembly.EntryPoint.Name);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Generate_Name_Success_For_MethodAccess()
    {
        LoggingCodeTemplate test =
            """
            _logger.LogInformation({|#0:$"Value: {_logger.GetType()}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            _logger.LogInformation("Value: {LoggerGetType}", _logger.GetType());
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Generate_Name_Success_For_MethodAccess_ML()
    {
        LoggingCodeTemplate test =
            """
            _logger.LogInformation({|#0:$"Value: {_logger.GetType().GetProperties().GetHashCode()}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            _logger.LogInformation("Value: {LoggerGetTypeGetPropertiesGetHashCode}", _logger.GetType().GetProperties().GetHashCode());
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Inline_Literal_Number()
    {
        LoggingCodeTemplate test =
            """
            _logger.LogInformation({|#0:$"Value: {1}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            _logger.LogInformation("Value: 1");
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Retain_NameOf()
    {
        LoggingCodeTemplate test =
            """
            _logger.LogInformation({|#0:$"Value: {nameof(Exception)} {1}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            _logger.LogInformation($"Value: {nameof(Exception)} 1");
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Success()
    {
        LoggingCodeTemplate test =
            """
            Exception ex = null;
            _logger.LogInformation({|#0:$"Value: {1} - {nameof(TestClass)} - {_logger} - {ex.ToString()} - {ex.Message} - {1f.ToString("D2")} - {this.GetType().FullName}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            Exception ex = null;
            _logger.LogInformation(ex, $"Value: 1 - {nameof(TestClass)} - {{Logger}} - {{Ex}} - {{ExMessage}} - {{1f}} - {{GetTypeFullName}}", _logger, ex, ex.Message, 1f.ToString("D2"), this.GetType().FullName);
            """;

        var expected = VerifyCS.Diagnostic("CA2254").WithLocation(0).WithArguments("LoggerExtensions.LogInformation(ILogger, string?, params object?[])");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Success_With_Log_Exception()
    {
        LoggingCodeTemplate test =
            """
            Exception ex = null;
            _logger.LogInformation({|#0:$"Value: {ex.ToString()}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            Exception ex = null;
            _logger.LogInformation(ex, "Value: {Ex}", ex);
            """;
        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Trim_ToString()
    {
        LoggingCodeTemplate test =
            """
            var text = new object();
            _logger.LogInformation({|#0:$"Value: {text.ToString()}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            var text = new object();
            _logger.LogInformation("Value: {Text}", text);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Trim_Underline()
    {
        LoggingCodeTemplate test =
            """
            var _text = new object();
            _logger.LogInformation({|#0:$"Value: {_text}"|});
            """;

        LoggingCodeTemplate fixtest =
            """
            var _text = new object();
            _logger.LogInformation("Value: {Text}", _text);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    #endregion Public 方法

    #region Private 方法

    private static DiagnosticResult GetExpected()
    {
        return VerifyCS.Diagnostic("CA2254").WithLocation(0).WithArguments("LoggerExtensions.LogInformation(ILogger, string?, params object?[])");
    }

    #endregion Private 方法
}
