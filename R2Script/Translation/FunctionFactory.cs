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
		private int _maxStack;
		public Translator Translator
		{
			get;
		}
		private Stmt_Function Function
		{
			get;
		}
		public int MaxStack
		{
			get => _maxStack;
		}
		public Dictionary<SymbolTable, OffsetTable> OffsetTables
		{
			get;
		}
		private FunctionFactory(Translator translator, Stmt_Function func)
		{
			this.Translator = translator;
			this.Function = func;
			this.OffsetTables = new Dictionary<SymbolTable, OffsetTable>();
		}
		public static FunctionFactory FromFunction(Translator translator, Stmt_Function func)
		{
			var n = new FunctionFactory(translator, func);
			CheckLocals(func.Body.SymbolTable, new List<string>());
			n.MapArgs();
			n.MapLocals(func.Body.SymbolTable, 0);
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
			if (!OffsetTables.ContainsKey(Function.Body.SymbolTable))
				OffsetTables[Function.Body.SymbolTable] = new OffsetTable();
			var table = OffsetTables[Function.Body.SymbolTable];
			int bs = -1;
			foreach (var t in Function.Body.SymbolTable.Symbols)
			{
				if (t is SymbolIden &&
					Function.Args.Contains((t as SymbolIden).Name))
					table.Add((t as SymbolIden).Name, bs--);
			}
		}
		private OffsetTable MapLocals(SymbolTable sym, int stackBase)
		{
			if (!OffsetTables.ContainsKey(sym))
				OffsetTables[sym] = new OffsetTable();
			var table = OffsetTables[sym];

			sym.Symbols.ForEach(t =>
			{
				if (t is SymbolIden)
				{
					var si = t as SymbolIden;
					if (Function.Args.Contains(si.Name))
						return;
					table.Add(si.Name, stackBase++);
					_maxStack = Math.Max(_maxStack, stackBase);
				}
				else
				{
					var st = t as SymbolTable;
					MapLocals(st, stackBase);
				}
			});
			return table;
		}

		private string GetLocalOffset(string n)
		{
			int off = OffsetTables[Function.Body.SymbolTable].Offsets[n];
			if (off < 0)//args
				return "+" + (0 - off + 1);//-1 to +2/   -2 to +3 and so on
			else//locals
				return "-" + (off + 1);
		}

		public ASMCode GenerateCode()
		{
			return
				ASMSnippet.FromCode(
					new ASMCode[] {
						(ASMInstruction)"push bp",
						(ASMInstruction)"mov bp,sp",
						GenerateBody(),
						(ASMInstruction)"pop bp",
						(ASMInstruction)"ret"
				});
		}
		private ASMCode GenerateBody()
		{
			ASMSnippet snippet = ASMSnippet.FromEmpty();
			foreach (var stmt in Function.Body.Statements)
			{
				var s = GenerateStatement(stmt);
				if (s == null) continue;
				snippet.Instructions.Add(s);
			}
			return snippet;
		}
		private ASMCode GenerateStatement(Statement stmt)
		{
			if (stmt is Stmt_Var v)
			{
				foreach (var variable in v.Variables)
				{
					if (variable.InitialValue == null)
						continue;
					if (!(variable is Stmt_Var.Variable))
						continue;
					Stmt_Var.Variable vv = variable as Stmt_Var.Variable;
					return ASMSnippet.FromCode(
						new ASMCode[] {
								GenerateExpressionToMem(vv.InitialValue, $"[bp{GetLocalOffset(vv.Name)}]"),
						});
				}
				return null;
			}
			else if (stmt is Stmt_Assign sa)
			{
				if (!Function.Ar gs.Contains(sa.Name) &&
					!Translator.GlobalNameManager.GlobalNames.Contains(sa.Name))
					throw new TranslationException($"Unknown local or variable:'{sa.Name}'", sa.Line);
				if (Function.Ar gs.Contains(sa.Name))
					return ASMSnippet.FromCode(
						new ASMCode[] {
							GenerateExpressionToMem(sa.Value, $"[bp{GetLocalOffset(sa.Name)}]"),
						});
				else
					return ASMSnippet.FromCode(
						new ASMCode[] {
							GenerateExpressionToMem(sa.Value, $"[{GlobalNameManager.GetGlobalName(sa.Name)}]"),
						});
			}
			else if (stmt is Stmt_Return sr)
			{
				return GenerateExpressionToReg(sr.Value, "r0");
			}
			else if (stmt is Stmt_Call sc)
			{
				return GenerateCall(sc.Name, sc.Arguments);
			}
			throw new TranslationException("Unexpected statement", stmt.Line);
		}
		private ASMCode GenerateCall(string name, Expr_ValueList args)
		{
			int c = args.ValueList.Count;
			ASMSnippet asm = ASMSnippet.FromEmpty();
			for (int i = c - 1; i >= 0; i--)
			{
				asm.Instructions.Add(
					GenerateExpressionToPush(args.ValueList[i]));
			}
			if (Function.Ar gs.Contains(name))//local
				asm.Instructions.Add(
					ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"call [bp{GetLocalOffset(name)}]"
						}));
			else if (Translator.GlobalNameManager.GlobalNames.Contains(name))
				asm.Instructions.Add((ASMInstruction)$"call {GlobalNameManager.GetGlobalName(name)}");
			asm.Instructions.Add((ASMInstruction)$"add sp,{c}");
			return asm;
		}
		private ASMCode GenerateExpressionToPush(Expression e)
		{
			if (e is Expr_Variable ev)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						(ASMInstruction)$"push [bp{GetLocalOffset(ev.Name)}]",
				});
			}
			else if (e is Expr_Call ec)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateCall(ec.Name,ec.Arguments),
						(ASMInstruction)$"push r0",
				});
			}
			else if (e is Expr_Value eval)
			{
				if (eval.Value.StartsWith("\"") || eval.Value.StartsWith("\'"))
				{
					int n = Translator.ConstantManager.AddString(
						eval.Value.Substring(1, eval.Value.Length - 2));

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
			throw new TranslationException("Unexpected expression", e.Line);
		}
		private ASMCode GenerateExpressionToReg(Expression e, string targetReg)
		{
			if (e is Expr_Variable ev)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						(ASMInstruction)$"mov {targetReg},[bp{GetLocalOffset(ev.Name)}]",
				});
			}
			else if (e is Expr_Call ec)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateCall(ec.Name,ec.Arguments),
						(ASMInstruction)$"mov {targetReg},r0",
				});
			}
			else if (e is Expr_Value eval)
			{
				if (eval.Value.StartsWith("\"") || eval.Value.StartsWith("\'"))
				{
					int n = Translator.ConstantManager.AddString(
						eval.Value.Substring(1, eval.Value.Length - 2));

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
			throw new TranslationException("Unexpected expression", e.Line);
		}
		private ASMCode GenerateExpressionToMem(Expression e, string targetMem)
		{
			if (e is Expr_Variable ev)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						(ASMInstruction)$"mov r0,[bp{GetLocalOffset(ev.Name)}]",
						(ASMInstruction)$"mov {targetMem},r0",
				});
			}
			else if (e is Expr_Call ec)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateCall(ec.Name,ec.Arguments),
						(ASMInstruction)$"mov {targetMem},r0",
				});
			}
			else if (e is Expr_Value eval)
			{
				if (eval.Value.StartsWith("\"") || eval.Value.StartsWith("\'"))
				{
					int n = Translator.ConstantManager.AddString(
						eval.Value.Substring(1, eval.Value.Length - 2));

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
			throw new TranslationException("Unexpected expression", e.Line);
		}


		/// <summary>
		/// r1/r2
		/// </summary>
		/// <param name="b"></param>
		/// <returns></returns>
		public ASMCode GenerateBinaryExpression(Expr_Binary b)
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
