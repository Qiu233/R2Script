using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public abstract class Expression
	{
		public int Line
		{
			get;
			set;
		}
		public Expression(int line)
		{
			this.Line = line;
		}
	}
}
