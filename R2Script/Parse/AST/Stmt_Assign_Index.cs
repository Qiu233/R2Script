using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Assign_Index : Stmt_Assign
	{
		public Expression Index;
		public Stmt_Assign_Index(int line) : base(line)
		{
		}
	}
}
