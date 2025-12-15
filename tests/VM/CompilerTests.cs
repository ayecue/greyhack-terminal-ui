using Xunit;
using GreyHackTerminalUI.VM;

namespace GreyHackTerminalUI.Tests.VM
{
    public class CompilerTests
    {
        [Fact]
        public void Compile_SimpleVarDeclaration_GeneratesBytecode()
        {
            // Arrange
            var ast = ParseCode("var x = 10");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.PUSH_CONST);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.STORE_VAR);
        }

        [Fact]
        public void Compile_Assignment_GeneratesBytecode()
        {
            // Arrange
            var ast = ParseCode("x = 20");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.PUSH_CONST);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.STORE_VAR);
        }

        [Fact]
        public void Compile_BinaryExpression_GeneratesBytecode()
        {
            // Arrange
            var ast = ParseCode("var x = 10 + 5");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.ADD);
        }

        [Fact]
        public void Compile_IfStatement_GeneratesJumpInstructions()
        {
            // Arrange
            var ast = ParseCode("if x > 10 then var y = 5 end if");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.JUMP_IF_FALSE);
        }

        [Fact]
        public void Compile_WhileLoop_GeneratesJumpInstructions()
        {
            // Arrange
            var ast = ParseCode("while x < 10 do x = x + 1 end while");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.JUMP_IF_FALSE);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.JUMP);
        }

        [Fact]
        public void Compile_FunctionCall_GeneratesCallInstruction()
        {
            // Arrange
            var ast = ParseCode("print(\"hello\")");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.CALL);
        }

        [Fact]
        public void Compile_MethodCall_GeneratesCallMethodInstruction()
        {
            // Arrange
            var ast = ParseCode("Canvas.show()");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.CALL_METHOD);
        }

        [Fact]
        public void Compile_UnaryExpression_GeneratesUnaryInstruction()
        {
            // Arrange
            var ast = ParseCode("var x = not true");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.NOT);
        }

        [Fact]
        public void Compile_PropertyAccess_GeneratesGetMember()
        {
            // Arrange
            var ast = ParseCode("var w = Canvas.width");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.NotEmpty(chunk.Code);
            Assert.Contains(chunk.Code, b => b == (byte)OpCode.GET_MEMBER);
        }

        [Fact]
        public void Compile_Constants_AddedToChunk()
        {
            // Arrange
            var ast = ParseCode("var x = 42");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.Contains(chunk.Constants, c => (c is int i && i == 42) || (c is double d && d == 42.0));
        }

        [Fact]
        public void Compile_Names_AddedToChunk()
        {
            // Arrange
            var ast = ParseCode("var myVariable = 10");
            var compiler = new Compiler();

            // Act
            var chunk = compiler.Compile(ast);

            // Assert
            Assert.Contains(chunk.Names, n => n == "myVariable");
        }

        // Helper method
        private static ProgramNode ParseCode(string code)
        {
            var input = $"#UI{{ {code} }}";
            var lexer = new Lexer(input);
            var tokens = lexer.NextUIBlock();
            var parser = new Parser();
            return parser.Parse(tokens);
        }
    }
}
