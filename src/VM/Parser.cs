using System;
using System.Collections.Generic;

namespace GreyHackTerminalUI.VM
{
    public class Parser
    {
        private List<Token> _tokens;
        private int _current;

        public ProgramNode Parse(List<Token> tokens)
        {
            _tokens = tokens;
            _current = 0;

            var program = new ProgramNode();

            // Skip the #UI{ token
            if (Check(TokenType.UIBlockStart))
                Advance();

            while (!IsAtEnd() && !Check(TokenType.BraceClose))
            {
                try
                {
                    var stmt = ParseStatement();
                    if (stmt != null)
                        program.Statements.Add(stmt);
                }
                catch (ParseException)
                {
                    // Synchronize on errors - skip to next statement
                    Synchronize();
                }
            }

            return program;
        }

        private StatementNode ParseStatement()
        {
            if (Check(TokenType.Var))
                return ParseVarDecl();
            if (Check(TokenType.If))
                return ParseIf();
            if (Check(TokenType.While))
                return ParseWhile();
            if (Check(TokenType.Return))
                return ParseReturn();

            return ParseExpressionStatement();
        }

        private VarDeclNode ParseVarDecl()
        {
            Token varToken = Advance(); // consume 'var'
            
            Token name = Consume(TokenType.Identifier, "Expected variable name after 'var'");
            
            ExpressionNode initializer = null;
            if (Match(TokenType.Equals))
            {
                initializer = ParseExpression();
            }

            // Semicolons are optional
            Match(TokenType.Semicolon);

            return new VarDeclNode
            {
                Name = name.Value,
                Initializer = initializer,
                Line = varToken.Line,
                Column = varToken.Column
            };
        }

        private IfNode ParseIf()
        {
            Token ifToken = Advance(); // consume 'if'
            
            // Optional parentheses around condition
            bool hasParen = Match(TokenType.OpenParen);
            ExpressionNode condition = ParseExpression();
            if (hasParen)
                Consume(TokenType.CloseParen, "Expected ')' after if condition");

            Consume(TokenType.Then, "Expected 'then' after if condition");

            var node = new IfNode
            {
                Condition = condition,
                Line = ifToken.Line,
                Column = ifToken.Column
            };

            // Parse then branch
            while (!IsAtEnd() && !Check(TokenType.ElseIf) && !Check(TokenType.Else) && !Check(TokenType.EndIf))
            {
                node.ThenBranch.Add(ParseStatement());
            }

            // Parse else if branches
            while (Match(TokenType.ElseIf))
            {
                var elseIfBranch = new ElseIfBranch();
                
                hasParen = Match(TokenType.OpenParen);
                elseIfBranch.Condition = ParseExpression();
                if (hasParen)
                    Consume(TokenType.CloseParen, "Expected ')' after else if condition");
                
                Consume(TokenType.Then, "Expected 'then' after else if condition");

                while (!IsAtEnd() && !Check(TokenType.ElseIf) && !Check(TokenType.Else) && !Check(TokenType.EndIf))
                {
                    elseIfBranch.Body.Add(ParseStatement());
                }

                node.ElseIfBranches.Add(elseIfBranch);
            }

            // Parse else branch
            if (Match(TokenType.Else))
            {
                while (!IsAtEnd() && !Check(TokenType.EndIf))
                {
                    node.ElseBranch.Add(ParseStatement());
                }
            }

            Consume(TokenType.EndIf, "Expected 'end if' after if statement");

            return node;
        }

        private WhileNode ParseWhile()
        {
            Token whileToken = Advance(); // consume 'while'
            
            // Optional parentheses around condition
            bool hasParen = Match(TokenType.OpenParen);
            ExpressionNode condition = ParseExpression();
            if (hasParen)
                Consume(TokenType.CloseParen, "Expected ')' after while condition");

            Consume(TokenType.Do, "Expected 'do' after while condition");

            var node = new WhileNode
            {
                Condition = condition,
                Line = whileToken.Line,
                Column = whileToken.Column
            };

            while (!IsAtEnd() && !Check(TokenType.EndWhile))
            {
                node.Body.Add(ParseStatement());
            }

            Consume(TokenType.EndWhile, "Expected 'end while' after while loop");

            return node;
        }

        private ReturnNode ParseReturn()
        {
            Token returnToken = Advance(); // consume 'return'
            
            ExpressionNode value = null;
            if (!Check(TokenType.Semicolon) && !IsStatementEnd())
            {
                value = ParseExpression();
            }

            // Semicolons are optional
            Match(TokenType.Semicolon);

            return new ReturnNode
            {
                Value = value,
                Line = returnToken.Line,
                Column = returnToken.Column
            };
        }

