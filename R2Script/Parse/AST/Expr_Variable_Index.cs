using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Variable_Index : Expr_Variable
	{
		public Expression Index;
		public Expr_Variable_Index(int line) : base(line)
		{
		}
	}
}
