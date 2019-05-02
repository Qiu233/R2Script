using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Continue : Statement
	{
		public Stmt_Continue(int line, string file) : base(line, file)
		{
		}
	}
}
