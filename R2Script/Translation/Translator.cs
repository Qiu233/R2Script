using R2Script.Parse;
using R2Script.Parse.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation
{
	public class Translator
	{
		private Stmt_Block Code
		{
			get;
		}
		private SymbolTable GlobalSymbolTable => Code.SymbolTable;
		public Translator(Stmt_Block block)
		{
			this.Code = block;
		}

	}
}
