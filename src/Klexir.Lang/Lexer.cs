using Klexir.Lang.Abstractions;
using MonadicSharp;

namespace Klexir.Lang;

/// <summary>Hand-written scanner: identifiers/keywords, integer literals, arithmetic operators, parens.</summary>
public sealed class Lexer(string source)
{
    public Result<IReadOnlyList<Token>> Tokenize()
    {
        var tokens = new List<Token>();
        var i = 0;
        var line = 1;
        var column = 1;

        while (i < source.Length)
        {
            var c = source[i];

            if (c == '\n')
            {
                i++;
                line++;
                column = 1;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                i++;
                column++;
                continue;
            }

            var startColumn = column;

            if (char.IsDigit(c))
            {
                var start = i;
                while (i < source.Length && char.IsDigit(source[i]))
                {
                    i++;
                    column++;
                }

                tokens.Add(new Token(TokenType.Int, source[start..i], new SourcePosition(line, startColumn)));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                {
                    i++;
                    column++;
                }

                var text = source[start..i];
                var type = text switch
                {
                    "let" => TokenType.Let,
                    "in" => TokenType.In,
                    _ => TokenType.Identifier,
                };
                tokens.Add(new Token(type, text, new SourcePosition(line, startColumn)));
                continue;
            }

            var single = c switch
            {
                '=' => TokenType.Equals,
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '*' => TokenType.Star,
                '/' => TokenType.Slash,
                '(' => TokenType.LParen,
                ')' => TokenType.RParen,
                _ => (TokenType?)null,
            };

            if (single is not { } tokenType)
            {
                return Result<IReadOnlyList<Token>>.Failure(
                    Error.Create($"Unexpected character '{c}' at {new SourcePosition(line, column)}."));
            }

            tokens.Add(new Token(tokenType, c.ToString(), new SourcePosition(line, startColumn)));
            i++;
            column++;
        }

        tokens.Add(new Token(TokenType.Eof, string.Empty, new SourcePosition(line, column)));
        return Result<IReadOnlyList<Token>>.Success(tokens);
    }
}
