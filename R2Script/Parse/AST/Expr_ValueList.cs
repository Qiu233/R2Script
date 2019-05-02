using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_ValueList : Expression
	{
		public List<Expression> ValueList = new List<Expression>();

		public Expr_ValueList(int line, string file) : base(line, file)
		{
		}
	}
}
