using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public abstract class Statement
	{
		public int Line
		{
			get;
			set;
		}
		public string File
		{
			get;
			set;
		}
		public Statement(int line,string file)
		{
			this.Line = line;
			this.File = file;
		}
	}
}
