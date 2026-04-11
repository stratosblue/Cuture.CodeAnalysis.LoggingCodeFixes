using VerifyCS = Cuture.CodeAnalysis.LoggingCodeFixes.Test.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.AvoidPotentiallyExpensiveCallWhenLoggingAnalyzer
    , Cuture.CodeAnalysis.LoggingCodeFixes.LoggingCodeFixesProvider>;

namespace Cuture.CodeAnalysis.LoggingCodeFixes.Test;

[TestClass]
public class LoggerMessageFixTest
{
    #region Public 方法

    [TestMethod]
    public async Task Should_Add_Using_And_Use_Short_Type_Name_In_Generated_Method()
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

                public void Test()
                {
                    {|#0:_logger.LogInformation("Time: {Time}", global::System.DateTimeOffset.Now)|};
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

                public void Test()
                {
                    LogInformationTime(_logger, global::System.DateTimeOffset.Now);
                }

                [LoggerMessage(Level = LogLevel.Information, Message = "Time: {Time}")]
                static partial void LogInformationTime(ILogger logger, DateTimeOffset time);
            }
            """;

        var expected = VerifyCS.Diagnostic("CA1873").WithLocation(0);
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Fix_To_LoggerMessage_For_MultiLevel_NonPartial_Nested_Type()
    {
        var test =
            """
            using Microsoft.Extensions.Logging;

            namespace TestNamespace;

            class Outer
            {
                class Middle
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

                        private int GetValue(int value) => value;
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
                partial class Middle
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

                        private int GetValue(int value) => value;
                        [LoggerMessage(Level = LogLevel.Information, Message = "Value: {Value}")]
                        static partial void LogInformationValue(ILogger logger, int value);
                    }
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("CA1873").WithLocation(0);
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Generate_NonConflicting_Method_Name()
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

                private static void LogInformationValue(ILogger logger, int value)
                {
                }

                private static void LogInformationValue1(ILogger logger, int value)
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

                private static void LogInformationValue(ILogger logger, int value)
                {
                }

                private static void LogInformationValue1(ILogger logger, int value)
                {
                }

                public void Test(int value)
                {
                    LogInformationValue2(_logger, value);
                }

                [LoggerMessage(Level = LogLevel.Information, Message = "Value: {Value}")]
                static partial void LogInformationValue2(ILogger logger, int value);
            }
            """;

        var expected = VerifyCS.Diagnostic("CA1873").WithLocation(0);
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Should_Handle_Keyword_And_Duplicate_Parameter_Names()
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

                public void Test(int value1, int value2)
                {
                    {|#0:_logger.LogInformation("Value: {class} {class}", value1, value2)|};
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

                public void Test(int value1, int value2)
                {
                    LogInformationClass(_logger, value1, value2);
                }

                [LoggerMessage(Level = LogLevel.Information, Message = "Value: {class} {class}")]
                static partial void LogInformationClass(ILogger logger, int @class, int @class1);
            }
            """;

        var expected = VerifyCS.Diagnostic("CA1873").WithLocation(0);
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    #endregion Public 方法
}
