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
			Translator t = Translator.Create(s);
			t.Configuration = new Configuration() { IncBPAfterCall = true };
			string c = t.Compile();
			Console.WriteLine(c);
			File.WriteAllText("./test.asm", c);
		}
	}
}
