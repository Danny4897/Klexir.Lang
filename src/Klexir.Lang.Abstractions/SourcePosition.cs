namespace Klexir.Lang.Abstractions;

/// <summary>1-based line/column location in source text, used by lexer, parser and type-checker diagnostics.</summary>
public readonly record struct SourcePosition(int Line, int Column)
{
    public override string ToString() => $"{Line}:{Column}";
}
