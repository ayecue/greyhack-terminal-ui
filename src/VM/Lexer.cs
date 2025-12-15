using System.Collections.Generic;
using System.Text;

namespace GreyHackTerminalUI.VM
{
    public class Lexer
  {
        public const string BLOCK_START = "#UI{";

        private readonly string _input;
        private int _position;
        private int _line;
        private int _column;
        private int _blockStart;

        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            { "var", TokenType.Var },
            { "if", TokenType.If },
            { "then", TokenType.Then },
            { "else", TokenType.Else },
            { "while", TokenType.While },
            { "do", TokenType.Do },
            { "return", TokenType.Return },
            { "and", TokenType.And },
            { "or", TokenType.Or },
            { "not", TokenType.Not },
            { "true", TokenType.True },
            { "false", TokenType.False },
            { "null", TokenType.Null },
            { "end", TokenType.Identifier } // Special handling for "end if" / "end while"
        };

        private static readonly Dictionary<string, Dictionary<string, (TokenType type, string display)>> CompoundKeywords =
            new Dictionary<string, Dictionary<string, (TokenType type, string display)>>
        {
            ["end"] = new Dictionary<string, (TokenType, string)>
            {
                ["if"] = (TokenType.EndIf, "end if"),
                ["while"] = (TokenType.EndWhile, "end while")
            },
            ["else"] = new Dictionary<string, (TokenType, string)>
            {
                ["if"] = (TokenType.ElseIf, "else if")
            }
        };

        public Lexer(string input)
        {
            _input = input ?? "";
            _position = 0;
            _line = 1;
            _column = 1;
            _blockStart = -1;
        }

        public List<Token> NextUIBlock()
        {
            int start = FindUIBlockStart();
            if (start == -1)
                return null;

            _blockStart = start;
            _position = start + BLOCK_START.Length;
            UpdateLineColumn(start, _position);

            var tokens = new List<Token>() { new Token(TokenType.UIBlockStart, BLOCK_START, _line, _column) };

            int braceDepth = 1;

            while (_position < _input.Length && braceDepth > 0)
            {
                SkipWhitespaceAndComments();
                
                if (_position >= _input.Length)
                    break;

                Token token = ReadToken();
                
                if (token.Type == TokenType.Error)
                {
                    tokens.Add(token);
                    break;
                }

                if (token.Type == TokenType.EOF)
                    break;

                // Track brace depth
                if (token.Type == TokenType.UIBlockStart)
                    braceDepth++;
                else if (token.Type == TokenType.BraceClose)
                    braceDepth--;

                // Don't add the final closing brace to tokens (marks end of block)
                if (braceDepth > 0 || token.Type != TokenType.BraceClose)
                    tokens.Add(token);

                if (braceDepth == 0)
                    break;
            }

            tokens.Add(new Token(TokenType.EOF, "", _line, _column));
            return tokens;
        }

        public (int start, int end) GetConsumedRange()
        {
            return (_blockStart, _position);
        }

        private int FindUIBlockStart()
        {
            while (_position < _input.Length - 3)
            {
                int idx = _input.IndexOf(Lexer.BLOCK_START, _position, System.StringComparison.Ordinal);
                if (idx == -1)
                    return -1;
                
                _position = idx;
                return idx;
            }
            return -1;
        }

