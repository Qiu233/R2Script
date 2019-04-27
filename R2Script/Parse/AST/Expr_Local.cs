using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Local : Expression
	{
		public string Name;

		public Expr_Local(int line) : base(line)
		{
		}
	}
}
