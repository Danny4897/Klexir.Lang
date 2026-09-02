using Klexir.Lang.Abstractions;

namespace Klexir.Lang;

public enum TokenType
{
    Int,
    Identifier,
    Let,
    Rec,
    In,
    If,
    Then,
    Else,
    True,
    False,
    Fun,
    Equals,
    EqualsEquals,
    FatArrow,
    Colon,
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
