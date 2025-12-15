using System;
using System.Collections.Generic;

namespace GreyHackTerminalUI.VM
{
    public class CompiledChunk
    {
        public List<byte> Code { get; } = new List<byte>();
        public List<object> Constants { get; } = new List<object>();
        public List<string> Names { get; } = new List<string>();

        public int AddConstant(object value)
        {
            int index = Constants.IndexOf(value);
            if (index == -1)
            {
                index = Constants.Count;
                Constants.Add(value);
            }
            return index;
        }

        public int AddName(string name)
        {
            int index = Names.IndexOf(name);
            if (index == -1)
            {
                index = Names.Count;
                Names.Add(name);
            }
            return index;
        }

        public void EmitByte(byte b) => Code.Add(b);
        public void EmitOp(OpCode op) => Code.Add((byte)op);
        
        public void EmitOpWithArg(OpCode op, int arg)
        {
            Code.Add((byte)op);
            Code.Add((byte)arg);
        }

        public void EmitJump(OpCode op)
        {
            Code.Add((byte)op);
            Code.Add(0xFF); // placeholder high byte
            Code.Add(0xFF); // placeholder low byte
        }

        public int CurrentOffset => Code.Count;

        public void PatchJump(int offset)
        {
            int jump = Code.Count - offset - 2;
            Code[offset] = (byte)((jump >> 8) & 0xFF);
            Code[offset + 1] = (byte)(jump & 0xFF);
        }

        public void EmitLoop(int loopStart)
        {
            Code.Add((byte)OpCode.JUMP);
            int offset = Code.Count - loopStart + 2;
            Code.Add((byte)(((-offset) >> 8) & 0xFF));
            Code.Add((byte)((-offset) & 0xFF));
        }
    }

    public class Compiler
    {
        private CompiledChunk _chunk;

        public CompiledChunk Compile(ProgramNode program)
        {
            _chunk = new CompiledChunk();

            foreach (var stmt in program.Statements)
            {
                CompileStatement(stmt);
            }

            _chunk.EmitOp(OpCode.HALT);
            return _chunk;
        }

        private void CompileStatement(StatementNode stmt)
        {
            switch (stmt)
            {
                case VarDeclNode varDecl:
                    CompileVarDecl(varDecl);
                    break;
                case AssignmentNode assign:
                    CompileAssignment(assign);
                    break;
                case ExpressionStatementNode exprStmt:
                    CompileExpression(exprStmt.Expression);
                    _chunk.EmitOp(OpCode.POP); // Discard result
                    break;
                case IfNode ifNode:
                    CompileIf(ifNode);
                    break;
                case WhileNode whileNode:
                    CompileWhile(whileNode);
                    break;
                case ReturnNode returnNode:
                    CompileReturn(returnNode);
                    break;
                default:
                    throw new CompileException($"Unknown statement type: {stmt.GetType().Name}");
            }
        }

        private void CompileVarDecl(VarDeclNode varDecl)
        {
            if (varDecl.Initializer != null)
            {
                CompileExpression(varDecl.Initializer);
            }
            else
            {
                _chunk.EmitOp(OpCode.PUSH_NULL);
            }

            int nameIndex = _chunk.AddName(varDecl.Name);
            _chunk.EmitOpWithArg(OpCode.STORE_VAR, nameIndex);
        }

        private void CompileAssignment(AssignmentNode assign)
        {
            CompileExpression(assign.Value);

            switch (assign.Target)
            {
                case IdentifierNode ident:
                    int nameIndex = _chunk.AddName(ident.Name);
                    _chunk.EmitOpWithArg(OpCode.STORE_VAR, nameIndex);
                    break;
                case MemberAccessNode member:
                    CompileExpression(member.Object);
                    int memberIndex = _chunk.AddName(member.Member);
                    _chunk.EmitOpWithArg(OpCode.SET_MEMBER, memberIndex);
                    break;
                default:
                    throw new CompileException("Invalid assignment target");
            }
        }

