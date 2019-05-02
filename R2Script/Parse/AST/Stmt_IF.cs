using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public class Stmt_IF : Statement
	{
		public class IFStructure
		{
			public Expression Condition;
			public Statement Body;
			public IFStructure(Expression condition, Statement body)
			{
				Condition = condition;
				Body = body;
			}
		}
		public List<IFStructure> IF;
		public Statement Else;

		public Stmt_IF(int line, string file) : base(line, file)
		{
		}
	}
}
