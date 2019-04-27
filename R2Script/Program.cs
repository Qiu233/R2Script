using R2Script.Lex;
using R2Script.Parse;
using R2Script.Parse.AST;
using R2Script.Translation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace R2Script
{
	class Program
	{
		static void Main(string[] args)
		{
			string code = File.ReadAllText("./test.rs");
			/*Tokenizer t = new Tokenizer(code);
			while (true)
			{
				if (t.Next() < 0) break;
				var tk = t.Get();
				Console.WriteLine(tk.Type);
			}*/
			Parser ps = new Parser(code);
			var s = ps.Parse();
			FunctionFactory ff = FunctionFactory.FromFunction(s.SymbolTable, (Stmt_Function)s.Statements[0]);
			//Console.WriteLine(ff.OffsetTables.ElementAt(0).Value.Offsets["j"]);
			Console.WriteLine(ff.GenerateCode().GetCode());
			//Console.WriteLine(ff.MaxStack);
		}
	}
}
