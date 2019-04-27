using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Value : Expression
	{
		public string Value;

		public Expr_Value(int line) : base(line)
		{
		}
	}
}