        private void CompileIf(IfNode ifNode)
        {
            // Compile condition
            CompileExpression(ifNode.Condition);
            
            // Jump to else/elseif/end if condition is false
            int thenJump = _chunk.CurrentOffset;
            _chunk.EmitJump(OpCode.JUMP_IF_FALSE);
            _chunk.EmitOp(OpCode.POP); // Pop condition

            // Compile then branch
            foreach (var stmt in ifNode.ThenBranch)
            {
                CompileStatement(stmt);
            }

            // Jump over else branches
            int elseJump = _chunk.CurrentOffset;
            _chunk.EmitJump(OpCode.JUMP);

            // Patch the then jump
            _chunk.PatchJump(thenJump + 1);
            _chunk.EmitOp(OpCode.POP); // Pop condition

            // Handle else if branches
            var elseIfJumps = new List<int>();
            foreach (var elseIfBranch in ifNode.ElseIfBranches)
            {
                CompileExpression(elseIfBranch.Condition);
                int elseIfJump = _chunk.CurrentOffset;
                _chunk.EmitJump(OpCode.JUMP_IF_FALSE);
                _chunk.EmitOp(OpCode.POP);

                foreach (var stmt in elseIfBranch.Body)
                {
                    CompileStatement(stmt);
                }

                elseIfJumps.Add(_chunk.CurrentOffset);
                _chunk.EmitJump(OpCode.JUMP);

                _chunk.PatchJump(elseIfJump + 1);
                _chunk.EmitOp(OpCode.POP);
            }

            // Compile else branch
            foreach (var stmt in ifNode.ElseBranch)
            {
                CompileStatement(stmt);
            }

            // Patch all jumps to end
            _chunk.PatchJump(elseJump + 1);
            foreach (var jump in elseIfJumps)
            {
                _chunk.PatchJump(jump + 1);
            }
        }

        private void CompileWhile(WhileNode whileNode)
        {
            int loopStart = _chunk.CurrentOffset;

            // Compile condition
            CompileExpression(whileNode.Condition);

            // Jump out if condition is false
            int exitJump = _chunk.CurrentOffset;
            _chunk.EmitJump(OpCode.JUMP_IF_FALSE);
            _chunk.EmitOp(OpCode.POP); // Pop condition

            // Compile body
            foreach (var stmt in whileNode.Body)
            {
                CompileStatement(stmt);
            }

            // Jump back to start
            _chunk.EmitLoop(loopStart);

            // Patch exit jump
            _chunk.PatchJump(exitJump + 1);
            _chunk.EmitOp(OpCode.POP); // Pop condition
        }

        private void CompileReturn(ReturnNode returnNode)
        {
            if (returnNode.Value != null)
            {
                CompileExpression(returnNode.Value);
                _chunk.EmitOp(OpCode.RETURN_VALUE);
            }
            else
            {
                _chunk.EmitOp(OpCode.RETURN);
            }
        }

        private void CompileExpression(ExpressionNode expr)
        {
            switch (expr)
            {
                case LiteralNode literal:
                    CompileLiteral(literal);
                    break;
                case IdentifierNode ident:
                    int nameIndex = _chunk.AddName(ident.Name);
                    _chunk.EmitOpWithArg(OpCode.LOAD_VAR, nameIndex);
                    break;
                case BinaryNode binary:
                    CompileBinary(binary);
                    break;
                case UnaryNode unary:
                    CompileUnary(unary);
                    break;
                case MemberAccessNode member:
                    CompileExpression(member.Object);
                    int memberIndex = _chunk.AddName(member.Member);
                    _chunk.EmitOpWithArg(OpCode.GET_MEMBER, memberIndex);
                    break;
                case CallNode call:
                    CompileCall(call);
                    break;
                case GroupNode group:
                    CompileExpression(group.Expression);
                    break;
                default:
                    throw new CompileException($"Unknown expression type: {expr.GetType().Name}");
            }
        }

