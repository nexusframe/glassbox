using System.Globalization;

namespace Glassbox.Tools.Mcp;

public static class Calculator
{
    private const int MaxLength = 200;

    public static decimal Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new FormatException("Expression is empty.");
        if (expression.Length > MaxLength) throw new FormatException("Expression is too long (max 200 characters).");
        try
        {
            var parser = new Parser(expression);
            return parser.Parse();
        }
        catch (DivideByZeroException) { throw new FormatException("Division by zero."); }
        catch (OverflowException) { throw new FormatException("Number is too large."); }
    }

    private sealed class Parser(string s)
    {
        private int _pos;

        public decimal Parse()
        {
            var v = Expr();
            SkipWs();
            if (_pos < s.Length) throw new FormatException($"Unexpected character '{s[_pos]}'.");
            return v;
        }

        private void SkipWs() { while (_pos < s.Length && char.IsWhiteSpace(s[_pos])) _pos++; }

        private char Peek() { SkipWs(); return _pos < s.Length ? s[_pos] : '\0'; }

        private decimal Expr()
        {
            var v = Term();
            while (true)
            {
                var c = Peek();
                if (c == '+') { _pos++; v = checked(v + Term()); }
                else if (c == '-') { _pos++; v = checked(v - Term()); }
                else return v;
            }
        }

        private decimal Term()
        {
            var v = Unary();
            while (true)
            {
                var c = Peek();
                if (c == '*') { _pos++; v = checked(v * Unary()); }
                else if (c == '/') { _pos++; v = v / Unary(); }
                else return v;
            }
        }

        private decimal Unary()
        {
            if (Peek() == '-') { _pos++; return -Unary(); }
            return Primary();
        }

        private decimal Primary()
        {
            var c = Peek();
            if (c == '(')
            {
                _pos++;
                var v = Expr();
                if (Peek() != ')') throw new FormatException("Missing closing parenthesis.");
                _pos++;
                return v;
            }
            if (!char.IsAsciiDigit(c) && c != '.')
                throw new FormatException(c == '\0' ? "Unexpected end of expression." : $"Unexpected character '{c}'.");
            var start = _pos;
            while (_pos < s.Length && (char.IsAsciiDigit(s[_pos]) || s[_pos] == '.')) _pos++;
            if (!decimal.TryParse(s.AsSpan(start, _pos - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n))
                throw new FormatException("Invalid number.");
            return n;
        }
    }
}
