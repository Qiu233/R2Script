using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Function : Statement
	{
		public string Name;
		public List<string> Args;
		public Stmt_Block Body;

		public Stmt_Function(int line) : base(line)
		{
		}
	}
}
