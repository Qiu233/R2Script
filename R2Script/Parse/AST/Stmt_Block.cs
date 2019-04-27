using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_Block : Statement
	{
		public List<Statement> Statements;
		public SymbolTable SymbolTable;

		public Stmt_Block(int line) : base(line)
		{
		}
	}
}
