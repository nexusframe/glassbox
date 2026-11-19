using Glassbox.Tools.Mcp;

namespace Glassbox.Tests;

public class CalculatorTests
{
    [Theory]
    [InlineData("240*0.17", "40.8")]
    [InlineData("2+3*4", "14")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("-3+5", "2")]
    [InlineData(" 10 / 4 ", "2.5")]
    public void Evaluates_valid_expressions(string expr, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Calculator.Evaluate(expr));

    [Theory]
    [InlineData("1/0")]
    [InlineData("2+")]
    [InlineData("2,5+1")]
    [InlineData("System.IO.File")]
    [InlineData("(1")]
    [InlineData("")]
    public void Rejects_invalid_expressions(string expr) =>
        Assert.Throws<FormatException>(() => Calculator.Evaluate(expr));

    [Fact]
    public void Rejects_too_long_expression() =>
        Assert.Throws<FormatException>(() => Calculator.Evaluate(string.Concat(Enumerable.Repeat("1+", 100)) + "1"));
}
