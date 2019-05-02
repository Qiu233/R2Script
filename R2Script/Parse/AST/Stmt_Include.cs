using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Include : Stmt_PreCompile
	{
		public string File;
		public Stmt_Include(int line) : base(line)
		{
		}
	}
}
