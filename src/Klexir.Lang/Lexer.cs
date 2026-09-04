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

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                    column++;
                }

                continue;
            }

            var startColumn = column;

            if (c == '"')
            {
                var stringResult = ScanString(ref i, ref line, ref column);
                if (stringResult.IsFailure)
                {
                    return Result<IReadOnlyList<Token>>.Failure(stringResult.Error);
                }

                tokens.Add(new Token(TokenType.String, stringResult.Value, new SourcePosition(line, startColumn)));
                continue;
            }

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
                    "rec" => TokenType.Rec,
                    "in" => TokenType.In,
                    "if" => TokenType.If,
                    "then" => TokenType.Then,
                    "else" => TokenType.Else,
                    "true" => TokenType.True,
                    "false" => TokenType.False,
                    "func" => TokenType.Func,
                    "match" => TokenType.Match,
                    "with" => TokenType.With,
                    "Some" => TokenType.Some,
                    "None" => TokenType.None,
                    "Ok" => TokenType.Ok,
                    "Err" => TokenType.Err,
                    "map" => TokenType.Map,
                    "bind" => TokenType.Bind,
                    "filter" => TokenType.Filter,
                    "fold" => TokenType.Fold,
                    "record" => TokenType.Record,
                    "union" => TokenType.Union,
                    "andThen" => TokenType.AndThen,
                    _ => TokenType.Identifier,
                };
                tokens.Add(new Token(type, text, new SourcePosition(line, startColumn)));
                continue;
            }

            if (c == '=' && i + 1 < source.Length && source[i + 1] == '>')
            {
                tokens.Add(new Token(TokenType.FatArrow, "=>", new SourcePosition(line, startColumn)));
                i += 2;
                column += 2;
                continue;
            }

            if (c is '=' or '<' or '>' && i + 1 < source.Length && source[i + 1] == '=')
            {
                var twoCharType = c switch
                {
                    '=' => TokenType.EqualsEquals,
                    '<' => TokenType.LessEquals,
                    _ => TokenType.GreaterEquals,
                };
                tokens.Add(new Token(twoCharType, source.Substring(i, 2), new SourcePosition(line, startColumn)));
                i += 2;
                column += 2;
                continue;
            }

            var single = c switch
            {
                '=' => TokenType.Equals,
                '<' => TokenType.Less,
                '>' => TokenType.Greater,
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '*' => TokenType.Star,
                '/' => TokenType.Slash,
                '(' => TokenType.LParen,
                ')' => TokenType.RParen,
                '[' => TokenType.LBracket,
                ']' => TokenType.RBracket,
                '{' => TokenType.LBrace,
                '}' => TokenType.RBrace,
                ':' => TokenType.Colon,
                ',' => TokenType.Comma,
                '|' => TokenType.Pipe,
                ';' => TokenType.Semicolon,
                '.' => TokenType.Dot,
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

    /// <summary>Scans a <c>"..."</c> literal starting at the opening quote, unescaping <c>\" \\ \n</c> as it goes.</summary>
    private Result<string> ScanString(ref int i, ref int line, ref int column)
    {
        var startPosition = new SourcePosition(line, column);
        i++; // opening quote
        column++;

        var text = new System.Text.StringBuilder();

        while (true)
        {
            if (i >= source.Length)
            {
                return Result<string>.Failure(Error.Create($"Unterminated string literal starting at {startPosition}."));
            }

            var c = source[i];

            if (c == '"')
            {
                i++;
                column++;
                return Result<string>.Success(text.ToString());
            }

            if (c == '\n')
            {
                text.Append(c);
                i++;
                line++;
                column = 1;
                continue;
            }

            if (c == '\\')
            {
                if (i + 1 >= source.Length)
                {
                    return Result<string>.Failure(Error.Create($"Unterminated string literal starting at {startPosition}."));
                }

                char? escaped = source[i + 1] switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    'n' => '\n',
                    _ => null,
                };

                if (escaped is not { } escapedChar)
                {
                    return Result<string>.Failure(Error.Create(
                        $"Unknown escape sequence '\\{source[i + 1]}' at {new SourcePosition(line, column)}."));
                }

                text.Append(escapedChar);
                i += 2;
                column += 2;
                continue;
            }

            text.Append(c);
            i++;
            column++;
        }
    }
}
