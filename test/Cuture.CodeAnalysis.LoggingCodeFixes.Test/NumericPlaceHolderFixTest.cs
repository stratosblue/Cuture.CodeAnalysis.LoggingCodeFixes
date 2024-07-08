using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Cuture.CodeAnalysis.LoggingCodeFixes.Test.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.LoggerMessageDefineAnalyzer
    , Cuture.CodeAnalysis.LoggingCodeFixes.LoggingCodeFixesProvider>;

namespace Cuture.CodeAnalysis.LoggingCodeFixes.Test;

[TestClass]
public class NumericPlaceHolderFixTest
{
    #region Public 方法

    [TestMethod]
    public async Task Should_BeginLine_NewLine_Retain()
    {
        LoggingCodeTemplate test =
            @"
            var type = _logger.GetType();
            _logger.LogInformation({|#0:""\r\nValue: {1}""|}, type?.Name);
            ";

        LoggingCodeTemplate fixtest =
            @"
            var type = _logger.GetType();
            _logger.LogInformation(""\r\nValue: {TypeName}"", type?.Name);
            ";

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_ConditionalAccess_Replace_With_Memeber_Name()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            _logger.LogInformation({|#0:"Value: {1}"|}, type?.Name);
            """;

        LoggingCodeTemplate fixtest =
            """
            var type = _logger.GetType();
            _logger.LogInformation("Value: {TypeName}", type?.Name);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_ConditionalAccess_Replace_With_Memeber_Name_ML()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            _logger.LogInformation({|#0:"Value: {1}"|}, type?.Assembly?.EntryPoint?.Name);
            """;

        LoggingCodeTemplate fixtest =
            """
            var type = _logger.GetType();
            _logger.LogInformation("Value: {TypeAssemblyEntryPointName}", type?.Assembly?.EntryPoint?.Name);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_ConditionalAccess_Replace_With_Method_Name()
    {
        LoggingCodeTemplate test =
            """
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {1}"|}, ex?.GetType());
            """;

        LoggingCodeTemplate fixtest =
            """
            var ex = new Exception();
            _logger.LogInformation("Value: {ExGetType}", ex?.GetType());
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_ConditionalAccess_Replace_With_Method_Name_ML()
    {
        LoggingCodeTemplate test =
            """
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {1}"|}, ex?.GetType()?.GetProperties()?.GetHashCode());
            """;

        LoggingCodeTemplate fixtest =
            """
            var ex = new Exception();
            _logger.LogInformation("Value: {ExGetTypeGetPropertiesGetHashCode}", ex?.GetType()?.GetProperties()?.GetHashCode());
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_EndLine_NewLine_Retain()
    {
        LoggingCodeTemplate test =
            @"
            var type = _logger.GetType();
            _logger.LogInformation({|#0:""Value: {1}\r\n""|}, type?.Name);
            ";

        LoggingCodeTemplate fixtest =
            @"
            var type = _logger.GetType();
            _logger.LogInformation(""Value: {TypeName}\r\n"", type?.Name);
            ";

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_MidLine_NewLine_Retain()
    {
        LoggingCodeTemplate test =
            @"
            var type = _logger.GetType();
            _logger.LogInformation({|#0:""Value: {1}\r\n{2}""|}, type?.Name, type);
            ";

        LoggingCodeTemplate fixtest =
            @"
            var type = _logger.GetType();
            _logger.LogInformation(""Value: {TypeName}\r\n{Type}"", type?.Name, type);
            ";

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, [expected, expected], fixtest);
    }

    [TestMethod]
    public async Task Should_Multi_NewLine_Retain()
    {
        LoggingCodeTemplate test =
            @"
            var type = _logger.GetType();
            _logger.LogInformation({|#0:""Value\r\n: {1}\r\n{2}\r\n""|}, type?.Name, type);
            ";

        LoggingCodeTemplate fixtest =
            @"
            var type = _logger.GetType();
            _logger.LogInformation(""Value\r\n: {TypeName}\r\n{Type}\r\n"", type?.Name, type);
            ";

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, [expected, expected], fixtest);
    }

    [TestMethod]
    public async Task Should_Replace_With_Memeber_Name()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            _logger.LogInformation({|#0:"Value: {1}"|}, type.Name);
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
    public async Task Should_Replace_With_Memeber_Name_ML()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            _logger.LogInformation({|#0:"Value: {1}"|}, type.Assembly.EntryPoint.Name);
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
    public async Task Should_Replace_With_Method_Name()
    {
        LoggingCodeTemplate test =
            """
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {1}"|}, ex.GetType());
            """;

        LoggingCodeTemplate fixtest =
            """
            var ex = new Exception();
            _logger.LogInformation("Value: {ExGetType}", ex.GetType());
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Replace_With_Method_Name_ML()
    {
        LoggingCodeTemplate test =
            """
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {1}"|}, ex.GetType().GetProperties().GetHashCode());
            """;

        LoggingCodeTemplate fixtest =
            """
            var ex = new Exception();
            _logger.LogInformation("Value: {ExGetTypeGetPropertiesGetHashCode}", ex.GetType().GetProperties().GetHashCode());
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Replace_With_Name_Of()
    {
        LoggingCodeTemplate test =
            """
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {1}"|}, nameof(ex));
            """;

        LoggingCodeTemplate fixtest =
            """
            var ex = new Exception();
            _logger.LogInformation("Value: {Ex}", nameof(ex));
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Replace_With_Variable_Name()
    {
        LoggingCodeTemplate test =
            """
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {1}"|}, ex);
            """;

        LoggingCodeTemplate fixtest =
            """
            var ex = new Exception();
            _logger.LogInformation("Value: {Ex}", ex);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Success()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {1} {2} {3} {4}"|}, nameof(ex), type.Assembly?.EntryPoint.Name, ex.GetType().GetProperties()?.GetHashCode(), type);
            """;

        LoggingCodeTemplate fixtest =
            """
            var type = _logger.GetType();
            var ex = new Exception();
            _logger.LogInformation("Value: {Ex} {TypeAssemblyEntryPointName} {ExGetTypeGetPropertiesGetHashCode} {Type}", nameof(ex), type.Assembly?.EntryPoint.Name, ex.GetType().GetProperties()?.GetHashCode(), type);
            """;

        var expected = GetExpected();
        await VerifyCS.VerifyCodeFixAsync(test, [expected, expected, expected, expected], fixtest);
    }

    #endregion Public 方法

    #region Private 方法

    private static DiagnosticResult GetExpected()
    {
        return VerifyCS.Diagnostic("CA2253").WithLocation(0).WithArguments("LoggerExtensions.LogInformation(ILogger, string?, params object?[])");
    }

    #endregion Private 方法
}
