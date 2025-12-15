using Xunit;
using GreyHackTerminalUI.VM;

namespace GreyHackTerminalUI.Tests.VM
{
    public class LexerTests
    {
        [Fact]
        public void NextUIBlock_SimpleBlock_ReturnsTokens()
        {
            // Arrange
            var input = "#UI{ var x = 10 }";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.NextUIBlock();

            // Assert
            Assert.NotNull(tokens);
            Assert.Contains(tokens, t => t.Type == TokenType.UIBlockStart);
            Assert.Contains(tokens, t => t.Type == TokenType.Var);
            Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "x");
            Assert.Contains(tokens, t => t.Type == TokenType.Equals);
            Assert.Contains(tokens, t => t.Type == TokenType.Number && t.Value == "10");
        }

        [Fact]
        public void NextUIBlock_NoBlock_ReturnsNull()
        {
            // Arrange
            var input = "no UI block here";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.NextUIBlock();

            // Assert
            Assert.Null(tokens);
        }

        [Fact]
        public void NextUIBlock_MultipleBlocks_ReturnsEachSequentially()
        {
            // Arrange
            var input = "#UI{ var x = 1 } some text #UI{ var y = 2 }";
            var lexer = new Lexer(input);

            // Act
            var block1 = lexer.NextUIBlock();
            var block2 = lexer.NextUIBlock();
            var block3 = lexer.NextUIBlock();

            // Assert
            Assert.NotNull(block1);
            Assert.NotNull(block2);
            Assert.Null(block3);
        }

        [Fact]
        public void NextUIBlock_WithComments_SkipsComments()
        {
            // Arrange
            var input = "#UI{ // this is a comment\nvar x = 10 }";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.NextUIBlock();

            // Assert
            Assert.NotNull(tokens);
            Assert.Contains(tokens, t => t.Type == TokenType.Var);
            Assert.DoesNotContain(tokens, t => t.Value.Contains("comment"));
        }

        [Fact]
        public void NextUIBlock_WithStrings_TokenizesCorrectly()
        {
            // Arrange
            var input = "#UI{ var name = \"Hello World\" }";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.NextUIBlock();

            // Assert
            Assert.NotNull(tokens);
            Assert.Contains(tokens, t => t.Type == TokenType.String && t.Value == "Hello World");
        }

        [Fact]
        public void NextUIBlock_WithNestedBraces_HandlesCorrectly()
        {
            // Arrange
            var input = "#UI{ if x > 10 then var y = 5 end if }";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.NextUIBlock();

            // Assert
            Assert.NotNull(tokens);
            Assert.Contains(tokens, t => t.Type == TokenType.If);
            Assert.Contains(tokens, t => t.Type == TokenType.Then);
            Assert.Contains(tokens, t => t.Type == TokenType.EndIf);
        }

        [Theory]
        [InlineData("var", TokenType.Var)]
        [InlineData("if", TokenType.If)]
        [InlineData("then", TokenType.Then)]
        [InlineData("else", TokenType.Else)]
        [InlineData("while", TokenType.While)]
        [InlineData("do", TokenType.Do)]
        [InlineData("return", TokenType.Return)]
        [InlineData("and", TokenType.And)]
        [InlineData("or", TokenType.Or)]
        [InlineData("not", TokenType.Not)]
        public void NextUIBlock_Keywords_RecognizedCorrectly(string keyword, TokenType expectedType)
        {
            // Arrange
            var input = $"#UI{{ {keyword} }}";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.NextUIBlock();

            // Assert
            Assert.NotNull(tokens);
            Assert.Contains(tokens, t => t.Type == expectedType);
        }

        [Theory]
        [InlineData("+", TokenType.Plus)]
        [InlineData("-", TokenType.Minus)]
        [InlineData("*", TokenType.Star)]
        [InlineData("/", TokenType.Slash)]
        [InlineData("=", TokenType.Equals)]
        [InlineData("==", TokenType.EqualsEquals)]
        [InlineData("!=", TokenType.NotEquals)]
        [InlineData("<", TokenType.LessThan)]
        [InlineData(">", TokenType.GreaterThan)]
        [InlineData("<=", TokenType.LessEquals)]
        [InlineData(">=", TokenType.GreaterEquals)]
        public void NextUIBlock_Operators_TokenizedCorrectly(string op, TokenType expectedType)
        {
            // Arrange
            var input = $"#UI{{ x {op} y }}";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.NextUIBlock();

            // Assert
            Assert.NotNull(tokens);
            Assert.Contains(tokens, t => t.Type == expectedType);
        }
    }
}
