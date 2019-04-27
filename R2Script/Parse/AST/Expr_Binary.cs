using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Binary : Expression
	{
		public Expression Left, Right;
		public string Operator;

		public Expr_Binary(int line) : base(line)
		{
		}
	}
}
