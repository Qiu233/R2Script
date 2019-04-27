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
		private FunctionFactory(Stmt_Function func)
		{
			this.Function = func;
			this.OffsetTables = new Dictionary<SymbolTable, OffsetTable>();
		}
		public static FunctionFactory FromFunction(SymbolTable global, Stmt_Function func)
		{
			var n = new FunctionFactory(func);
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
					Function.Locals.Contains((t as SymbolIden).Name))
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
					if (Function.Locals.Contains(si.Name))
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
						ASMInstruction.Create($"\n\n_{Function.Name}:",false),
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
				snippet.Instructions.Add(GenerateStatement(stmt));
			}
			return snippet;
		}
		private ASMCode GenerateStatement(Statement stmt)
		{
			if (stmt is Stmt_Var)
			{
				Stmt_Var v = stmt as Stmt_Var;
				foreach (var variable in v.Variables)
				{
					if (variable is Stmt_Var.VariableValue vv)
					{
						if (vv.InitialValue == null) continue;
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToMem(vv.InitialValue, $"[bp{GetLocalOffset(vv.Name)}]"),
							});
					}
					else//array
					{
						throw new TranslationException("Array cannot be a constant inside a function", v.Line);
					}
				}
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
			asm.Instructions.Add((ASMInstruction)$"call _{name}");
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
			throw new TranslationException("Unexpected expression", e.Line);
		}
	}
}
