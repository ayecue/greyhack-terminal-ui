using System;
using System.Collections.Generic;

namespace GreyHackTerminalUI.VM
{
    public class VMContext
    {
        private const int MAX_VARIABLES = 100;
        private const int MAX_STRING_LENGTH = 102400; // 100KB

        public Dictionary<string, object> Variables { get; } = new Dictionary<string, object>();
        public Dictionary<string, object> Globals { get; } = new Dictionary<string, object>();

        public void ClearVariables()
        {
            Variables.Clear();
        }

        public object GetVariable(string name)
        {
            if (Variables.TryGetValue(name, out object value))
                return value;
            if (Globals.TryGetValue(name, out value))
                return value;
            return null;
        }

        public void SetVariable(string name, object value)
        {
            // Check variable count limit
            if (!Variables.ContainsKey(name) && Variables.Count >= MAX_VARIABLES)
            {
                throw new VMException($"Variable limit exceeded (max {MAX_VARIABLES})");
            }

            // Check string size limit
            if (value is string str && str.Length > MAX_STRING_LENGTH)
            {
                throw new VMException($"String too large (max {MAX_STRING_LENGTH} characters)");
            }

            Variables[name] = value;
        }

        public void SetGlobal(string name, object value)
        {
            Globals[name] = value;
        }

        public bool HasVariable(string name)
        {
            return Variables.ContainsKey(name) || Globals.ContainsKey(name);
        }
    }

    public class VMResult
    {
        public bool Success { get; set; }
        public object ReturnValue { get; set; }
        public string Error { get; set; }
        public int ErrorLine { get; set; }
    }

    public class VirtualMachine
    {
        private const int MAX_STACK_SIZE = 1024;
        private const int MAX_ITERATIONS = 40000;
        private const int MAX_EXECUTION_TIME_MS = 500;

        private CompiledChunk _chunk;
        private VMContext _context;
        private object[] _stack;
        private int _sp; // Stack pointer
        private int _ip; // Instruction pointer
        private IntrinsicRegistry _intrinsics;
        private bool _shouldStop; // Flag to signal VM should stop execution

        public VirtualMachine(IntrinsicRegistry intrinsics = null)
        {
            _intrinsics = intrinsics ?? new IntrinsicRegistry();
            _stack = new object[MAX_STACK_SIZE];
        }

