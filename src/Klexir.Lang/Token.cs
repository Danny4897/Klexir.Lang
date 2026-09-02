using Klexir.Lang.Abstractions;

namespace Klexir.Lang;

public enum TokenType
{
    Int,
    Identifier,
    Let,
    In,
    Equals,
    Plus,
    Minus,
    Star,
    Slash,
    LParen,
    RParen,
    Eof,
}

public sealed record Token(TokenType Type, string Text, SourcePosition Position);
