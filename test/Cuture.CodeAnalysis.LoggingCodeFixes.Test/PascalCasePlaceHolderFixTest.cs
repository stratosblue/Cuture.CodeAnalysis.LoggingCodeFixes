using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Cuture.CodeAnalysis.LoggingCodeFixes.Test.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.LoggerMessageDefineAnalyzer
    , Cuture.CodeAnalysis.LoggingCodeFixes.LoggingCodeFixesProvider>;

namespace Cuture.CodeAnalysis.LoggingCodeFixes.Test;

[TestClass]
public class PascalCasePlaceHolderFixTest
{
    #region Public 方法

    [TestMethod]
    public async Task Should_Success()
    {
        LoggingCodeTemplate test =
            """
            var type = _logger.GetType();
            var ex = new Exception();
            _logger.LogInformation({|#0:"Value: {ex} {typeAssemblyEntryPointName} {exGetTypeGetPropertiesGetHashCode} {type}"|}, nameof(ex), type.Assembly.EntryPoint.Name, ex.GetType().GetProperties().GetHashCode(), type);
            """;

        LoggingCodeTemplate fixtest =
            """
            var type = _logger.GetType();
            var ex = new Exception();
            _logger.LogInformation("Value: {Ex} {TypeAssemblyEntryPointName} {ExGetTypeGetPropertiesGetHashCode} {Type}", nameof(ex), type.Assembly.EntryPoint.Name, ex.GetType().GetProperties().GetHashCode(), type);
            """;

        var expected = GetExpected();

        await VerifyCS.VerifyCodeFixAsync(test, [expected, expected, expected, expected], fixtest);
    }

    #endregion Public 方法

    #region Private 方法

    private static DiagnosticResult GetExpected()
    {
        return VerifyCS.Diagnostic("CA1727").WithLocation(0).WithArguments("LoggerExtensions.LogInformation(ILogger, string?, params object?[])");
    }

    #endregion Private 方法
}
