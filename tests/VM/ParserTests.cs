using Xunit;
using GreyHackTerminalUI.VM;
using System.Linq;

namespace GreyHackTerminalUI.Tests.VM
{
    public class ParserTests
    {
        [Fact]
        public void Parse_SimpleVarDeclaration_CreatesVarDeclNode()
        {
            // Arrange
            var tokens = TokenizeCode("var x = 10");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var varDecl = Assert.IsType<VarDeclNode>(ast.Statements[0]);
            Assert.Equal("x", varDecl.Name);
            Assert.NotNull(varDecl.Initializer);
        }

        [Fact]
        public void Parse_Assignment_CreatesAssignmentNode()
        {
            // Arrange
            var tokens = TokenizeCode("x = 20");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var assignment = Assert.IsType<AssignmentNode>(ast.Statements[0]);
        }

        [Fact]
        public void Parse_IfStatement_CreatesIfNode()
        {
            // Arrange
            var tokens = TokenizeCode("if x > 10 then var y = 5 end if");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var ifNode = Assert.IsType<IfNode>(ast.Statements[0]);
            Assert.NotNull(ifNode.Condition);
            Assert.NotEmpty(ifNode.ThenBranch);
        }

        [Fact]
        public void Parse_IfElseStatement_CreatesIfNodeWithElse()
        {
            // Arrange
            var tokens = TokenizeCode("if x > 10 then var y = 5 else var y = 0 end if");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var ifNode = Assert.IsType<IfNode>(ast.Statements[0]);
            Assert.NotEmpty(ifNode.ThenBranch);
            Assert.NotEmpty(ifNode.ElseBranch);
        }

        [Fact]
        public void Parse_ElseIfStatement_CreatesIfNodeWithElseIf()
        {
            // Arrange
            var tokens = TokenizeCode("if x > 10 then var y = 5 else if x > 5 then var y = 3 else var y = 0 end if");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var ifNode = Assert.IsType<IfNode>(ast.Statements[0]);
            Assert.Single(ifNode.ElseIfBranches);
        }

        [Fact]
        public void Parse_WhileLoop_CreatesWhileNode()
        {
            // Arrange
            var tokens = TokenizeCode("while x < 10 do x = x + 1 end while");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var whileNode = Assert.IsType<WhileNode>(ast.Statements[0]);
            Assert.NotNull(whileNode.Condition);
            Assert.NotEmpty(whileNode.Body);
        }

        [Fact]
        public void Parse_FunctionCall_CreatesCallNode()
        {
            // Arrange
            var tokens = TokenizeCode("print(\"hello\")");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var exprStmt = Assert.IsType<ExpressionStatementNode>(ast.Statements[0]);
            var call = Assert.IsType<CallNode>(exprStmt.Expression);
            Assert.Single(call.Arguments);
        }

        [Fact]
        public void Parse_MethodCall_CreatesCallNodeWithMemberAccess()
        {
            // Arrange
            var tokens = TokenizeCode("Canvas.show()");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var exprStmt = Assert.IsType<ExpressionStatementNode>(ast.Statements[0]);
            var call = Assert.IsType<CallNode>(exprStmt.Expression);
            var memberAccess = Assert.IsType<MemberAccessNode>(call.Callee);
            Assert.Equal("show", memberAccess.Member);
        }

        [Fact]
        public void Parse_BinaryExpression_CreatesBinaryNode()
        {
            // Arrange
            var tokens = TokenizeCode("var result = 10 + 5 * 2");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var varDecl = Assert.IsType<VarDeclNode>(ast.Statements[0]);
            var binary = Assert.IsType<BinaryNode>(varDecl.Initializer);
            Assert.Equal(BinaryOp.Add, binary.Operator);
        }

        [Fact]
        public void Parse_UnaryExpression_CreatesUnaryNode()
        {
            // Arrange
            var tokens = TokenizeCode("var x = not true");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var varDecl = Assert.IsType<VarDeclNode>(ast.Statements[0]);
            var unary = Assert.IsType<UnaryNode>(varDecl.Initializer);
            Assert.Equal(UnaryOp.Not, unary.Operator);
        }

        [Fact]
        public void Parse_PropertyAccess_CreatesMemberAccessNode()
        {
            // Arrange
            var tokens = TokenizeCode("var w = Canvas.width");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var varDecl = Assert.IsType<VarDeclNode>(ast.Statements[0]);
            var memberAccess = Assert.IsType<MemberAccessNode>(varDecl.Initializer);
            Assert.Equal("width", memberAccess.Member);
        }

        [Fact]
        public void Parse_ReturnStatement_CreatesReturnNode()
        {
            // Arrange
            var tokens = TokenizeCode("return 42");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Single(ast.Statements);
            var returnNode = Assert.IsType<ReturnNode>(ast.Statements[0]);
            Assert.NotNull(returnNode.Value);
        }

        [Fact]
        public void Parse_MultipleStatements_CreatesMultipleNodes()
        {
            // Arrange
            var tokens = TokenizeCode("var x = 10\nvar y = 20\nx = x + y");
            var parser = new Parser();

            // Act
            var ast = parser.Parse(tokens);

            // Assert
            Assert.Equal(3, ast.Statements.Count);
        }

        [Fact]
        public void Parse_SemicolonsOptional_ParsesSuccessfully()
        {
            // Arrange
            var tokensWithSemi = TokenizeCode("var x = 10; var y = 20;");
            var tokensWithout = TokenizeCode("var x = 10\nvar y = 20");
            var parser1 = new Parser();
            var parser2 = new Parser();

            // Act
            var ast1 = parser1.Parse(tokensWithSemi);
            var ast2 = parser2.Parse(tokensWithout);

            // Assert
            Assert.Equal(2, ast1.Statements.Count);
            Assert.Equal(2, ast2.Statements.Count);
        }

        // Helper method to tokenize code
        private static System.Collections.Generic.List<Token> TokenizeCode(string code)
        {
            var input = $"#UI{{ {code} }}";
            var lexer = new Lexer(input);
            return lexer.NextUIBlock();
        }
    }
}
