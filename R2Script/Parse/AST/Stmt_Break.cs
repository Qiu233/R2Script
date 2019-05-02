using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Break : Statement
	{
		public Stmt_Break(int line, string file) : base(line, file)
		{
		}
	}
}
