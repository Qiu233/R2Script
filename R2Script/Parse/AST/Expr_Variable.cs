using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Variable : Expression
	{
		public string Name;

		public Expr_Variable(int line) : base(line)
		{
		}
	}
}
