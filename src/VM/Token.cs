namespace GreyHackTerminalUI.VM
{
    public enum TokenType
    {
        // Block markers
        UIBlockStart,   // #UI{
        BraceClose,     // }
        
        // Delimiters
        OpenParen,      // (
        CloseParen,     // )
        Comma,          // ,
        Semicolon,      // ;
        Dot,            // .
        
        // Literals
        String,         // "text"
        Number,         // 123, 3.14
        True,           // true
        False,          // false
        Null,           // null
        
        // Identifiers and keywords
        Identifier,     // variable names, function names
        Var,            // var
        If,             // if
        Then,           // then
        Else,           // else
        ElseIf,         // else if (combined token)
        EndIf,          // end if
        While,          // while
        Do,             // do
        EndWhile,       // end while
        Return,         // return
        And,            // and, &&
        Or,             // or, ||
        Not,            // not, !
        
        // Operators
        Plus,           // +
        Minus,          // -
        Star,           // *
        Slash,          // /
        Percent,        // %
        Equals,         // =
        EqualsEquals,   // ==
        NotEquals,      // !=
        LessThan,       // <
        GreaterThan,    // >
        LessEquals,     // <=
        GreaterEquals,  // >=
        
        // Special
        EOF,
        Error,
        Newline         // For optional line tracking
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int Line { get; }
        public int Column { get; }

        public Token(TokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"{Type}({Value}) @ {Line}:{Column}";
    }
}
