using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Return : Statement
	{
		public Expression Value;

		public Stmt_Return(int line, string file) : base(line, file)
		{
		}
	}
}
