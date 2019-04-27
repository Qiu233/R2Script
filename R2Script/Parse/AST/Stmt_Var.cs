using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Var : Statement
	{
		public abstract class Variable
		{
			public string Name;
		}
		public class VariableArray : Variable
		{
			public Expression Length;
			public Expr_ValueList InitialValue;
			public VariableArray(string name, Expression len, Expr_ValueList val)
			{
				this.Name = name;
				this.Length = len;
				this.InitialValue = val;
			}

			public VariableArray()
			{
			}
		}
		public class VariableValue : Variable
		{
			public Expression InitialValue;
			public VariableValue(string name, Expression val)
			{
				this.Name = name;
				this.InitialValue = val;
			}

			public VariableValue()
			{
			}
		}
		public List<Variable> Variables;

		public Stmt_Var(int line) : base(line)
		{
		}
	}
}
