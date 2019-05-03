using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Expr_Binary : Expression, Contractable
	{
		public Expression Left, Right;
		public string Operator;

		public Expr_Binary(int line, string file) : base(line, file)
		{
		}

		public Expression TryContract()
		{
			if (Left is Contractable bl)
				Left = bl.TryContract();
			if (Right is Contractable br)
				Right = br.TryContract();
			if (!(Left is Expr_Value && Right is Expr_Value))
				return this;
			switch (Operator)
			{
				case "+":
					return new Expr_Value(Line, File)
					{
						Value = (Convert.ToInt64((Left as Expr_Value).Value) +
						Convert.ToInt64((Right as Expr_Value).Value)).ToString()
					};
				case "-":
					return new Expr_Value(Line, File)
					{
						Value = (Convert.ToInt64((Left as Expr_Value).Value) -
						Convert.ToInt64((Right as Expr_Value).Value)).ToString()
					};
				case "*":
					return new Expr_Value(Line, File)
					{
						Value = (Convert.ToInt64((Left as Expr_Value).Value) *
						Convert.ToInt64((Right as Expr_Value).Value)).ToString()
					};
				default:
					return this;
			}
		}
	}
}
