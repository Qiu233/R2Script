using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Var : Statement
	{
		public class Variable
		{
			public string Name;
			public Expression InitialValue;
			public Variable(string name, Expression val)
			{
				this.Name = name;
				this.InitialValue = val;
			}

			public Variable()
			{
			}
		}
		/// <summary>
		/// InitialValue -> Length
		/// </summary>
		public class VariableArray : Variable
		{
			public VariableArray(string name, Expression len) : base(name, len)
			{
			}
			public VariableArray() : base()
			{
			}
		}
		public List<Variable> Variables;

		public Stmt_Var(int line) : base(line)
		{
		}
	}
}