        private void UpdateLineColumn(int from, int to)
        {
            for (int i = from; i < to && i < _input.Length; i++)
            {
                if (_input[i] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
        }

        private char Current => _position < _input.Length ? _input[_position] : '\0';
        private char Peek(int offset = 1) => (_position + offset) < _input.Length ? _input[_position + offset] : '\0';

        private void Advance()
        {
            if (_position < _input.Length)
            {
                if (Current == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _position++;
            }
        }

        private void SkipWhitespaceAndComments()
        {
            while (_position < _input.Length)
            {
                if (char.IsWhiteSpace(Current))
                {
                    Advance();
                }
                else if (Current == '/' && Peek() == '/')
                {
                    // Line comment
                    while (_position < _input.Length && Current != '\n')
                        Advance();
                }
                else if (Current == '/' && Peek() == '*')
                {
                    // Block comment
                    Advance(); Advance(); // Skip /*
                    while (_position < _input.Length - 1 && !(Current == '*' && Peek() == '/'))
                        Advance();
                    if (_position < _input.Length - 1)
                    {
                        Advance(); Advance(); // Skip */
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private Token ReadToken()
        {
            if (_position >= _input.Length)
                return new Token(TokenType.EOF, "", _line, _column);

            char c = Current;
            int startLine = _line;
            int startCol = _column;

            // Single character tokens
            switch (c)
            {
                case '{':
                    Advance();
                    return new Token(TokenType.UIBlockStart, "{", startLine, startCol); // Nested brace
                case '}':
                    Advance();
                    return new Token(TokenType.BraceClose, "}", startLine, startCol);
                case '(':
                    Advance();
                    return new Token(TokenType.OpenParen, "(", startLine, startCol);
                case ')':
                    Advance();
                    return new Token(TokenType.CloseParen, ")", startLine, startCol);
                case ',':
                    Advance();
                    return new Token(TokenType.Comma, ",", startLine, startCol);
                case ';':
                    Advance();
                    return new Token(TokenType.Semicolon, ";", startLine, startCol);
                case '.':
                    Advance();
                    return new Token(TokenType.Dot, ".", startLine, startCol);
                case '+':
                    Advance();
                    return new Token(TokenType.Plus, "+", startLine, startCol);
                case '-':
                    // Could be minus or negative number
                    if (char.IsDigit(Peek()))
                        return ReadNumber();
                    Advance();
                    return new Token(TokenType.Minus, "-", startLine, startCol);
                case '*':
                    Advance();
                    return new Token(TokenType.Star, "*", startLine, startCol);
                case '/':
                    Advance();
                    return new Token(TokenType.Slash, "/", startLine, startCol);
                case '%':
                    Advance();
                    return new Token(TokenType.Percent, "%", startLine, startCol);
                case '=':
                    Advance();
                    if (Current == '=')
                    {
                        Advance();
                        return new Token(TokenType.EqualsEquals, "==", startLine, startCol);
                    }
                    return new Token(TokenType.Equals, "=", startLine, startCol);
                case '!':
                    Advance();
                    if (Current == '=')
                    {
                        Advance();
                        return new Token(TokenType.NotEquals, "!=", startLine, startCol);
                    }
                    return new Token(TokenType.Not, "!", startLine, startCol);
                case '<':
                    Advance();
                    if (Current == '=')
                    {
                        Advance();
                        return new Token(TokenType.LessEquals, "<=", startLine, startCol);
                    }
                    return new Token(TokenType.LessThan, "<", startLine, startCol);
                case '>':
                    Advance();
                    if (Current == '=')
                    {
                        Advance();
                        return new Token(TokenType.GreaterEquals, ">=", startLine, startCol);
                    }
                    return new Token(TokenType.GreaterThan, ">", startLine, startCol);
                case '&':
                    Advance();
                    if (Current == '&')
                    {
                        Advance();
                        return new Token(TokenType.And, "&&", startLine, startCol);
                    }
                    return new Token(TokenType.Error, "&", startLine, startCol);
                case '|':
                    Advance();
                    if (Current == '|')
                    {
                        Advance();
                        return new Token(TokenType.Or, "||", startLine, startCol);
                    }
                    return new Token(TokenType.Error, "|", startLine, startCol);
                case '"':
                case '\'':
                    return ReadString(c);
            }

            // Numbers
            if (char.IsDigit(c))
                return ReadNumber();

            // Identifiers and keywords
            if (char.IsLetter(c) || c == '_')
                return ReadIdentifier();

            // Unknown character
            Advance();
            return new Token(TokenType.Error, c.ToString(), startLine, startCol);
        }

        private Token ReadString(char quote)
        {
            int startLine = _line;
            int startCol = _column;
            Advance(); // Skip opening quote

            var sb = new StringBuilder();

            while (_position < _input.Length)
            {
                char c = Current;

                if (c == quote)
                {
                    // Check for escaped quote (doubled)
                    if (Peek() == quote)
                    {
                        sb.Append(quote);
                        Advance(); Advance();
                        continue;
                    }
                    Advance(); // Skip closing quote
                    return new Token(TokenType.String, sb.ToString(), startLine, startCol);
                }

                if (c == '\\')
                {
                    Advance();
                    if (_position < _input.Length)
                    {
                        char escaped = Current;
                        switch (escaped)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case '\\': sb.Append('\\'); break;
                            case '"': sb.Append('"'); break;
                            case '\'': sb.Append('\''); break;
                            default: sb.Append(escaped); break;
                        }
                        Advance();
                    }
                    continue;
                }

                sb.Append(c);
                Advance();
            }

            // Unterminated string
            return new Token(TokenType.Error, sb.ToString(), startLine, startCol);
        }

        private Token ReadNumber()
        {
            int startLine = _line;
            int startCol = _column;
            var sb = new StringBuilder();

            // Handle sign
            if (Current == '-' || Current == '+')
            {
                sb.Append(Current);
                Advance();
            }

            // Integer part
            while (_position < _input.Length && char.IsDigit(Current))
            {
                sb.Append(Current);
                Advance();
            }

            // Decimal part
            if (Current == '.' && char.IsDigit(Peek()))
            {
                sb.Append(Current);
                Advance();
                while (_position < _input.Length && char.IsDigit(Current))
                {
                    sb.Append(Current);
                    Advance();
                }
            }

            return new Token(TokenType.Number, sb.ToString(), startLine, startCol);
        }

        private Token ReadIdentifier()
        {
            int startLine = _line;
            int startCol = _column;
            var sb = new StringBuilder();

            while (_position < _input.Length && (char.IsLetterOrDigit(Current) || Current == '_'))
            {
                sb.Append(Current);
                Advance();
            }

            string text = sb.ToString();
            string lower = text.ToLowerInvariant();

            // Check for compound keywords (e.g., "end if", "else if")
            var compoundToken = TryReadCompoundKeyword(lower, startLine, startCol);
            if (compoundToken != null)
                return compoundToken;

            // Check for keywords
            if (Keywords.TryGetValue(lower, out TokenType type))
                return new Token(type, text, startLine, startCol);

            return new Token(TokenType.Identifier, text, startLine, startCol);
        }

        private Token TryReadCompoundKeyword(string firstWord, int startLine, int startCol)
        {
            if (!CompoundKeywords.TryGetValue(firstWord, out var secondWords))
                return null;

            // Save state
            int savedPos = _position;
            int savedLine = _line;
            int savedCol = _column;

            SkipWhitespaceAndComments();

            // Try to read next word
            string nextWord = PeekNextIdentifier();
            if (nextWord != null && secondWords.TryGetValue(nextWord, out var tokenInfo))
            {
                // Consume the second word since it's part of the compound keyword
                ReadNextIdentifier();
                return new Token(tokenInfo.type, tokenInfo.display, startLine, startCol);
            }

            // Restore position if not a compound keyword
            _position = savedPos;
            _line = savedLine;
            _column = savedCol;
            return null;
        }

        private string PeekNextIdentifier()
        {
            if (_position >= _input.Length || !(char.IsLetter(Current) || Current == '_'))
                return null;

            var sb = new StringBuilder();
            int pos = _position;
            while (pos < _input.Length && (char.IsLetterOrDigit(_input[pos]) || _input[pos] == '_'))
            {
                sb.Append(_input[pos]);
                pos++;
            }
            return sb.Length > 0 ? sb.ToString().ToLowerInvariant() : null;
        }

        private void ReadNextIdentifier()
        {
            while (_position < _input.Length && (char.IsLetterOrDigit(Current) || Current == '_'))
            {
                Advance();
            }
        }
    }

    public static class UIBlockDetector
    {
        public static bool ContainsUIBlock(string input)
        {
            return !string.IsNullOrEmpty(input) && input.Contains(Lexer.BLOCK_START);
        }

        public static string StripUIBlocks(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder();
            var lexer = new Lexer(input);
            int lastEnd = 0;

            while (true)
            {
                var tokens = lexer.NextUIBlock();
                if (tokens == null)
                    break;

                var range = lexer.GetConsumedRange();
                if (range.start > lastEnd)
                {
                    result.Append(input.Substring(lastEnd, range.start - lastEnd));
                }
                lastEnd = range.end;
            }

            if (lastEnd < input.Length)
            {
                result.Append(input.Substring(lastEnd));
            }

            return result.ToString().Trim();
        }
    }
}
