using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_For : Statement
	{
		public Statement Initialization;
		public Statement Condition;
		public Statement Iteration;
		public Statement Body;

		public Stmt_For(int line) : base(line)
		{
		}
	}
}
