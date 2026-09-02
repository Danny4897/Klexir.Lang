using Klexir.Lang.Abstractions;

namespace Klexir.Lang;

public enum TokenType
{
    Int,
    Identifier,
    Let,
    In,
    If,
    Then,
    Else,
    True,
    False,
    Equals,
    EqualsEquals,
    Less,
    LessEquals,
    Greater,
    GreaterEquals,
    Plus,
    Minus,
    Star,
    Slash,
    LParen,
    RParen,
    Eof,
}

public sealed record Token(TokenType Type, string Text, SourcePosition Position);
