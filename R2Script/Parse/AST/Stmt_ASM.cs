using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_ASM : Statement
	{
		public string ASM;
		public Stmt_ASM(int line) : base(line)
		{
		}
	}
}
