using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Single : Expression, Contractable
	{
		public string Operator;
		public Expression Value;
		public Expr_Single(int line, string file) : base(line, file)
		{
		}

		public Expression TryContract()
		{
			if (!(Value is Expr_Value) && !(Value is Contractable))
				return this;
			Expression v = null;
			if (Value is Contractable c)
				if (!((v = c.TryContract()) is Expr_Value))
					return this;
				else
					v = Value;
			Expr_Value vv = v as Expr_Value;
			Value = vv;
			switch (Operator)
			{
				case "!":
					if (vv.Value == "0")
						return new Expr_Value(Line, File) { Value = "1" };
					else
						return new Expr_Value(Line, File) { Value = "0" };
				default:
					return this;
			}
		}
	}
}
