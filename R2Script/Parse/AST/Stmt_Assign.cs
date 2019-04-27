using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Assign : Statement
	{
		public string Name;
		public Expression Value;

		public Stmt_Assign(int line) : base(line)
		{
		}
	}
}
