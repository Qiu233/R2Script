using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Org : Stmt_PreCompile
	{
		public string Address;
		public Stmt_Org(int line, string file) : base(line, file)
		{
		}
	}
}
