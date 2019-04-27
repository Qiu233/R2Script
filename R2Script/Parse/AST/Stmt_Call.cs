using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Call : Statement
	{
		public string Name;
		public Expr_ValueList Arguments;

		public Stmt_Call(int line) : base(line)
		{
		}
	}
}