        public VMResult Execute(CompiledChunk chunk, VMContext context)
        {
            _chunk = chunk;
            _context = context;
            _sp = 0;
            _ip = 0;
            _shouldStop = false;

            int iterations = 0;
            var startTime = DateTime.UtcNow;

            try
            {
                while (_ip < _chunk.Code.Count)
                {
                    if (_shouldStop)
                    {
                        return new VMResult { Success = false, Error = "Execution stopped" };
                    }

                    if (++iterations > MAX_ITERATIONS)
                    {
                        return new VMResult { Success = false, Error = "Execution limit exceeded (infinite loop?)" };
                    }

                    // Check execution time every 1000 iterations
                    if (iterations % 1000 == 0)
                    {
                        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        if (elapsed > MAX_EXECUTION_TIME_MS)
                        {
                            return new VMResult { Success = false, Error = $"Execution time limit exceeded ({MAX_EXECUTION_TIME_MS}ms)" };
                        }
                    }

                    OpCode op = (OpCode)_chunk.Code[_ip++];

                    switch (op)
                    {
                        case OpCode.PUSH_CONST:
                            Push(_chunk.Constants[ReadByte()]);
                            break;

                        case OpCode.PUSH_NULL:
                            Push(null);
                            break;

                        case OpCode.PUSH_TRUE:
                            Push(true);
                            break;

                        case OpCode.PUSH_FALSE:
                            Push(false);
                            break;

                        case OpCode.POP:
                            Pop();
                            break;

                        case OpCode.DUP:
                            Push(Peek());
                            break;

                        case OpCode.LOAD_VAR:
                            {
                                string name = _chunk.Names[ReadByte()];
                                object value = _context.GetVariable(name);
                                
                                // If variable not found, check if it's an intrinsic function name
                                // and return the name as a callable reference
                                if (value == null && !_context.HasVariable(name))
                                {
                                    // Return the name itself - CALL will try it as an intrinsic
                                    value = name;
                                }
                                
                                Push(value);
                            }
                            break;

                        case OpCode.STORE_VAR:
                            {
                                string name = _chunk.Names[ReadByte()];
                                _context.SetVariable(name, Peek()); // Don't pop - assignment is an expression
                            }
                            break;

                        case OpCode.ADD:
                            BinaryOp((a, b) =>
                            {
                                if (a is string || b is string)
                                    return ToString(a) + ToString(b);
                                return ToDouble(a) + ToDouble(b);
                            });
                            break;

                        case OpCode.SUB:
                            BinaryOp((a, b) => ToDouble(a) - ToDouble(b));
                            break;

                        case OpCode.MUL:
                            BinaryOp((a, b) => ToDouble(a) * ToDouble(b));
                            break;

                        case OpCode.DIV:
                            BinaryOp((a, b) =>
                            {
                                double divisor = ToDouble(b);
                                if (divisor == 0) throw new VMException("Division by zero");
                                return ToDouble(a) / divisor;
                            });
                            break;

                        case OpCode.MOD:
                            BinaryOp((a, b) =>
                            {
                                double divisor = ToDouble(b);
                                if (divisor == 0) throw new VMException("Modulo by zero");
                                return ToDouble(a) % divisor;
                            });
                            break;

                        case OpCode.NEG:
                            Push(-ToDouble(Pop()));
                            break;

                        case OpCode.EQ:
                            BinaryOp((a, b) => Equals(a, b));
                            break;

                        case OpCode.NE:
                            BinaryOp((a, b) => !Equals(a, b));
                            break;

                        case OpCode.LT:
                            BinaryOp((a, b) => ToDouble(a) < ToDouble(b));
                            break;

                        case OpCode.GT:
                            BinaryOp((a, b) => ToDouble(a) > ToDouble(b));
                            break;

                        case OpCode.LE:
                            BinaryOp((a, b) => ToDouble(a) <= ToDouble(b));
                            break;

                        case OpCode.GE:
                            BinaryOp((a, b) => ToDouble(a) >= ToDouble(b));
                            break;

                        case OpCode.NOT:
                            Push(!IsTruthy(Pop()));
                            break;

                        case OpCode.JUMP:
                            {
                                short offset = ReadShort();
                                _ip += offset;
                            }
                            break;

                        case OpCode.JUMP_IF_FALSE:
                            {
                                short offset = ReadShort();
                                if (!IsTruthy(Peek()))
                                    _ip += offset;
                            }
                            break;

                        case OpCode.JUMP_IF_TRUE:
                            {
                                short offset = ReadShort();
                                if (IsTruthy(Peek()))
                                    _ip += offset;
                            }
                            break;

                        case OpCode.CALL:
                            {
                                int argCount = ReadByte();
                                object callee = _stack[_sp - argCount - 1];
                                
                                // Get arguments
                                object[] args = new object[argCount];
                                for (int i = argCount - 1; i >= 0; i--)
                                    args[i] = Pop();
                                Pop(); // Pop callee

                                // Call intrinsic function
                                if (callee is string funcName)
                                {
                                    object result = _intrinsics.Call(funcName, args, _context);
                                    Push(result);
                                }
                                else
                                {
                                    throw new VMException($"Cannot call non-function: {callee}");
                                }
                            }
                            break;

                        case OpCode.CALL_METHOD:
                            {
                                string methodName = _chunk.Names[ReadByte()];
                                int argCount = ReadByte();

                                // Get arguments
                                object[] args = new object[argCount];
                                for (int i = argCount - 1; i >= 0; i--)
                                    args[i] = Pop();
                                
                                object obj = Pop();

                                // Call method on object
                                object result = _intrinsics.CallMethod(obj, methodName, args, _context);
                                Push(result);
                            }
                            break;

                        case OpCode.GET_MEMBER:
                            {
                                string memberName = _chunk.Names[ReadByte()];
                                object obj = Pop();
                                object value = _intrinsics.GetMember(obj, memberName, _context);
                                Push(value);
                            }
                            break;

                        case OpCode.SET_MEMBER:
                            {
                                string memberName = _chunk.Names[ReadByte()];
                                object obj = Pop();
                                object value = Peek();
                                _intrinsics.SetMember(obj, memberName, value, _context);
                            }
                            break;

                        case OpCode.RETURN:
                            return new VMResult { Success = true, ReturnValue = null };

                        case OpCode.RETURN_VALUE:
                            return new VMResult { Success = true, ReturnValue = Pop() };

                        case OpCode.HALT:
                            return new VMResult { Success = true, ReturnValue = _sp > 0 ? Pop() : null };

                        default:
                            throw new VMException($"Unknown opcode: {op}");
                    }
                }

                return new VMResult { Success = true };
            }
            catch (VMException ex)
            {
                return new VMResult { Success = false, Error = ex.Message };
            }
            catch (Exception ex)
            {
                return new VMResult { Success = false, Error = $"Runtime error: {ex.Message}" };
            }
        }

        private byte ReadByte() => _chunk.Code[_ip++];

        private short ReadShort()
        {
            byte high = _chunk.Code[_ip++];
            byte low = _chunk.Code[_ip++];
            return (short)((high << 8) | low);
        }

        private void Push(object value)
        {
            if (_sp >= MAX_STACK_SIZE)
                throw new VMException("Stack overflow");
            _stack[_sp++] = value;
        }

        private object Pop()
        {
            if (_sp <= 0)
                throw new VMException("Stack underflow");
            return _stack[--_sp];
        }

        private object Peek(int distance = 0)
        {
            return _stack[_sp - 1 - distance];
        }

        private void BinaryOp(Func<object, object, object> operation)
        {
            object b = Pop();
            object a = Pop();
            Push(operation(a, b));
        }

        private static bool IsTruthy(object value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is double d) return d != 0;
            if (value is int i) return i != 0;
            if (value is string s) return s.Length > 0;
            return true;
        }

        private static double ToDouble(object value)
        {
            if (value == null) return 0;
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is bool b) return b ? 1 : 0;
            if (value is string s && double.TryParse(s, out double parsed)) return parsed;
            return 0;
        }

        private static string ToString(object value)
        {
            if (value == null) return "null";
            return value.ToString();
        }

        public void Stop()
        {
            _shouldStop = true;
        }
    }

    public class VMException : Exception
    {
        public VMException(string message) : base(message) { }
    }
}
