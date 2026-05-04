using VerifyCS = Cuture.CodeAnalysis.LoggingCodeFixes.Test.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.LoggerMessageDefineAnalyzer
    , Cuture.CodeAnalysis.LoggingCodeFixes.LoggingCodeFixesProvider>;

namespace Cuture.CodeAnalysis.LoggingCodeFixes.Test;

[TestClass]
public class LoggerMessageFixCA1848Test
{
    [TestMethod]
    public async Task Should_Fix_To_LoggerMessage_For_NonPartial_Nested_Type()
    {
        var test =
            """
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            class Outer
            {
                class Inner
                {
                    private readonly ILogger _logger;

                    public Inner(ILogger logger)
                    {
                        _logger = logger;
                    }

                    public void Test(int value)
                    {
                        {|#0:_logger.LogInformation("Value: {Value}", value)|};
                    }
                }
            }
            """;

        var fixtest =
            """
            using Microsoft.Extensions.Logging;
            using System;

            namespace TestNamespace;

            partial class Outer
            {
                partial class Inner
                {
                    private readonly ILogger _logger;

                    public Inner(ILogger logger)
                    {
                        _logger = logger;
                    }

                    public void Test(int value)
                    {
                        LogInformationValue(_logger, value);
                    }

                    [LoggerMessage(Level = LogLevel.Information, Message = "Value: {Value}")]
                    static partial void LogInformationValue(ILogger logger, int value);
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("CA1848")
                               .WithLocation(0)
                               .WithArguments("LoggerExtensions.LogInformation(ILogger, string?, params object?[])");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Generate_NonConflicting_Method_Name_For_CA1848()
    {
        var test =
            """
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            class TestClass
            {
                private readonly ILogger _logger;

                public TestClass(ILogger logger)
                {
                    _logger = logger;
                }

                static void LogInformationValue(ILogger logger, int value)
                {
                }

                public void Test(int value)
                {
                    {|#0:_logger.LogInformation("Value: {Value}", value)|};
                }
            }
            """;

        var fixtest =
            """
            using Microsoft.Extensions.Logging;
            using System;

            namespace TestNamespace;

            partial class TestClass
            {
                private readonly ILogger _logger;

                public TestClass(ILogger logger)
                {
                    _logger = logger;
                }

                static void LogInformationValue(ILogger logger, int value)
                {
                }

                public void Test(int value)
                {
                    LogInformationValue1(_logger, value);
                }

                [LoggerMessage(Level = LogLevel.Information, Message = "Value: {Value}")]
                static partial void LogInformationValue1(ILogger logger, int value);
            }
            """;

        var expected = VerifyCS.Diagnostic("CA1848")
                               .WithLocation(0)
                               .WithArguments("LoggerExtensions.LogInformation(ILogger, string?, params object?[])");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }
}