        private StatementNode ParseExpressionStatement()
        {
            ExpressionNode expr = ParseExpression();

            // Check for assignment
            if (Match(TokenType.Equals))
            {
                ExpressionNode value = ParseExpression();

                // Semicolons are optional
                Match(TokenType.Semicolon);

                return new AssignmentNode
                {
                    Target = expr,
                    Value = value,
                    Line = expr.Line,
                    Column = expr.Column
                };
            }

            // Semicolons are optional
            Match(TokenType.Semicolon);

            return new ExpressionStatementNode
            {
                Expression = expr,
                Line = expr.Line,
                Column = expr.Column
            };
        }
            
        private ExpressionNode ParseExpression()
        {
            return ParseOr();
        }

        private ExpressionNode ParseOr()
        {
            ExpressionNode left = ParseAnd();

            while (Match(TokenType.Or))
            {
                Token op = Previous();
                ExpressionNode right = ParseAnd();
                left = new BinaryNode
                {
                    Left = left,
                    Operator = BinaryOp.Or,
                    Right = right,
                    Line = op.Line,
                    Column = op.Column
                };
            }

            return left;
        }

        private ExpressionNode ParseAnd()
        {
            ExpressionNode left = ParseEquality();

            while (Match(TokenType.And))
            {
                Token op = Previous();
                ExpressionNode right = ParseEquality();
                left = new BinaryNode
                {
                    Left = left,
                    Operator = BinaryOp.And,
                    Right = right,
                    Line = op.Line,
                    Column = op.Column
                };
            }

            return left;
        }

        private ExpressionNode ParseEquality()
        {
            ExpressionNode left = ParseComparison();

            while (Match(TokenType.EqualsEquals, TokenType.NotEquals))
            {
                Token op = Previous();
                BinaryOp binOp = op.Type == TokenType.EqualsEquals ? BinaryOp.Eq : BinaryOp.Ne;
                ExpressionNode right = ParseComparison();
                left = new BinaryNode
                {
                    Left = left,
                    Operator = binOp,
                    Right = right,
                    Line = op.Line,
                    Column = op.Column
                };
            }

            return left;
        }

        private ExpressionNode ParseComparison()
        {
            ExpressionNode left = ParseTerm();

            while (Match(TokenType.LessThan, TokenType.GreaterThan, TokenType.LessEquals, TokenType.GreaterEquals))
            {
                Token op = Previous();
                BinaryOp binOp = op.Type switch
                {
                    TokenType.LessThan => BinaryOp.Lt,
                    TokenType.GreaterThan => BinaryOp.Gt,
                    TokenType.LessEquals => BinaryOp.Le,
                    TokenType.GreaterEquals => BinaryOp.Ge,
                    _ => throw new ParseException("Invalid comparison operator")
                };
                ExpressionNode right = ParseTerm();
                left = new BinaryNode
                {
                    Left = left,
                    Operator = binOp,
                    Right = right,
                    Line = op.Line,
                    Column = op.Column
                };
            }

            return left;
        }

        private ExpressionNode ParseTerm()
        {
            ExpressionNode left = ParseFactor();

            while (Match(TokenType.Plus, TokenType.Minus))
            {
                Token op = Previous();
                BinaryOp binOp = op.Type == TokenType.Plus ? BinaryOp.Add : BinaryOp.Sub;
                ExpressionNode right = ParseFactor();
                left = new BinaryNode
                {
                    Left = left,
                    Operator = binOp,
                    Right = right,
                    Line = op.Line,
                    Column = op.Column
                };
            }

            return left;
        }

        private ExpressionNode ParseFactor()
        {
            ExpressionNode left = ParseUnary();

            while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
            {
                Token op = Previous();
                BinaryOp binOp = op.Type switch
                {
                    TokenType.Star => BinaryOp.Mul,
                    TokenType.Slash => BinaryOp.Div,
                    TokenType.Percent => BinaryOp.Mod,
                    _ => throw new ParseException("Invalid factor operator")
                };
                ExpressionNode right = ParseUnary();
                left = new BinaryNode
                {
                    Left = left,
                    Operator = binOp,
                    Right = right,
                    Line = op.Line,
                    Column = op.Column
                };
            }

            return left;
        }

