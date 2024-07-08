namespace Cuture.CodeAnalysis.LoggingCodeFixes.Test;

[TestClass]
public class PlaceHolderNormalizerTest
{
    #region Public 方法

    [TestMethod]
    [DataRow("@int", "Int")]
    [DataRow("Hello", "Hello")]
    [DataRow("hello", "Hello")]
    [DataRow("ex", "Ex")]
    [DataRow("ex.Message", "ExMessage")]
    [DataRow("ex.message.length.value", "ExMessageLengthValue")]
    [DataRow("ex.@Message", "ExMessage")]
    [DataRow("ex.@default", "ExDefault")]
    [DataRow("ex.@default.@int", "ExDefaultInt")]
    [DataRow("ex?.Message", "ExMessage")]
    [DataRow("ex?.Message?.Length", "ExMessageLength")]
    [DataRow("ex.GetType()", "ExGetType")]
    [DataRow("ex?.GetType()", "ExGetType")]
    [DataRow("ex.GetType().GetProperties()", "ExGetTypeGetProperties")]
    [DataRow("ex.GetType()?.GetProperties()", "ExGetTypeGetProperties")]
    [DataRow("ex.GetType().@default.Method()", "ExGetTypeDefaultMethod")]
    [DataRow("ex.GetType().@default.Property", "ExGetTypeDefaultProperty")]
    public void Should_Success(string source, string target)
    {
        var normalizeResult = PlaceHolderNormalizer.Normalize(source);
        Assert.AreEqual(target, normalizeResult);
    }

    #endregion Public 方法
}
