using R2Script.Parse;
using R2Script.Parse.AST;
using R2Script.Translation.ASM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace R2Script.Translation
{
	public class Translator
	{
		private List<Stmt_Block> Code
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
		public List<FunctionFactory> Functions
		{
			get;
		}
		public List<KeyValuePair<Stmt_Var.Variable, Stmt_Var>> Variables
		{
			get;
		}
		public List<KeyValuePair<string, string>> ImportedASM
		{
			get;
		}
		public Configuration Configuration
		{
			get;
			set;
		}
		public List<string> SearchingPath
		{
			get;
		}
		public List<string> SearchedFiles
		{
			get;
		}

		public Translator(List<Stmt_Block> block, IEnumerable<string> paths = null)
		{
			this.SearchingPath = new List<string>();
			this.SearchingPath.AddRange(paths);
			this.SearchedFiles = new List<string>();
			SearchFiles();
			this.Configuration = new Configuration();
			this.Code = block;
			this.ConstantManager = ConstantManager.Create(this);
			this.GlobalNameManager = GlobalNameManager.Create(this);
			this.Functions = new List<FunctionFactory>();
			this.Variables = new List<KeyValuePair<Stmt_Var.Variable, Stmt_Var>>();
			this.ImportedASM = new List<KeyValuePair<string, string>>();
			foreach (var b in block.ToArray())
			{
				PreCompile(b);
			}
			ProcessGlobals();
			ProcessVariables();
			ProcessFunctions();
		}

		private void SearchFiles()
		{
			foreach (var d in SearchingPath)
			{
				if (!Directory.Exists(d))
					continue;
				foreach (var file in Directory.EnumerateFiles(d))
					SearchedFiles.Add(file);
			}
			foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory()))
			{
				SearchedFiles.Add(file);
			}
		}

		private bool GetLibFile(string name, out string lib)
		{
			if (name.Contains("/") || name.Contains("\\"))
			{
				if (File.Exists(name))
				{
					lib = null;
					return false;
				}
				lib = File.ReadAllText(name);
				return true;
			}
			var r = SearchedFiles.Where(t => Path.GetFileName(t) == name);
			if (r.Count() > 1)
			{
				string s = $"More than one file matched '{name}'\n";
				s += string.Join("\n", r);
				throw new TranslationException(s, 0, "");
			}
			else if (r.Count() == 0)
			{
				lib = null;
				return false;
			}
			lib = File.ReadAllText(r.ElementAt(0));
			return true;
		}


		public static Translator Create(Stmt_Block[] block, IEnumerable<string> paths = null)
		{
			return new Translator(block.ToList(), paths);
		}

		private void PreCompile(Stmt_Block b)
		{
			foreach (var r in b.Statements)
			{
				if (r is Stmt_Import si)
				{
					if (GetLibFile(si.TargetFile, out string lib))
						ImportedASM.Add(new KeyValuePair<string, string>(si.TargetFile, lib));
					else
						throw new TranslationException($"No such file:'{si.TargetFile}'", si.Line, si.TargetFile);
				}
				else if (r is Stmt_Include sinc)
				{
					if (GetLibFile(sinc.TargetFile, out string lib))
					{
						Parser p = new Parser(lib, sinc.TargetFile);
						var block = p.Parse();
						Code.Add(block);
						PreCompile(block);
					}
					else
						throw new TranslationException($"No such file:'{sinc.TargetFile}'", sinc.Line, sinc.TargetFile);
				}
			}
		}

		private void ProcessGlobals()
		{
			foreach (var t in Code)
			{
				foreach (var r in t.Statements)
				{
					if (!(r is Stmt_Var) && !(r is Stmt_Function) && !(r is Stmt_PreCompile))
						throw new TranslationException("Unexpected statements", r.Line, r.File);
					if (r is Stmt_Var sv)
						foreach (var va in sv.Variables)
							if (!GlobalNameManager.GlobalNames.Contains(va.Name))
								GlobalNameManager.Add(va.Name);
							else throw new TranslationException("Global duplicated", sv.Line, r.File);
					else if (r is Stmt_Function sf)
						if (!GlobalNameManager.GlobalNames.Contains(sf.Name))
							GlobalNameManager.Add(sf.Name);
						else throw new TranslationException("Global duplicated", sf.Line, r.File);
				}
			}
		}
		private void ProcessFunctions()
		{
			foreach (var t in Code)
			{
				foreach (var r in t.Statements)
				{
					if (!(r is Stmt_Function))
						continue;
					var f = FunctionFactory.FromFunction(this, r as Stmt_Function);
					Functions.Add(f);
				}
			}
		}
		private void ProcessVariables()
		{
			foreach (var t in Code)
			{
				foreach (var r in t.Statements)
				{
					if (!(r is Stmt_Var))
						continue;
					var j = (r as Stmt_Var);
					Variables.AddRange(j.Variables.Select(s => new KeyValuePair<Stmt_Var.Variable, Stmt_Var>(s, j)));
				}
			}
		}
		public string CompileConstants()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < ConstantManager.Constants.Count; i++)
			{
				ConstantManager.Constant c = ConstantManager.Constants[i];
				if (c is ConstantManager.ConstantString cs)
				{
					sb.Append($"{ConstantManager.GetConstant(i)}: dw {cs.Content},0\n");
				}
			}
			return sb.ToString();
		}
		public string CompileFunctions()
		{
			StringBuilder sb = new StringBuilder();
			foreach (var f in Functions)
			{
				sb.Append($"; function {(f.Naked ? "naked " : "")}{f.Name}({string.Join(",", f.Function.Args)})\n");
				sb.Append($"{GlobalNameManager.GetGlobalName(f.Name)}:\n");
				sb.Append(f.ASMCode.GetCode());
				sb.Append("\n\n");
			}
			return sb.ToString();
		}
		public string CompileData()
		{
			StringBuilder sb = new StringBuilder();
			foreach (var v in Variables)
			{
				if (v.Key.InitialValue is Expr_Value ev)
				{
					sb.Append($"{GlobalNameManager.GetGlobalName(v.Key.Name)}: ");
					sb.Append($"dw {ev.Value}");
					if (ev.Value.StartsWith("\""))
						sb.Append($",0");
					sb.Append("\n");
				}
				else if (v.Key.InitialValue is Expr_Binary eb)
				{
					sb.Append($"{GlobalNameManager.GetGlobalName(v.Key.Name)}: ");
					var b = eb.TryContract();
					if (b is Expr_Value ev1)
						sb.Append($"dw {ev1.Value}\n");
					else
						throw new TranslationException("Variable should be initialized as constants", v.Value.Line, v.Value.File);
				}
				else if (v.Key.InitialValue is Expr_ValueList evl)
				{
					sb.Append($"{GlobalNameManager.GetGlobalName(v.Key.Name)}:\n");
					List<string> tt = new List<string>();
					foreach (var ex in evl.ValueList)
					{
						if (ex is Expr_Value ev2)
							tt.Add(ev2.Value);
						else if (ex is Expr_Binary && (ex as Expr_Binary).TryContract() is Expr_Value ev3)
							tt.Add(ev3.Value);
						else
							throw new TranslationException("Variable should be initialized as constants", v.Value.Line, v.Value.File);
					}
					var ttt = tt.ToArray();
					int i = 0;
					for (i = 0; i < tt.Count;)
					{
						if (i + 16 <= tt.Count)
						{
							sb.Append($"dw {string.Join(",", ttt, i, 16)}\n");
							i += 16;
						}
						else
						{
							sb.Append($"dw {string.Join(",", ttt, i, tt.Count - i)}\n");
							i += tt.Count - i;
						}
					}
				}
				else throw new TranslationException("Variable should be initialized as constants", v.Value.Line, v.Value.File);
			}
			return sb.ToString();
		}
		public string CompileASMs()
		{
			StringBuilder sb = new StringBuilder();
			foreach (var f in ImportedASM)
			{
				sb.Append($";from file:'{f.Key}'\n");
				sb.Append(f.Value);
				sb.Append('\n');
			}
			return sb.ToString();
		}
		public string Compile()
		{
			if (!GlobalNameManager.GlobalNames.Contains("main"))
				throw new TranslationException("Function 'main' is not defined", 0, "");
			string asms = CompileASMs();
			string function = CompileFunctions();
			string data = CompileData();
			string cons = CompileConstants();
			string code = $"mov sp,0\n" +
				$"call {GlobalNameManager.GetGlobalName("main")}\n" +
				$"__mainLoop:jmp __mainLoop" +
				$"\n{cons}\n{data}\n{function}\n{asms}";
			return code;
		}
	}
}
