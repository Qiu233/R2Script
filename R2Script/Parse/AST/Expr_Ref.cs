using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Ref : Expression
	{
		public enum RefType
		{
			Value, Address
		}
		public Expression Value;
		public RefType Type;
		public Expr_Ref(int line, string file) : base(line, file)
		{
		}
	}
}
