using Microsoft.CodeAnalysis.CSharp.Syntax;

internal record struct InterpolatedStringContentSyntaxDescriptor(InterpolatedStringContentSyntax InterpolatedStringContentSyntax, ArgumentSyntax? Argument, ExpressionSyntax? VariableExpression)
{
    public static implicit operator InterpolatedStringContentSyntaxDescriptor(InterpolatedStringContentSyntax value)
    {
        return new(value, null, null);
    }

    public static implicit operator InterpolatedStringContentSyntaxDescriptor(ValueTuple<InterpolatedStringContentSyntax, ArgumentSyntax> value)
    {
        return new(value.Item1, value.Item2, null);
    }

    public static implicit operator InterpolatedStringContentSyntaxDescriptor(ValueTuple<InterpolatedStringContentSyntax, ExpressionSyntax> value)
    {
        return new(value.Item1, null, value.Item2);
    }

    public static implicit operator InterpolatedStringContentSyntaxDescriptor(ValueTuple<InterpolatedStringContentSyntax, ArgumentSyntax, ExpressionSyntax> value)
    {
        return new(value.Item1, value.Item2, value.Item3);
    }
}
