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
    [DataRow("ex.Message", "Message")]
    [DataRow("ex.message.length.value", "LengthValue")]
    [DataRow("ex.@Message", "Message")]
    [DataRow("ex.@default", "Default")]
    [DataRow("ex.@default.@int", "DefaultInt")]
    [DataRow("ex?.Message", "Message")]
    [DataRow("ex?.Message?.Length", "MessageLength")]
    [DataRow("ex.GetType()", "Type")]
    [DataRow("ex?.GetType()", "Type")]
    [DataRow("ex.GetType().GetProperties()", "TypeProperties")]
    [DataRow("ex.GetType()?.GetProperties()", "TypeProperties")]
    [DataRow("ex.GetType().@default.Method()", "DefaultMethod")]
    [DataRow("ex.GetType().@default.Property", "DefaultProperty")]
    public void Should_Success(string source, string target)
    {
        var normalizeResult = PlaceHolderNormalizer.Normalize(source);
        Assert.AreEqual(target, normalizeResult);
    }

    #endregion Public 方法
}
