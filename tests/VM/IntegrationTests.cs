using System;
using Xunit;
using GreyHackTerminalUI.VM;

namespace GreyHackTerminalUI.Tests.VM
{
    public class IntegrationTests
    {
        [Fact]
        public void FullPipeline_SimpleAnimation_ExecutesCorrectly()
        {
            // Arrange
            var code = @"
                if not hasInContext(""x"") then
                    var x = 0
                end if
                x = x + 1
                if x > 10 then
                    x = 0
                end if
            ";
            
            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act - Execute multiple times to simulate animation frames
            for (int i = 0; i < 15; i++)
            {
                var result = ExecuteCode(code, context, intrinsics);
                Assert.True(result.Success, $"Execution failed at frame {i}: {result.Error}");
            }

            // Assert - x should have wrapped back to 4 (0->11, then reset to 0->4)
            var x = Convert.ToDouble(context.GetVariable("x"));
            Assert.Equal(4.0, x);
        }

        [Fact]
        public void FullPipeline_DrawCircles_CallsCanvasMethods()
        {
            // Arrange
            var code = @"
                Canvas.clear()
                var i = 0
                while i < 3 do
                    Canvas.drawCircle(100 + i * 50, 100, 20, ""#FF0000"")
                    i = i + 1
                end while
                Canvas.render()
            ";

            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();
            var canvas = CreateMockCanvas();
            intrinsics.RegisterObject(canvas);
            context.SetGlobal("Canvas", "Canvas");

            // Act
            var result = ExecuteCode(code, context, intrinsics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, canvas.CallCounts["clear"]);
            Assert.Equal(3, canvas.CallCounts["drawCircle"]);
            Assert.Equal(1, canvas.CallCounts["render"]);
        }

        [Fact]
        public void FullPipeline_ComplexMath_CalculatesCorrectly()
        {
            // Arrange
            var code = @"
                var radius = 10
                var area = 3.14159 * radius * radius
                var circumference = 2 * 3.14159 * radius
                var ratio = area / circumference
            ";

            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act
            var result = ExecuteCode(code, context, intrinsics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(314.159, (double)context.GetVariable("area"), 3);
            Assert.Equal(62.8318, (double)context.GetVariable("circumference"), 3);
            Assert.Equal(5.0, (double)context.GetVariable("ratio"), 3);
        }

        [Fact]
        public void FullPipeline_NestedLoops_ExecutesCorrectly()
        {
            // Arrange
            var code = @"
                var sum = 0
                var i = 0
                while i < 5 do
                    var j = 0
                    while j < 5 do
                        sum = sum + 1
                        j = j + 1
                    end while
                    i = i + 1
                end while
            ";

            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act
            var result = ExecuteCode(code, context, intrinsics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(25.0, context.GetVariable("sum"));
        }

        [Fact]
        public void FullPipeline_NestedIfStatements_ExecutesCorrectly()
        {
            // Arrange
            var code = @"
                var x = 15
                var category = """"
                if x < 10 then
                    category = ""small""
                else if x < 20 then
                    if x < 15 then
                        category = ""medium-low""
                    else
                        category = ""medium-high""
                    end if
                else
                    category = ""large""
                end if
            ";

            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act
            var result = ExecuteCode(code, context, intrinsics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("medium-high", context.GetVariable("category"));
        }

        [Fact]
        public void FullPipeline_MathFunctions_WorkCorrectly()
        {
            // Arrange
            var code = @"
                var a = floor(3.7)
                var b = ceil(3.2)
                var c = round(3.5)
                var d = abs(-5)
                var e = min(10, 5)
                var f = max(10, 5)
            ";

            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act
            var result = ExecuteCode(code, context, intrinsics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3.0, context.GetVariable("a"));
            Assert.Equal(4.0, context.GetVariable("b"));
            Assert.Equal(4.0, context.GetVariable("c"));
            Assert.Equal(5.0, context.GetVariable("d"));
            Assert.Equal(5.0, context.GetVariable("e"));
            Assert.Equal(10.0, context.GetVariable("f"));
        }

        [Fact]
        public void FullPipeline_TypeConversion_WorksCorrectly()
        {
            // Arrange
            var code = @"
                var num = toNumber(""42"")
                var str = toString(123)
                var typeNum = typeof(42)
                var typeStr = typeof(""hello"")
            ";

            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act
            var result = ExecuteCode(code, context, intrinsics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(42.0, context.GetVariable("num"));
            Assert.Equal("123", context.GetVariable("str"));
            Assert.Equal("number", context.GetVariable("typeNum"));
            Assert.Equal("string", context.GetVariable("typeStr"));
        }

        [Fact]
        public void FullPipeline_BounceAnimation_ExecutesCorrectly()
        {
            // Arrange
            var code = @"
                if not hasInContext(""x"") then
                    var x = 0
                    var dx = 1
                end if
                
                x = x + dx
                
                if x > 100 or x < 0 then
                    dx = -dx
                end if
            ";

            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act - Execute many times to see bounce
            for (int i = 0; i < 150; i++)
            {
                var result = ExecuteCode(code, context, intrinsics);
                Assert.True(result.Success);
            }

            // Assert - x should be somewhere valid and moving
            var x = (double)context.GetVariable("x");
            var dx = (double)context.GetVariable("dx");
            Assert.InRange(x, -1, 101); // Allow slight overflow
            Assert.True(dx == 1 || dx == -1);
        }

        [Fact]
        public void FullPipeline_ErrorHandling_DivisionByZero_HandledGracefully()
        {
            // Arrange
            var code = "var x = 10 / 0"; // Division by zero
            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act
            var result = ExecuteCode(code, context, intrinsics);

            // Assert
            // VM should either fail gracefully or return infinity
            // Just verify the code doesn't crash
            Assert.NotNull(result);
        }

        [Fact]
        public void FullPipeline_MultipleBlocks_ProcessesAll()
        {
            // Arrange
            var input = @"
                print(""#UI{ var x = 10 }"")
                print(""#UI{ var y = 20 }"")
                print(""#UI{ var z = 30 }"")
            ";

            var lexer = new Lexer(input);
            var context = new VMContext();
            var intrinsics = new IntrinsicRegistry();

            // Act
            var blocks = new System.Collections.Generic.List<CompiledChunk>();
            while (true)
            {
                var tokens = lexer.NextUIBlock();
                if (tokens == null || tokens.Count == 0) break;
                
                var parser = new Parser();
                var ast = parser.Parse(tokens);
                if (ast == null) continue;
                
                var compiler = new Compiler();
                var chunk = compiler.Compile(ast);
                if (chunk != null)
                    blocks.Add(chunk);
            }

            var vm = new VirtualMachine(intrinsics);
            foreach (var chunk in blocks)
            {
                var result = vm.Execute(chunk, context);
                Assert.True(result.Success);
            }

            // Assert
            Assert.Equal(3, blocks.Count);
            Assert.Equal(10.0, Convert.ToDouble(context.GetVariable("x")));
            Assert.Equal(20.0, Convert.ToDouble(context.GetVariable("y")));
            Assert.Equal(30.0, Convert.ToDouble(context.GetVariable("z")));
        }

        // Helper methods
        private static VMResult ExecuteCode(string code, VMContext context, IntrinsicRegistry intrinsics)
        {
            var input = $"#UI{{ {code} }}";
            var lexer = new Lexer(input);
            var tokens = lexer.NextUIBlock();
            var parser = new Parser();
            var ast = parser.Parse(tokens);
            var compiler = new Compiler();
            var chunk = compiler.Compile(ast);
            var vm = new VirtualMachine(intrinsics);
            return vm.Execute(chunk, context);
        }

        private static MockCanvas CreateMockCanvas()
        {
            return new MockCanvas();
        }

        private class MockCanvas : IntrinsicObject
        {
            public System.Collections.Generic.Dictionary<string, int> CallCounts { get; }
                = new System.Collections.Generic.Dictionary<string, int>();

            public MockCanvas() : base("Canvas")
            {
                Methods["show"] = (target, args, ctx) => { IncrementCall("show"); return null; };
                Methods["hide"] = (target, args, ctx) => { IncrementCall("hide"); return null; };
                Methods["clear"] = (target, args, ctx) => { IncrementCall("clear"); return null; };
                Methods["render"] = (target, args, ctx) => { IncrementCall("render"); return null; };
                Methods["drawCircle"] = (target, args, ctx) => { IncrementCall("drawCircle"); return null; };
                Methods["fillCircle"] = (target, args, ctx) => { IncrementCall("fillCircle"); return null; };
                Methods["drawRect"] = (target, args, ctx) => { IncrementCall("drawRect"); return null; };
                Methods["fillRect"] = (target, args, ctx) => { IncrementCall("fillRect"); return null; };
                Methods["drawLine"] = (target, args, ctx) => { IncrementCall("drawLine"); return null; };
                Methods["drawText"] = (target, args, ctx) => { IncrementCall("drawText"); return null; };
                Methods["setPixel"] = (target, args, ctx) => { IncrementCall("setPixel"); return null; };
                Methods["setSize"] = (target, args, ctx) => { IncrementCall("setSize"); return null; };
                Methods["setTitle"] = (target, args, ctx) => { IncrementCall("setTitle"); return null; };
                
                Getters["width"] = (target, ctx) => 320.0;
                Getters["height"] = (target, ctx) => 240.0;
            }

            private void IncrementCall(string methodName)
            {
                if (!CallCounts.ContainsKey(methodName))
                    CallCounts[methodName] = 0;
                CallCounts[methodName]++;
            }
        }
    }
}
