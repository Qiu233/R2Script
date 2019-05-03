using CommandLine;
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

#if DEBUG
			string code = File.ReadAllText("./test.rs");
			Parse.Parser ps = new Parse.Parser(code, "./test.rs");
			var s = ps.Parse();
			Translator t = Translator.Create(s);
			string c = t.Compile();
			Console.WriteLine(c);
			File.WriteAllText("./test.asm", c);
#else
			if (CommandLine.Parser.Default.ParseArguments<Options>(args) is Parsed<Options> p)
			{
				var options = p.Value;
				var paths = options.LibPaths.ToList();
				paths.Add(AppDomain.CurrentDomain.BaseDirectory);
				paths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib"));
				Stmt_Block[] files = options.InputFiles.
					Select(f => new Parse.Parser(
						File.ReadAllText(f), f).Parse()).ToArray();
				Translator t = Translator.Create(files, paths);
				string code = t.Compile();
				File.WriteAllText(options.OutputFile, code);
				Console.WriteLine("Compilation is done without errors");
			}
#endif
		}
	}
}
