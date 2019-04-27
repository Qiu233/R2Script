using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_While : Statement
	{
		public Expression Condition;
		public Statement Body;

		public Stmt_While(int line) : base(line)
		{
		}
	}
}
