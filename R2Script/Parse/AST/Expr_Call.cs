using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Call : Expression
	{
		public string Name;
		public Expr_ValueList Arguments;

		public Expr_Call(int line, string file) : base(line, file)
		{
		}
	}
}
