using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public abstract class Stmt_PreCompile : Statement
	{
		public Stmt_PreCompile(int line) : base(line)
		{
		}
	}
}