        private ExpressionNode ParseUnary()
        {
            if (Match(TokenType.Not, TokenType.Minus))
            {
                Token op = Previous();
                UnaryOp unaryOp = op.Type == TokenType.Not ? UnaryOp.Not : UnaryOp.Negate;
                ExpressionNode operand = ParseUnary();
                return new UnaryNode
                {
                    Operator = unaryOp,
                    Operand = operand,
                    Line = op.Line,
                    Column = op.Column
                };
            }

            return ParseCall();
        }

        private ExpressionNode ParseCall()
        {
            ExpressionNode expr = ParsePrimary();

            while (true)
            {
                if (Match(TokenType.OpenParen))
                {
                    expr = FinishCall(expr);
                }
                else if (Match(TokenType.Dot))
                {
                    Token name = Consume(TokenType.Identifier, "Expected property name after '.'");
                    expr = new MemberAccessNode
                    {
                        Object = expr,
                        Member = name.Value,
                        Line = name.Line,
                        Column = name.Column
                    };
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        private CallNode FinishCall(ExpressionNode callee)
        {
            var call = new CallNode
            {
                Callee = callee,
                Line = callee.Line,
                Column = callee.Column
            };

            if (!Check(TokenType.CloseParen))
            {
                do
                {
                    call.Arguments.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }

            Consume(TokenType.CloseParen, "Expected ')' after arguments");

            return call;
        }

        private ExpressionNode ParsePrimary()
        {
            Token token = Peek();

            if (Match(TokenType.Number))
            {
                string val = Previous().Value;
                object numVal = val.Contains(".") 
                    ? (object)double.Parse(val, System.Globalization.CultureInfo.InvariantCulture)
                    : (object)int.Parse(val);
                return new LiteralNode
                {
                    Value = numVal,
                    LiteralType = LiteralType.Number,
                    Line = token.Line,
                    Column = token.Column
                };
            }

            if (Match(TokenType.String))
            {
                return new LiteralNode
                {
                    Value = Previous().Value,
                    LiteralType = LiteralType.String,
                    Line = token.Line,
                    Column = token.Column
                };
            }

            if (Match(TokenType.True))
            {
                return new LiteralNode
                {
                    Value = true,
                    LiteralType = LiteralType.Boolean,
                    Line = token.Line,
                    Column = token.Column
                };
            }

            if (Match(TokenType.False))
            {
                return new LiteralNode
                {
                    Value = false,
                    LiteralType = LiteralType.Boolean,
                    Line = token.Line,
                    Column = token.Column
                };
            }

            if (Match(TokenType.Null))
            {
                return new LiteralNode
                {
                    Value = null,
                    LiteralType = LiteralType.Null,
                    Line = token.Line,
                    Column = token.Column
                };
            }

            if (Match(TokenType.Identifier))
            {
                return new IdentifierNode
                {
                    Name = Previous().Value,
                    Line = token.Line,
                    Column = token.Column
                };
            }

            if (Match(TokenType.OpenParen))
            {
                ExpressionNode expr = ParseExpression();
                Consume(TokenType.CloseParen, "Expected ')' after expression");
                return new GroupNode
                {
                    Expression = expr,
                    Line = token.Line,
                    Column = token.Column
                };
            }

            throw new ParseException($"Unexpected token: {token}");
        }

        private bool IsAtEnd() => Peek().Type == TokenType.EOF;

        private bool IsStatementEnd()
        {
            if (IsAtEnd()) return true;
            var type = Peek().Type;
            return type == TokenType.BraceClose ||
                   type == TokenType.If ||
                   type == TokenType.Else ||
                   type == TokenType.ElseIf ||
                   type == TokenType.EndIf ||
                   type == TokenType.While ||
                   type == TokenType.EndWhile ||
                   type == TokenType.Return ||
                   type == TokenType.Var;
        }
        
        private Token Peek() => _tokens[_current];
        
        private Token Previous() => _tokens[_current - 1];

        private Token Advance()
        {
            if (!IsAtEnd()) _current++;
            return Previous();
        }

        private bool Check(TokenType type)
        {
            if (IsAtEnd()) return false;
            return Peek().Type == type;
        }

        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            throw new ParseException($"{message} at {Peek()}");
        }

        private void Synchronize()
        {
            Advance();

            while (!IsAtEnd())
            {
                if (Previous().Type == TokenType.Semicolon) return;

                switch (Peek().Type)
                {
                    case TokenType.Var:
                    case TokenType.If:
                    case TokenType.While:
                    case TokenType.Return:
                        return;
                }

                Advance();
            }
        }
    }

    public class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }
}
