using System;
using System.Collections.Generic;

namespace GreyHackTerminalUI.VM
{
    public delegate object IntrinsicFunction(object[] args, VMContext context);
    public delegate object IntrinsicMethod(object target, object[] args, VMContext context);

    public class IntrinsicObject
    {
        public string Name { get; }
        public Dictionary<string, IntrinsicMethod> Methods { get; } = new Dictionary<string, IntrinsicMethod>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Func<object, VMContext, object>> Getters { get; } = new Dictionary<string, Func<object, VMContext, object>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Action<object, object, VMContext>> Setters { get; } = new Dictionary<string, Action<object, object, VMContext>>(StringComparer.OrdinalIgnoreCase);

        public IntrinsicObject(string name)
        {
            Name = name;
        }
    }

    public partial class IntrinsicRegistry
    {
        private readonly Dictionary<string, IntrinsicFunction> _functions = new Dictionary<string, IntrinsicFunction>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IntrinsicObject> _objects = new Dictionary<string, IntrinsicObject>(StringComparer.OrdinalIgnoreCase);

        public IntrinsicRegistry()
        {
            RegisterBuiltins();
        }
        public void RegisterFunction(string name, IntrinsicFunction func)
        {
            _functions[name] = func;
        }

        public void RegisterObject(IntrinsicObject obj)
        {
            _objects[obj.Name] = obj;
        }

        public bool TryGetFunction(string name, out IntrinsicFunction func)
        {
            return _functions.TryGetValue(name, out func);
        }

        public bool TryGetObject(string name, out IntrinsicObject obj)
        {
            return _objects.TryGetValue(name, out obj);
        }

        public object Call(string funcName, object[] args, VMContext context)
        {
            if (_functions.TryGetValue(funcName, out IntrinsicFunction func))
            {
                return func(args, context);
            }
            throw new Exception($"Unknown function: {funcName}");
        }

        public object CallMethod(object target, string methodName, object[] args, VMContext context)
        {
            if (target is string targetName && _objects.TryGetValue(targetName, out IntrinsicObject obj))
            {
                if (obj.Methods.TryGetValue(methodName, out IntrinsicMethod method))
                {
                    return method(target, args, context);
                }
                throw new Exception($"Unknown method: {targetName}.{methodName}");
            }
            throw new Exception($"Cannot call method on: {target}");
        }

        public object GetMember(object target, string memberName, VMContext context)
        {
            if (target is string targetName && _objects.TryGetValue(targetName, out IntrinsicObject obj))
            {
                if (obj.Getters.TryGetValue(memberName, out var getter))
                {
                    return getter(target, context);
                }
                throw new Exception($"Unknown property: {targetName}.{memberName}");
            }
            throw new Exception($"Cannot get member on: {target}");
        }

        public void SetMember(object target, string memberName, object value, VMContext context)
        {
            if (target is string targetName && _objects.TryGetValue(targetName, out IntrinsicObject obj))
            {
                if (obj.Setters.TryGetValue(memberName, out var setter))
                {
                    setter(target, value, context);
                    return;
                }
                throw new Exception($"Cannot set property: {targetName}.{memberName}");
            }
            throw new Exception($"Cannot set member on: {target}");
        }

        // Provide test-safe versions of built-in functions
        private void RegisterBuiltins()
        {
            // Context checking
            RegisterFunction("hasInContext", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("hasInContext requires 1 argument");
                    
                string varName = args[0]?.ToString();
                if (string.IsNullOrEmpty(varName))
                    return false;
                    
                return ctx.HasVariable(varName);
            });

            // Print function (just returns the value in tests)
            RegisterFunction("print", (args, ctx) =>
            {
                if (args.Length > 0)
                    return args[0];
                return null;
            });

            // Type checking
            RegisterFunction("typeof", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("typeof requires 1 argument");
                    
                object value = args[0];
                if (value == null) return "null";
                if (value is bool) return "boolean";
                if (value is double || value is float || value is int) return "number";
                if (value is string) return "string";
                return "object";
            });

            // Conversion functions
            RegisterFunction("toNumber", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("toNumber requires 1 argument");
                    
                object value = args[0];
                if (value is double d) return d;
                if (value is int i) return (double)i;
                if (value is float f) return (double)f;
                if (value is string s && double.TryParse(s, out double result))
                    return result;
                    
                return 0.0;
            });

            RegisterFunction("toString", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("toString requires 1 argument");
                    
                object value = args[0];
                return value?.ToString() ?? "null";
            });

            // Math functions
            RegisterFunction("floor", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("floor requires 1 argument");
                    
                double value = Convert.ToDouble(args[0]);
                return Math.Floor(value);
            });

            RegisterFunction("ceil", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("ceil requires 1 argument");
                    
                double value = Convert.ToDouble(args[0]);
                return Math.Ceiling(value);
            });

            RegisterFunction("round", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("round requires 1 argument");
                    
                double value = Convert.ToDouble(args[0]);
                return Math.Round(value);
            });

            RegisterFunction("abs", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("abs requires 1 argument");
                    
                double value = Convert.ToDouble(args[0]);
                return Math.Abs(value);
            });

            RegisterFunction("min", (args, ctx) =>
            {
                if (args.Length != 2)
                    throw new ArgumentException("min requires 2 arguments");
                    
                double a = Convert.ToDouble(args[0]);
                double b = Convert.ToDouble(args[1]);
                return Math.Min(a, b);
            });

            RegisterFunction("max", (args, ctx) =>
            {
                if (args.Length != 2)
                    throw new ArgumentException("max requires 2 arguments");
                    
                double a = Convert.ToDouble(args[0]);
                double b = Convert.ToDouble(args[1]);
                return Math.Max(a, b);
            });

            RegisterFunction("sin", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("sin requires 1 argument");
                    
                double value = Convert.ToDouble(args[0]);
                return Math.Sin(value);
            });

            RegisterFunction("cos", (args, ctx) =>
            {
                if (args.Length != 1)
                    throw new ArgumentException("cos requires 1 argument");
                    
                double value = Convert.ToDouble(args[0]);
                return Math.Cos(value);
            });

            RegisterFunction("random", (args, ctx) =>
            {
                var random = new Random();
                return random.NextDouble();
            });

            RegisterFunction("randomRange", (args, ctx) =>
            {
                if (args.Length != 2)
                    throw new ArgumentException("randomRange requires 2 arguments");
                    
                double min = Convert.ToDouble(args[0]);
                double max = Convert.ToDouble(args[1]);
                var random = new Random();
                return min + random.NextDouble() * (max - min);
            });
        }
    }
}
