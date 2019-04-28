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
		private IEnumerable<Stmt_Block> Code
		{
			get;
		}

		public ConstantManager ConstantManager
		{
			get;
		}
		public GlobalNameManager GlobalNameManager
		{
			get;
		}

		public Translator(IEnumerable<Stmt_Block> block)
		{
			this.Code = block;
			this.ConstantManager = ConstantManager.Create(this);
			this.GlobalNameManager = GlobalNameManager.Create(this);
		}
		

		public static Translator Create(params Stmt_Block[] block)
		{
			return new Translator(block);
		}
		public static Translator Create(IEnumerable<Stmt_Block> block)
		{
			return new Translator(block);
		}
	}
}
