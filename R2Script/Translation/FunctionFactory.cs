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
		private int _maxStack = 0;
		private int _binaryLayer = 1;
		private string _continueLabel = null;
		private string _breakLabel = null;
		public Translator Translator
		{
			get;
		}
		public Stmt_Function Function
		{
			get;
		}
		public int MaxStack
		{
			get => _maxStack;
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

		private LocalLabelManager FlowLabelManager
		{
			get;
		}
		private LocalLabelManager CalcLabelManager
		{
			get;
		}
		public ASMCode ASMCode
		{
			get;
			private set;
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
			this.FlowLabelManager = new LocalLabelManager("FLOW");
			this.CalcLabelManager = new LocalLabelManager("CALC");
			OffsetTables.Push(RootOffsetTable);
		}
		public static FunctionFactory FromFunction(Translator translator, Stmt_Function func)
		{
			var n = new FunctionFactory(translator, func);
			n.CheckLocals(func.Body.SymbolTable, new List<string>());
			n.MapArgs();
			n.ASMCode = n.GenerateCode();
			return n;
		}

		private void CheckLocals(SymbolTable tab, List<string> names)
		{
			tab.Symbols.ForEach(t =>
			{
				if (t is SymbolIden)
				{
					var si = t as SymbolIden;
					if (names.Contains(si.Name))
						throw new TranslationException("Local duplicated:'" + si.Name + "'", si.Line, Function.File);
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
			var code = ASMSnippet.FromCode(
				new ASMCode[] {
					(ASMInstruction)"push bp",
					(ASMInstruction)"mov bp,sp",
			});
			if (MaxStack > 0)
				code.Content.Add((ASMInstruction)$"sub sp,{MaxStack}");
			return code;
		}
		private ASMCode GetFunctionEnd()
		{
			var asm = ASMSnippet.FromEmpty();
			if (MaxStack > 0)
				asm.Content.Add((ASMInstruction)$"add sp,{MaxStack}");
			if (!Naked)
				asm.Content.Add((ASMInstruction)"pop bp");
			asm.Content.Add((ASMInstruction)"ret");
			return asm;
		}

		private ASMCode GenerateCode()
		{
			var asm = ASMSnippet.FromEmpty();
			var body = GenerateBody();
			asm.Content.Add(GetFunctionHead());
			asm.Content.Add(body);
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
						throw new TranslationException("Only native ASMs is allowed to be in a naked function", stmt.Line, stmt.File);
					var sa = stmt as Stmt_ASM;
					e.Content.Add((ASMInstruction)(sa.ASM + "\n"));
				}
				return e;
			}
		}
		private ASMCode GenerateStatement(Statement stmt)
		{
			if (stmt is Stmt_Block sb)
			{
				var asm = ASMSnippet.FromEmpty();
				foreach (var subStmt in sb.Statements)
					asm.Content.Add(GenerateStatement(subStmt));
				return asm;
			}
			else if (stmt is Stmt_Var v)
			{
				ASMSnippet asm = ASMSnippet.FromEmpty();
				foreach (var variable in v.Variables)
				{
					int off = StackUse;
					if (variable.InitialValue == null)
					{
						StackUse++;
						_maxStack = Math.Max(_maxStack, StackUse);
						AddOffset(variable.Name, off);
						continue;
					}
					if (!(variable is Stmt_Var.Variable))
					{
						StackUse++;
						_maxStack = Math.Max(_maxStack, StackUse);
						AddOffset(variable.Name, off);
						continue;
					}
					Stmt_Var.Variable vv = variable as Stmt_Var.Variable;
					if (vv is Stmt_Var.VariableArray)
					{
						int len = 0;
						if (vv.InitialValue is Expr_Binary eb)
						{
							Expression ex = null;
							if (!((ex = eb.TryContract()) is Expr_Value))
								throw new TranslationException("Array's length should be a constant", eb.Line, eb.File);
							len = Convert.ToInt32((ex as Expr_Value).Value);
						}
						else if (vv.InitialValue is Expr_Value ev)
						{
							len = Convert.ToInt32(ev.Value);
						}
						else
							throw new TranslationException("Array's length should be a constant", v.Line, v.File);
						if (len <= 0)
							throw new TranslationException("Array's length should not be lower or equal to 0", v.Line, v.File);
						StackUse += len + 1;
						_maxStack = Math.Max(_maxStack, StackUse);

						AddOffset(variable.Name, off);
						asm.Content.Add(ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov r0,bp",
								(ASMInstruction)$"add r0,{GetLocalOffset(off+len)}",
								(ASMInstruction)$"mov [bp{GetLocalOffset(off)}],r0",
						}));
					}
					else
					{
						StackUse++;
						_maxStack = Math.Max(_maxStack, StackUse);
						AddOffset(variable.Name, off);
						asm.Content.Add(ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToMem(vv.InitialValue, $"[bp{GetLocalOffset(off)}]"),
							}));
					}
				}
				return asm;
			}
			else if (stmt is Stmt_Assign sa)
			{
				bool b = SearchOffset(sa.Name, out int off);
				if (!b &&
					!Translator.GlobalNameManager.GlobalNames.Contains(sa.Name))
					throw new TranslationException($"Unknown global or local:'{sa.Name}'", sa.Line, sa.File);
				if (b)
				{
					if (!(sa is Stmt_Assign_Index))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToMem(sa.Value, $"[bp{GetLocalOffset(off)}]"),
							});
					else
					{
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(sa.Value,"r3"),
								GenerateExpressionToReg((sa as Stmt_Assign_Index).Index,"r0"),
								(ASMInstruction)$"add r0,[bp{GetLocalOffset(off)}]",
								(ASMInstruction)$"mov [r0],r3",
							});
					}
				}
				else
				{
					if (!(sa is Stmt_Assign_Index))
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToMem(sa.Value, $"[{GlobalNameManager.GetGlobalName(sa.Name)}]"),
							});
					else
					{
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(sa.Value,"r3"),
								GenerateExpressionToReg((sa as Stmt_Assign_Index).Index,"r0"),
								(ASMInstruction)$"add r0,[{GlobalNameManager.GetGlobalName(sa.Name)}]",
								(ASMInstruction)$"mov [r0],r3",
							});
					}
				}
			}
			else if (stmt is Stmt_Return sr)
			{
				return ASMSnippet.FromCode(
					new ASMCode[] {
						GenerateExpressionToReg(sr.Value, "r0"),
						GetFunctionEnd(),
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
			else if (stmt is Stmt_IF sif)
			{
				var asm = ASMSnippet.FromEmpty();
				List<string> IFLabels = sif.IF.Select(t => FlowLabelManager.GetNew()).ToList();
				IFLabels.Add(FlowLabelManager.GetNew());
				for (int i = 0; i < sif.IF.Count; i++)
				{
					var ifc = sif.IF[i];
					asm.Content.Add(GenerateExpressionToReg(ifc.Condition, "r0"));
					asm.Content.Add((ASMInstruction)$"cmp r0,0");
					asm.Content.Add((ASMInstruction)$"jz {IFLabels[i]}");

					PushNewOffsetTable();
					asm.Content.Add(GenerateStatement(ifc.Body));
					PopNewOffsetTable();

					asm.Content.Add((ASMInstruction)$"jmp {IFLabels.Last()}");
					asm.Content.Add(ASMInstruction.Create($"{IFLabels[i]}:", false));
				}

				if (sif.Else != null)
				{
					PushNewOffsetTable();
					asm.Content.Add(GenerateStatement(sif.Else));
					PopNewOffsetTable();
				}
				asm.Content.Add(ASMInstruction.Create($"{IFLabels.Last()}:", false));


				return asm;
			}
			else if (stmt is Stmt_While sw)
			{
				var asm = ASMSnippet.FromEmpty();
				string loop = FlowLabelManager.GetNew();
				string exit = FlowLabelManager.GetNew();

				asm.Content.Add(ASMInstruction.Create($"{loop}:", false));
				asm.Content.Add(GenerateExpressionToReg(sw.Condition, "r0"));
				asm.Content.Add((ASMInstruction)$"cmp r0,0");
				asm.Content.Add((ASMInstruction)$"jz {exit}");

				string tmp_break_label = _breakLabel;
				string tmp_continue_label = _continueLabel;
				_breakLabel = exit;
				_continueLabel = loop;
				PushNewOffsetTable();
				asm.Content.Add(GenerateStatement(sw.Body));
				PopNewOffsetTable();
				_breakLabel = tmp_break_label;
				_continueLabel = tmp_continue_label;

				asm.Content.Add((ASMInstruction)$"jmp {loop}");
				asm.Content.Add(ASMInstruction.Create($"{exit}:", false));

				return asm;
			}
			else if (stmt is Stmt_Break)
			{
				if (_breakLabel == null)
					throw new TranslationException("'break' should be inside a loop", stmt.Line, stmt.File);
				return ASMSnippet.FromCode(
					new ASMCode[] {
						(ASMInstruction)$"jmp {_breakLabel}",
					});
			}
			else if (stmt is Stmt_Continue)
			{
				if (_continueLabel == null)
					throw new TranslationException("'continue' should be inside a loop", stmt.Line, stmt.File);
				return ASMSnippet.FromCode(
					new ASMCode[] {
						(ASMInstruction)$"jmp {_continueLabel}",
					});
			}
			throw new TranslationException("Unexpected statement", stmt.Line, stmt.File);
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
			asm.Content.Add((ASMInstruction)$"call {GlobalNameManager.GetGlobalName(name)}");
			if (c > 0)
				asm.Content.Add((ASMInstruction)$"add sp,{c}");
			return asm;
		}
		private ASMCode GenerateExpressionToPush(Expression e)
		{
			if (e is Expr_Variable ev)
			{
				if (SearchOffset(ev.Name, out int off))
				{
					if (e is Expr_Variable_Index evi)
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(evi.Index,"r0"),
								(ASMInstruction)$"add r0,[bp{GetLocalOffset(off)}]",
								(ASMInstruction)$"push [r0]",
							});
					else
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"push [bp{GetLocalOffset(off)}]",
							});
				}
				else if (Translator.GlobalNameManager.GlobalNames.Contains(ev.Name))
				{
					if (e is Expr_Variable_Index evi)
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(evi.Index,"r0"),
								(ASMInstruction)$"add r0,[{GlobalNameManager.GetGlobalName(ev.Name)}]",
								(ASMInstruction)$"push [r0]",
							});
					else
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"push [{GlobalNameManager.GetGlobalName(ev.Name)}]",
							});
				}
				else
					throw new TranslationException("Unknown global or local", ev.Line, ev.File);
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
					throw new TranslationException("Array can only contain constants", evl.Line, evl.File);
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
						throw new TranslationException("Only local or global can be applied '&'", er.Line, er.File);
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
						throw new TranslationException("Unknown global or local", ev4.Line, ev4.File);
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
			throw new TranslationException("Unexpected expression", e.Line, e.File);
		}
		private ASMCode GenerateExpressionToReg(Expression e, string targetReg)
		{
			if (e is Expr_Variable ev)
			{
				if (SearchOffset(ev.Name, out int off))
				{
					if (e is Expr_Variable_Index evi)
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(evi.Index,targetReg),
								(ASMInstruction)$"add {targetReg},[bp{GetLocalOffset(off)}]",
								(ASMInstruction)$"mov {targetReg},[{targetReg}]",
							});
					else
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov {targetReg},[bp{GetLocalOffset(off)}]",
							});
				}
				else if (Translator.GlobalNameManager.GlobalNames.Contains(ev.Name))
				{
					if (e is Expr_Variable_Index evi)
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(evi.Index,targetReg),
								(ASMInstruction)$"add {targetReg},[{GlobalNameManager.GetGlobalName(ev.Name)}]",
								(ASMInstruction)$"mov {targetReg},[{targetReg}]",
							});
					else
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov {targetReg},[{GlobalNameManager.GetGlobalName(ev.Name)}]",
							});
				}
				else
					throw new TranslationException("Unknown global or local", ev.Line, ev.File);
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
					throw new TranslationException("Array can only contain constants", evl.Line, evl.File);
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
						throw new TranslationException("Only local or global can be applied '&'", er.Line, er.File);
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
						throw new TranslationException("Unknown global or local", ev4.Line, ev4.File);
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
			throw new TranslationException("Unexpected expression", e.Line, e.File);
		}
		private ASMCode GenerateExpressionToMem(Expression e, string targetMem)
		{
			if (e is Expr_Variable ev)
			{
				if (SearchOffset(ev.Name, out int off))
				{
					if (e is Expr_Variable_Index evi)
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(evi.Index,"r0"),
								(ASMInstruction)$"add r0,[bp{GetLocalOffset(off)}]",
								(ASMInstruction)$"mov r0,[r0]",
								(ASMInstruction)$"mov {targetMem},r0",
							});
					else
						return ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"mov r0,[bp{GetLocalOffset(off)}]",
								(ASMInstruction)$"mov {targetMem},r0",
							});
				}
				else if (Translator.GlobalNameManager.GlobalNames.Contains(ev.Name))
				{
					if (e is Expr_Variable_Index evi)
						return ASMSnippet.FromCode(
							new ASMCode[] {
								GenerateExpressionToReg(evi.Index,"r0"),
								(ASMInstruction)$"add r0,[{GlobalNameManager.GetGlobalName(ev.Name)}]",
								(ASMInstruction)$"mov r0,[r0]",
								(ASMInstruction)$"mov {targetMem},r0",
							});
					else
						return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov r0,[{GlobalNameManager.GetGlobalName(ev.Name)}]",
							(ASMInstruction)$"mov {targetMem},r0",
					});
				}
				else
					throw new TranslationException("Unknown global or local", ev.Line, ev.File);
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
							(ASMInstruction)$"mov r0,{ConstantManager.GetConstant(n)}",
							(ASMInstruction)$"mov {targetMem},r0",
					});
				}
				else//number
				{
					return ASMSnippet.FromCode(
						new ASMCode[] {
							(ASMInstruction)$"mov r0,{eval.Value}",
							(ASMInstruction)$"mov {targetMem},r0",
					});
				}
			}
			else if (e is Expr_ValueList evl)
			{
				if (!evl.ValueList.TrueForAll(t => t is Expr_Value))
					throw new TranslationException("Array can only contain constants", evl.Line, evl.File);
				int n = Translator.ConstantManager.AddArray(
					evl.ValueList.Select(t => (t as Expr_Value).Value).ToList());

				return ASMSnippet.FromCode(
					new ASMCode[] {
							(ASMInstruction)$"mov r0,{ConstantManager.GetConstant(n)}",
							(ASMInstruction)$"mov {targetMem},r0",
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
						throw new TranslationException("Only local or global can be applied '&'", er.Line, er.File);
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
						throw new TranslationException("Unknown global or local", ev4.Line, ev4.File);
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
			throw new TranslationException("Unexpected expression", e.Line, e.File);
		}


		private ASMCode GenerateBinaryRaw(Expr_Binary b)
		{
			string m1 = $"[bp{GetLocalOffset(_binaryLayer)}]";
			string m2 = $"[bp{GetLocalOffset(_binaryLayer + 1)}]";
			var asm = ASMSnippet.FromEmpty();
			if (b.Left is Expr_Value && b.Right is Expr_Value)
			{
				asm.Content.Add(GenerateExpressionToReg(b.Left, "r1"));
				asm.Content.Add(GenerateExpressionToReg(b.Right, "r2"));
			}
			else if ((b.Left is Expr_Value && b.Right is Expr_Variable) ||
				(b.Left is Expr_Variable && b.Right is Expr_Value) || (b.Left is Expr_Variable && b.Right is Expr_Variable))
			{
				asm.Content.Add(GenerateExpressionToReg(b.Left, "r1"));
				asm.Content.Add(GenerateExpressionToReg(b.Right, "r2"));
			}
			else if (b.Left is Expr_Value || b.Left is Expr_Variable)
			{
				_binaryLayer++;
				asm.Content.Add(GenerateExpressionToMem(b.Right, m1));
				asm.Content.Add((ASMInstruction)$"mov r2,{m1}");
				asm.Content.Add(GenerateExpressionToReg(b.Left, "r1"));
				_binaryLayer--;
			}
			else if (b.Right is Expr_Value || b.Right is Expr_Variable)
			{
				_binaryLayer++;
				asm.Content.Add(GenerateExpressionToMem(b.Left, m1));
				asm.Content.Add((ASMInstruction)$"mov r1,{m1}");
				asm.Content.Add(GenerateExpressionToReg(b.Right, "r2"));
				_binaryLayer--;
			}
			else
			{
				_binaryLayer++;
				asm.Content.Add(GenerateExpressionToMem(b.Left, m1));
				asm.Content.Add(GenerateExpressionToMem(b.Right, m2));
				asm.Content.Add((ASMInstruction)$"mov r1,{m1}");
				asm.Content.Add((ASMInstruction)$"mov r2,{m2}");
				_binaryLayer--;
			}
			return asm;
		}

		/// <summary>
		/// r1/r2
		/// </summary>
		/// <param name="b"></param>
		/// <returns></returns>
		private ASMCode GenerateBinaryExpression(Expr_Binary b)
		{
			ASMSnippet asm = ASMSnippet.FromEmpty();
			asm.Content.Add(GenerateBinaryRaw(b));
			switch (b.Operator)
			{
				case "+":
					asm.Content.Add((ASMInstruction)"add r1,r2");
					break;
				case "-":
					asm.Content.Add((ASMInstruction)"sub r1,r2");
					break;
				case "==":
					{
						string l1 = CalcLabelManager.GetNew();
						string l2 = CalcLabelManager.GetNew();
						asm.Content.Add(ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"cmp r1,r2",
								(ASMInstruction)$"jz {l1}",
								(ASMInstruction)$"mov r1,0",
								(ASMInstruction)$"jmp {l2}",
								ASMInstruction.Create($"{l1}:",false),
								(ASMInstruction)$"mov r1,1",
								ASMInstruction.Create($"{l2}:",false),
							}));
						break;
					}
				case ">":
					{
						string l1 = CalcLabelManager.GetNew();
						string l2 = CalcLabelManager.GetNew();
						asm.Content.Add(ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"cmp r1,r2",
								(ASMInstruction)$"jg {l1}",
								(ASMInstruction)$"mov r1,0",
								(ASMInstruction)$"jmp {l2}",
								ASMInstruction.Create($"{l1}:",false),
								(ASMInstruction)$"mov r1,1",
								ASMInstruction.Create($"{l2}:",false),
							}));
						break;
					}
				case ">=":
					{
						string l1 = CalcLabelManager.GetNew();
						string l2 = CalcLabelManager.GetNew();
						asm.Content.Add(ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"cmp r1,r2",
								(ASMInstruction)$"jge {l1}",
								(ASMInstruction)$"mov r1,0",
								(ASMInstruction)$"jmp {l2}",
								ASMInstruction.Create($"{l1}:",false),
								(ASMInstruction)$"mov r1,1",
								ASMInstruction.Create($"{l2}:",false),
							}));
						break;
					}
				case "<":
					{

						string l1 = CalcLabelManager.GetNew();
						string l2 = CalcLabelManager.GetNew();
						asm.Content.Add(ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"cmp r1,r2",
								(ASMInstruction)$"jl {l1}",
								(ASMInstruction)$"mov r1,0",
								(ASMInstruction)$"jmp {l2}",
								ASMInstruction.Create($"{l1}:",false),
								(ASMInstruction)$"mov r1,1",
								ASMInstruction.Create($"{l2}:",false),
							}));
						break;
					}
				case "<=":
					{

						string l1 = CalcLabelManager.GetNew();
						string l2 = CalcLabelManager.GetNew();
						asm.Content.Add(ASMSnippet.FromCode(
							new ASMCode[] {
								(ASMInstruction)$"cmp r1,r2",
								(ASMInstruction)$"jle {l1}",
								(ASMInstruction)$"mov r1,0",
								(ASMInstruction)$"jmp {l2}",
								ASMInstruction.Create($"{l1}:",false),
								(ASMInstruction)$"mov r1,1",
								ASMInstruction.Create($"{l2}:",false),
							}));
						break;
					}
				default:
					throw new TranslationException($"Unexpected operator:'{b.Operator}'", b.Line, b.File);
			}
			return asm;
		}
	}
}
