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
	public class FunctionFactory
	{
		public Translator Translator
		{
			get;
		}
		public Stmt_Function Function
		{
			get;
		}
		public string Name => Function.Name;
		public bool Naked => Function.Naked;

		private Stack<OffsetTable> OffsetTables
		{
			get;
		}
		private OffsetTable RootOffsetTable
		{
			get;
		}
		private int StackUse
		{
			get;
			set;
		}
		private void PushNewOffsetTable()
		{
			var t = new OffsetTable(StackUse);
			OffsetTables.Push(t);
		}

		private void PopNewOffsetTable()
		{
			StackUse = OffsetTables.Pop().OffsetBase;
		}

		private void AddOffset(string name, int offset)
		{
			OffsetTables.Peek().Add(name, offset);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="offset"></param>
		/// <returns>true if exists,else false</returns>
		private bool SearchOffset(string name, out int offset)
		{
			foreach (var t in OffsetTables)
			{
				var r = t.Offsets.Where(i => i.Key == name);
				if (r.Count() > 0)
				{
					offset = r.ElementAt(0).Value;
					return true;
				}
			}
			offset = 0;
			return false;
		}

		private FunctionFactory(Translator translator, Stmt_Function func)
		{
			this.Translator = translator;
			this.Function = func;
			this.OffsetTables = new Stack<OffsetTable>();
			this.RootOffsetTable = new OffsetTable(0);
			OffsetTables.Push(RootOffsetTable);
		}
		public static FunctionFactory FromFunction(Translator translator, Stmt_Function func)
		{
			var n = new FunctionFactory(translator, func);
			CheckLocals(func.Body.SymbolTable, new List<string>());
			n.MapArgs();
			return n;
		}

		private static void CheckLocals(SymbolTable tab, List<string> names)
		{
			tab.Symbols.ForEach(t =>
			{
				if (t is SymbolIden)
				{
					var si = t as SymbolIden;
					if (names.Contains(si.Name))
						throw new TranslationException("Local duplicated:'" + si.Name + "'", si.Line);
					names.Add(si.Name);
				}
				else
				{
					var st = t as SymbolTable;
					CheckLocals(st, names.Select(h => h).ToList());
				}
			});
		}

		private void MapArgs()
		{
			int bs = -1;
			foreach (var t in Function.Body.SymbolTable.Symbols)
			{
				if (t is SymbolIden &&
					Function.Args.Contains((t as SymbolIden).Name))
					RootOffsetTable.Add((t as SymbolIden).Name, bs--);
			}
		}

		private string GetLocalOffset(int off)
		{
			if (off < 0)//args
				return "+" + (0 - off + 1);//-1 to +2/   -2 to +3 and so on
			else//locals
				return "-" + (off + 1);
		}

		private ASMCode GetFunctionHead()
		{
			if (Naked)
				return ASMSnippet.FromEmpty();
			return ASMSnippet.FromCode(
				new ASMCode[] {
					(ASMInstruction)"push bp",
					(ASMInstruction)"mov bp,sp"
			});
		}
		private ASMCode GetFunctionEnd()
		{
			var asm = ASMSnippet.FromEmpty();
			if (!Naked)
				asm.Content.Add((ASMInstruction)"pop bp");
			asm.Content.Add((ASMInstruction)"ret");
			return asm;
		}

		public ASMCode GenerateCode()
		{
			var asm = ASMSnippet.FromEmpty();
			asm.Content.Add(GetFunctionHead());
			asm.Content.Add(GenerateBody());
			if (!asm.GetCode().Trim().EndsWith("ret"))
				asm.Content.Add(GetFunctionEnd());
			return asm;
		}
		private ASMCode GenerateBody()
		{
			if (!Naked)
			{
				ASMSnippet snippet = ASMSnippet.FromEmpty();
				foreach (var stmt in Function.Body.Statements)
				{
					var s = GenerateStatement(stmt);
					if (s == null) continue;
					snippet.Content.Add(s);
				}
				return snippet;
			}
			else
			{
				var e = ASMSnippet.FromEmpty();
				foreach (var stmt in Function.Body.Statements)
				{
					if (!(stmt is Stmt_ASM))
						throw new TranslationException("Only native ASMs is allowed to be in a naked function", stmt.Line);
					var sa = stmt as Stmt_ASM;
					e.Content.Add((ASMInstruction)(sa.ASM + "\n"));
				}
				return e;
			}
		}
		private ASMCode GenerateStatement(Statement stmt)
		{
			if (stmt is Stmt_Var v)
			{
				foreach (var variable in v.Variables)
				{
					int off = StackUse;
					AddOffset(variable.Name, off);
					if (variable.InitialValue == null)
						continue;
					if (!(variable is Stmt_Var.Variable))
						continue;
					Stmt_Var.Variable vv = variable as Stmt_Var.Variable;
					if (vv is Stmt_Var.VariableArray)
					{
						int len = 0;
						if (vv.InitialValue is Expr_Binary eb)
						{
							Expression ex = null;
							if (!((ex = eb.TryContract()) is Expr_Value))
								throw new TranslationException("Array's length should be a constant", eb.Line);
							len = Convert.ToInt32((ex as Expr_Value).Value);
						}
						else if (vv.InitialValue is Expr_Value ev)
						{
							len = Convert.ToInt32(ev.Value);
						}
						else
							throw new TranslationException("Array's length should be a constant", v.Line);
						StackUse += len + 1;

						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov r0,bp",
								(ASMInstruction)$"add r0,{GetLocalOffset(off+1)}",
								(ASMInstruction)$"mov [bp{GetLocalOffset(off)}],r0",
						});
					}
					else
					{
						StackUse++;
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToMem(vv.InitialValue, $"[bp{GetLocalOffset(off)}]"),
							});
					}
				}
				return null;
			}
			else if (stmt is Stmt_Assign sa)
			{
				bool b = SearchOffset(sa.Name, out int off);
				if (!b &&
					!Translator.GlobalNameManager.GlobalNames.Contains(sa.Name))
					throw new TranslationException($"Unknown global or local:'{sa.Name}'", sa.Line);
				if (b)
					return ASMSnippet.FromCode(
						new ASMCode[] {
							GenerateExpressionToMem(sa.Value, $"[bp{GetLocalOffset(off)}]"),
						});
				else
					return ASMSnippet.FromCode(
						new ASMCode[] {
							GenerateExpressionToMem(sa.Value, $"[{GlobalNameManager.GetGlobalName(sa.Name)}]"),
						});
			}
			else if (stmt is Stmt_Return sr)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateExpressionToReg(sr.Value, "r0"),
						(ASMInstruction)"pop bp",
						(ASMInstruction)"ret",
					});
			}
			else if (stmt is Stmt_Call sc)
			{
				return GenerateCall(sc.Name, sc.Arguments, sc.Line);
			}
			else if (stmt is Stmt_ASM sasm)
			{
				return ASMSnippet.FromASMCode(sasm.ASM);
			}
			throw new TranslationException("Unexpected statement", stmt.Line);
		}
		private ASMCode GenerateCall(string name, Expr_ValueList args, int line)
		{
			int c = args.ValueList.Count;
			ASMSnippet asm = ASMSnippet.FromEmpty();
			for (int i = c - 1; i >= 0; i--)
			{
				asm.Content.Add(
					GenerateExpressionToPush(args.ValueList[i]));
			}
			if (Translator.GlobalNameManager.GlobalNames.Contains(name))
				asm.Content.Add((ASMInstruction)$"call {GlobalNameManager.GetGlobalName(name)}");
			else
				throw new TranslationException($"Unknown global(function):'{name}'", line);
			if (c > 0)
				asm.Content.Add((ASMInstruction)$"add sp,{c}");
			return asm;
		}
		private ASMCode GenerateExpressionToPush(Expression e)
		{
			if (e is Expr_Variable ev)
			{
				if (SearchOffset(ev.Name, out int off))
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"push [bp{GetLocalOffset(off)}]",
					});
				else if (Translator.GlobalNameManager.GlobalNames.Contains(ev.Name))
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"push [{GlobalNameManager.GetGlobalName(ev.Name)}]",
					});
				else
					throw new TranslationException("Unknown global or local", ev.Line);
			}
			else if (e is Expr_Call ec)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateCall(ec.Name,ec.Arguments,ec.Line),
						(ASMInstruction)$"push r0",
				});
			}
			else if (e is Expr_Value eval)
			{
				if (eval.Value.StartsWith("\"") || eval.Value.StartsWith("\'"))
				{
					int n = Translator.ConstantManager.AddString(
						eval.Value.Substring(0, eval.Value.Length));

					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"push {ConstantManager.GetConstant(n)}",
					});
				}
				else//number
				{
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"push {eval.Value}",
					});
				}
			}
			else if (e is Expr_ValueList evl)
			{
				if (!evl.ValueList.TrueForAll(t => t is Expr_Value))
					throw new TranslationException("Array can only contain constants", evl.Line);
				int n = Translator.ConstantManager.AddArray(
					evl.ValueList.Select(t => (t as Expr_Value).Value).ToList());

				return ASMSnippet.FromCode(
					new ASMCode[] {
							(ASMInstruction)$"push {ConstantManager.GetConstant(n)}",
					});
			}
			else if (e is Expr_Binary eb)
			{
				var o = eb.TryContract();
				if (o is Expr_Value)
				{
					return GenerateExpressionToPush(o);
				}
				else
				{
					return ASMSnippet.FromCode(
					   new ASMCode[] {
						   GenerateBinaryExpression(eb),
						   (ASMInstruction)$"push r1",
					   });
				}
			}
			else if (e is Expr_Ref er)
			{
				if (er.Type == Expr_Ref.RefType.Address)
				{
					if (!(er.Value is Expr_Variable))
						throw new TranslationException("Only local or global can be applied '&'", er.Line);
					var ev4 = er.Value as Expr_Variable;
					if (SearchOffset(ev4.Name, out int off))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov r0,bp",
								(ASMInstruction)$"add r0,{GetLocalOffset(off)}",
								(ASMInstruction)$"push r0",
					});
					else if (Translator.GlobalNameManager.GlobalNames.Contains(ev4.Name))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"push {GlobalNameManager.GetGlobalName(ev4.Name)}",
						});
					else
						throw new TranslationException("Unknown global or local", ev4.Line);
				}
				else
				{
					return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(er.Value,"r0"),
								(ASMInstruction)$"mov r0,[r0]",
								(ASMInstruction)$"push r0",
						});
				}
			}
			throw new TranslationException("Unexpected expression", e.Line);
		}
		private ASMCode GenerateExpressionToReg(Expression e, string targetReg)
		{
			if (e is Expr_Variable ev)
			{
				if (SearchOffset(ev.Name, out int off))
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov {targetReg},[bp{GetLocalOffset(off)}]",
				});
				else if (Translator.GlobalNameManager.GlobalNames.Contains(ev.Name))
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov {targetReg},[{GlobalNameManager.GetGlobalName(ev.Name)}]",
					});
				else
					throw new TranslationException("Unknown global or local", ev.Line);
			}
			else if (e is Expr_Call ec)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateCall(ec.Name,ec.Arguments,ec.Line),
						(ASMInstruction)$"mov {targetReg},r0",
				});
			}
			else if (e is Expr_Value eval)
			{
				if (eval.Value.StartsWith("\"") || eval.Value.StartsWith("\'"))
				{
					int n = Translator.ConstantManager.AddString(
						eval.Value.Substring(0, eval.Value.Length));

					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov {targetReg},{ConstantManager.GetConstant(n)}",
					});
				}
				else//number
				{
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov {targetReg},{eval.Value}",
					});
				}
			}
			else if (e is Expr_ValueList evl)
			{
				if (!evl.ValueList.TrueForAll(t => t is Expr_Value))
					throw new TranslationException("Array can only contain constants", evl.Line);
				int n = Translator.ConstantManager.AddArray(
					evl.ValueList.Select(t => (t as Expr_Value).Value).ToList());

				return ASMSnippet.FromCode(
					new ASMCode[] {
							(ASMInstruction)$"mov {targetReg},{ConstantManager.GetConstant(n)}",
					});
			}
			else if (e is Expr_Binary eb)
			{
				var o = eb.TryContract();
				if (o is Expr_Value)
				{
					return GenerateExpressionToReg(o, targetReg);
				}
				else
				{
					return ASMSnippet.FromCode(
					   new ASMCode[] {
						   GenerateBinaryExpression(eb),
						   (ASMInstruction)$"mov {targetReg},r1",
					   });
				}
			}
			else if (e is Expr_Ref er)
			{
				if (er.Type == Expr_Ref.RefType.Address)
				{
					if (!(er.Value is Expr_Variable))
						throw new TranslationException("Only local or global can be applied '&'", er.Line);
					var ev4 = er.Value as Expr_Variable;
					if (SearchOffset(ev4.Name, out int off))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov {targetReg},bp",
								(ASMInstruction)$"add {targetReg},{GetLocalOffset(off)}",
					});
					else if (Translator.GlobalNameManager.GlobalNames.Contains(ev4.Name))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov {targetReg},{GlobalNameManager.GetGlobalName(ev4.Name)}",
						});
					else
						throw new TranslationException("Unknown global or local", ev4.Line);
				}
				else
				{
					return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(er.Value,targetReg),
								(ASMInstruction)$"mov {targetReg},[{targetReg}]",
						});
				}
			}
			throw new TranslationException("Unexpected expression", e.Line);
		}
		private ASMCode GenerateExpressionToMem(Expression e, string targetMem)
		{
			if (e is Expr_Variable ev)
			{
				if (SearchOffset(ev.Name, out int off))
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov r0,[bp{GetLocalOffset(off)}]",
							(ASMInstruction)$"mov {targetMem},r0",
				});
				else if (Translator.GlobalNameManager.GlobalNames.Contains(ev.Name))
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov r0,[{GlobalNameManager.GetGlobalName(ev.Name)}]",
							(ASMInstruction)$"mov {targetMem},r0",
					});
				else
					throw new TranslationException("Unknown global or local", ev.Line);
			}
			else if (e is Expr_Call ec)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateCall(ec.Name,ec.Arguments,ec.Line),
						(ASMInstruction)$"mov {targetMem},r0",
				});
			}
			else if (e is Expr_Value eval)
			{
				if (eval.Value.StartsWith("\"") || eval.Value.StartsWith("\'"))
				{
					int n = Translator.ConstantManager.AddString(
						eval.Value.Substring(0, eval.Value.Length));

					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov {targetMem},{ConstantManager.GetConstant(n)}",
					});
				}
				else//number
				{
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov {targetMem},{eval.Value}",
					});
				}
			}
			else if (e is Expr_ValueList evl)
			{
				if (!evl.ValueList.TrueForAll(t => t is Expr_Value))
					throw new TranslationException("Array can only contain constants", evl.Line);
				int n = Translator.ConstantManager.AddArray(
					evl.ValueList.Select(t => (t as Expr_Value).Value).ToList());

				return ASMSnippet.FromCode(
					new ASMCode[] {
							(ASMInstruction)$"mov {targetMem},{ConstantManager.GetConstant(n)}",
					});
			}
			else if (e is Expr_Binary eb)
			{
				var o = eb.TryContract();
				if (o is Expr_Value)
				{
					return GenerateExpressionToMem(o, targetMem);
				}
				else
				{
					return ASMSnippet.FromCode(
					   new ASMCode[] {
						   GenerateBinaryExpression(eb),
						   (ASMInstruction)$"mov {targetMem},r1",
					   });
				}
			}
			else if (e is Expr_Ref er)
			{
				if (er.Type == Expr_Ref.RefType.Address)
				{
					if (!(er.Value is Expr_Variable))
						throw new TranslationException("Only local or global can be applied '&'", er.Line);
					var ev4 = er.Value as Expr_Variable;
					if (SearchOffset(ev4.Name, out int off))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov r0,bp",
								(ASMInstruction)$"add r0,{GetLocalOffset(off)}",
								(ASMInstruction)$"mov {targetMem},r0",
					});
					else if (Translator.GlobalNameManager.GlobalNames.Contains(ev4.Name))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov {targetMem},{GlobalNameManager.GetGlobalName(ev4.Name)}",
						});
					else
						throw new TranslationException("Unknown global or local", ev4.Line);
				}
				else
				{
					return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(er.Value,"r0"),
								(ASMInstruction)$"mov r0,[r0]",
								(ASMInstruction)$"mov {targetMem},r0",
						});
				}
			}
			throw new TranslationException("Unexpected expression", e.Line);
		}


		/// <summary>
		/// r1/r2
		/// </summary>
		/// <param name="b"></param>
		/// <returns></returns>
		private ASMCode GenerateBinaryExpression(Expr_Binary b)
		{
			string opCode = null;
			switch (b.Operator)
			{
				case "+":
					opCode = "add";
					break;
				case "-":
					opCode = "sub";
					break;
				default:
					throw new TranslationException($"Unexpected operator:'{b.Operator}'", b.Line);
			}


			return ASMSnippet.FromCode(
				new ASMCode[] {
							GenerateExpressionToPush(b.Left),
							GenerateExpressionToPush(b.Right),
							(ASMInstruction)"pop r2",
							(ASMInstruction)"pop r1",
							(ASMInstruction)$"{opCode} r1,r2",
				});
		}
	}
}
