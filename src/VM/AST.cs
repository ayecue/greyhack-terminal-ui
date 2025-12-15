using System.Collections.Generic;

namespace GreyHackTerminalUI.VM
{
  public abstract class ASTNode
  {
    public int Line { get; set; }
    public int Column { get; set; }
  }
    
    public class ProgramNode : ASTNode
    {
        public List<StatementNode> Statements { get; } = new List<StatementNode>();
    }

    public abstract class StatementNode : ASTNode { }

    public class VarDeclNode : StatementNode
    {
        public string Name { get; set; }
        public ExpressionNode Initializer { get; set; }
    }

    public class AssignmentNode : StatementNode
    {
        public ExpressionNode Target { get; set; }
        public ExpressionNode Value { get; set; }
    }

    public class ExpressionStatementNode : StatementNode
    {
        public ExpressionNode Expression { get; set; }
    }

    public class IfNode : StatementNode
    {
        public ExpressionNode Condition { get; set; }
        public List<StatementNode> ThenBranch { get; } = new List<StatementNode>();
        public List<ElseIfBranch> ElseIfBranches { get; } = new List<ElseIfBranch>();
        public List<StatementNode> ElseBranch { get; } = new List<StatementNode>();
    }

    public class ElseIfBranch
    {
        public ExpressionNode Condition { get; set; }
        public List<StatementNode> Body { get; } = new List<StatementNode>();
    }

    public class WhileNode : StatementNode
    {
        public ExpressionNode Condition { get; set; }
        public List<StatementNode> Body { get; } = new List<StatementNode>();
    }

    public class ReturnNode : StatementNode
    {
        public ExpressionNode Value { get; set; }
    }

    public abstract class ExpressionNode : ASTNode { }

    public class LiteralNode : ExpressionNode
    {
        public object Value { get; set; }
        public LiteralType LiteralType { get; set; }
    }

    public enum LiteralType
    {
        Number,
        String,
        Boolean,
        Null
    }

    public class IdentifierNode : ExpressionNode
    {
        public string Name { get; set; }
    }

    public class BinaryNode : ExpressionNode
    {
        public ExpressionNode Left { get; set; }
        public BinaryOp Operator { get; set; }
        public ExpressionNode Right { get; set; }
    }

    public enum BinaryOp
    {
        Add, Sub, Mul, Div, Mod,
        Eq, Ne, Lt, Gt, Le, Ge,
        And, Or
    }

    public class UnaryNode : ExpressionNode
    {
        public UnaryOp Operator { get; set; }
        public ExpressionNode Operand { get; set; }
    }

    public enum UnaryOp
    {
        Not,
        Negate
    }

    public class MemberAccessNode : ExpressionNode
    {
        public ExpressionNode Object { get; set; }
        public string Member { get; set; }
    }

    public class CallNode : ExpressionNode
    {
        public ExpressionNode Callee { get; set; }
        public List<ExpressionNode> Arguments { get; } = new List<ExpressionNode>();
    }

    public class GroupNode : ExpressionNode
    {
        public ExpressionNode Expression { get; set; }
    }
}
