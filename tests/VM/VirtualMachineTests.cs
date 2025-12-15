using System;
using Xunit;
using GreyHackTerminalUI.VM;
using Moq;

namespace GreyHackTerminalUI.Tests.VM
{
    public class VirtualMachineTests
    {
        [Fact]
        public void Execute_SimpleVarDeclaration_StoresVariable()
        {
            // Arrange
            var code = "var x = 42";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            var x = context.GetVariable("x");
            Assert.True(x is int || x is double);
            Assert.Equal(42, Convert.ToDouble(x));
        }

        [Fact]
        public void Execute_Assignment_UpdatesVariable()
        {
            // Arrange
            var code = "var x = 10\nx = 20";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            var x = context.GetVariable("x");
            Assert.Equal(20, Convert.ToDouble(x));
        }

        [Fact]
        public void Execute_BinaryMath_CalculatesCorrectly()
        {
            // Arrange
            var code = "var result = 10 + 5 * 2";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(20.0, context.GetVariable("result"));
        }

        [Theory]
        [InlineData("var x = 10 + 5", 15.0)]
        [InlineData("var x = 10 - 5", 5.0)]
        [InlineData("var x = 10 * 5", 50.0)]
        [InlineData("var x = 10 / 5", 2.0)]
        public void Execute_ArithmeticOperators_WorkCorrectly(string code, double expected)
        {
            // Arrange
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expected, context.GetVariable("x"));
        }

        [Theory]
        [InlineData("var x = 10 > 5", true)]
        [InlineData("var x = 10 < 5", false)]
        [InlineData("var x = 10 >= 10", true)]
        [InlineData("var x = 10 <= 5", false)]
        [InlineData("var x = 10 == 10", true)]
        [InlineData("var x = 10 != 5", true)]
        public void Execute_ComparisonOperators_WorkCorrectly(string code, bool expected)
        {
            // Arrange
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expected, context.GetVariable("x"));
        }

        [Fact]
        public void Execute_IfStatement_TrueBranch_ExecutesCorrectly()
        {
            // Arrange
            var code = "var x = 10\nif x > 5 then var y = 100 end if";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            var y = context.GetVariable("y");
            Assert.Equal(100, Convert.ToDouble(y));
        }

        [Fact]
        public void Execute_IfStatement_FalseBranch_SkipsCode()
        {
            // Arrange
            var code = "var x = 3\nif x > 5 then var y = 100 end if";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Null(context.GetVariable("y"));
        }

        [Fact]
        public void Execute_IfElseStatement_ExecutesCorrectBranch()
        {
            // Arrange
            var code = "var x = 3\nif x > 5 then var y = 100 else var y = 50 end if";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            var y = context.GetVariable("y");
            Assert.Equal(50, Convert.ToDouble(y));
        }

        [Fact]
        public void Execute_WhileLoop_ExecutesMultipleTimes()
        {
            // Arrange
            var code = "var x = 0\nwhile x < 5 do x = x + 1 end while";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(5.0, context.GetVariable("x"));
        }

        [Fact]
        public void Execute_NotOperator_NegatesBooleans()
        {
            // Arrange
            var code = "var x = not true\nvar y = not false";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.False((bool)context.GetVariable("x"));
            Assert.True((bool)context.GetVariable("y"));
        }

        [Fact]
        public void Execute_AndOperator_WorksCorrectly()
        {
            // Arrange
            var code = "var x = true and true\nvar y = true and false";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.True((bool)context.GetVariable("x"));
            Assert.False((bool)context.GetVariable("y"));
        }

        [Fact]
        public void Execute_OrOperator_WorksCorrectly()
        {
            // Arrange
            var code = "var x = true or false\nvar y = false or false";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.True((bool)context.GetVariable("x"));
            Assert.False((bool)context.GetVariable("y"));
        }

        [Fact]
        public void Execute_IntrinsicFunction_CallsCorrectly()
        {
            // Arrange
            var code = "var x = floor(3.7)";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3.0, context.GetVariable("x"));
        }

        [Fact]
        public void Execute_HasInContext_DetectsVariables()
        {
            // Arrange
            var code = "var exists = hasInContext(\"x\")\nvar x = 10\nvar existsNow = hasInContext(\"x\")";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.False((bool)context.GetVariable("exists"));
            Assert.True((bool)context.GetVariable("existsNow"));
        }

        [Fact]
        public void Execute_MethodCall_CallsMockedCanvas()
        {
            // Arrange
            var code = "Canvas.show()\nCanvas.clear()";
            var (chunk, context, intrinsics) = CompileCode(code);
            
            // Set up mock canvas methods
            var showCalled = false;
            var clearCalled = false;
            intrinsics.RegisterObject(CreateMockCanvasObject(() => showCalled = true, () => clearCalled = true));
            context.SetGlobal("Canvas", "Canvas");
            
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.True(showCalled);
            Assert.True(clearCalled);
        }

        [Fact]
        public void Execute_PropertyAccess_ReturnsValue()
        {
            // Arrange
            var code = "var w = Canvas.width";
            var (chunk, context, intrinsics) = CompileCode(code);
            
            // Set up mock canvas property
            var canvas = new IntrinsicObject("Canvas");
            canvas.Getters["width"] = (target, ctx) => 320.0;
            intrinsics.RegisterObject(canvas);
            context.SetGlobal("Canvas", "Canvas");
            
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(320.0, context.GetVariable("w"));
        }

        [Fact]
        public void Execute_PersistentContext_MaintainsVariables()
        {
            // Arrange
            var code1 = "var x = 10";
            var code2 = "x = x + 5";
            var (chunk1, context, intrinsics) = CompileCode(code1);
            var (chunk2, _, _) = CompileCode(code2);
            var vm = new VirtualMachine(intrinsics);

            // Act
            vm.Execute(chunk1, context);
            vm.Execute(chunk2, context);

            // Assert
            Assert.Equal(15.0, context.GetVariable("x"));
        }

        [Fact]
        public void Execute_StringConcatenation_WorksCorrectly()
        {
            // Arrange
            var code = "var message = \"Hello\" + \" \" + \"World\"";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Hello World", context.GetVariable("message"));
        }

        [Fact]
        public void Execute_ReturnStatement_ReturnsValue()
        {
            // Arrange
            var code = "var x = 10\nreturn x * 2";
            var (chunk, context, intrinsics) = CompileCode(code);
            var vm = new VirtualMachine(intrinsics);

            // Act
            var result = vm.Execute(chunk, context);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(20.0, result.ReturnValue);
        }

        // Helper methods
        private static (CompiledChunk chunk, VMContext context, IntrinsicRegistry intrinsics) CompileCode(string code)
        {
            var input = $"#UI{{ {code} }}";
            var lexer = new Lexer(input);
            var tokens = lexer.NextUIBlock();
            var parser = new Parser();
            var ast = parser.Parse(tokens);
            var compiler = new Compiler();
            var chunk = compiler.Compile(ast);
            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();
            return (chunk, context, intrinsics);
        }

        private static IntrinsicObject CreateMockCanvasObject(System.Action onShow, System.Action onClear)
        {
            var canvas = new IntrinsicObject("Canvas");
            canvas.Methods["show"] = (target, args, ctx) => { onShow(); return null; };
            canvas.Methods["clear"] = (target, args, ctx) => { onClear(); return null; };
            return canvas;
        }
    }
}