        private void CompileLiteral(LiteralNode literal)
        {
            switch (literal.LiteralType)
            {
                case LiteralType.Null:
                    _chunk.EmitOp(OpCode.PUSH_NULL);
                    break;
                case LiteralType.Boolean:
                    _chunk.EmitOp((bool)literal.Value ? OpCode.PUSH_TRUE : OpCode.PUSH_FALSE);
                    break;
                case LiteralType.Number:
                case LiteralType.String:
                    int constIndex = _chunk.AddConstant(literal.Value);
                    _chunk.EmitOpWithArg(OpCode.PUSH_CONST, constIndex);
                    break;
            }
        }

        private void CompileBinary(BinaryNode binary)
        {
            // Short-circuit evaluation for AND/OR
            if (binary.Operator == BinaryOp.And)
            {
                CompileExpression(binary.Left);
                int jumpOffset = _chunk.CurrentOffset;
                _chunk.EmitJump(OpCode.JUMP_IF_FALSE);
                _chunk.EmitOp(OpCode.POP);
                CompileExpression(binary.Right);
                _chunk.PatchJump(jumpOffset + 1);
                return;
            }

            if (binary.Operator == BinaryOp.Or)
            {
                CompileExpression(binary.Left);
                int jumpOffset = _chunk.CurrentOffset;
                _chunk.EmitJump(OpCode.JUMP_IF_TRUE);
                _chunk.EmitOp(OpCode.POP);
                CompileExpression(binary.Right);
                _chunk.PatchJump(jumpOffset + 1);
                return;
            }

            // Normal binary operations
            CompileExpression(binary.Left);
            CompileExpression(binary.Right);

            OpCode op = binary.Operator switch
            {
                BinaryOp.Add => OpCode.ADD,
                BinaryOp.Sub => OpCode.SUB,
                BinaryOp.Mul => OpCode.MUL,
                BinaryOp.Div => OpCode.DIV,
                BinaryOp.Mod => OpCode.MOD,
                BinaryOp.Eq => OpCode.EQ,
                BinaryOp.Ne => OpCode.NE,
                BinaryOp.Lt => OpCode.LT,
                BinaryOp.Gt => OpCode.GT,
                BinaryOp.Le => OpCode.LE,
                BinaryOp.Ge => OpCode.GE,
                _ => throw new CompileException($"Unknown binary operator: {binary.Operator}")
            };

            _chunk.EmitOp(op);
        }

        private void CompileUnary(UnaryNode unary)
        {
            CompileExpression(unary.Operand);

            OpCode op = unary.Operator switch
            {
                UnaryOp.Not => OpCode.NOT,
                UnaryOp.Negate => OpCode.NEG,
                _ => throw new CompileException($"Unknown unary operator: {unary.Operator}")
            };

            _chunk.EmitOp(op);
        }

        private void CompileCall(CallNode call)
        {
            // Check if it's a method call (callee is MemberAccess)
            if (call.Callee is MemberAccessNode memberAccess)
            {
                // Compile the object
                CompileExpression(memberAccess.Object);

                // Compile arguments
                foreach (var arg in call.Arguments)
                {
                    CompileExpression(arg);
                }

                // Emit method call
                int nameIndex = _chunk.AddName(memberAccess.Member);
                _chunk.EmitOpWithArg(OpCode.CALL_METHOD, nameIndex);
                _chunk.EmitByte((byte)call.Arguments.Count);
            }
            else
            {
                // Regular function call
                CompileExpression(call.Callee);

                foreach (var arg in call.Arguments)
                {
                    CompileExpression(arg);
                }

                _chunk.EmitOpWithArg(OpCode.CALL, call.Arguments.Count);
            }
        }
    }

    public class CompileException : Exception
    {
        public CompileException(string message) : base(message) { }
    }
}
