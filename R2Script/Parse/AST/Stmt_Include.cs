using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Include : Stmt_PreCompile
	{
		public string TargetFile;
		public Stmt_Include(int line, string file) : base(line, file)
		{
		}
	}
}
