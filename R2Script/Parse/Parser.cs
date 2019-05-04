using R2Script.Lex;
using R2Script.Parse.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse
{
	public class Parser
	{
		private Stack<SymbolTable> SymbolTables;
		public SymbolTable RootSymbolTable
		{
			get;
		}
		public string Code
		{
			get;
		}
		public string File
		{
			get;
		}
		private Tokenizer Tokenizer
		{
			get;
		}
		public Parser(string code, string file)
		{
			this.SymbolTables = new Stack<SymbolTable>();
			this.Code = code;
			this.Tokenizer = new Tokenizer(code, file);
			this.File = file;
			this.RootSymbolTable = new SymbolTable(0);
			SymbolTables.Push(RootSymbolTable);//root
		}

		private Token Token => Tokenizer.Get();
		private int Line => Token.Line;
		private int NextToken()
		{
			return Tokenizer.NextToken();
		}
		private bool Match(TokenType type)
		{
			if (Token.Type == type)
				return true;
			return false;
		}
		private bool Match(int type)
		{
			return Match((TokenType)type);
		}
		private void Accept(TokenType type)
		{
			if (Token.Type == type)
				NextToken();
			else
			{
				if ((int)type > 127)
					throw new ParseException("Faild to match token:" + type, Token.Line, Token.File);
				else throw new ParseException("Faild to match token:'" + (char)type + "'", Token.Line, Token.File);
			}
		}
		private void Accept(int type)
		{
			Accept((TokenType)type);
		}
		private void Accept()
		{
			NextToken();
		}
		private string AcceptName()
		{
			string o = Token.Value;
			if (Match(TokenType.TK_NAME))
				Accept();
			else
				throw new ParseException("Faild to match a NAME", Token.Line, Token.File);
			return o;
		}
		private void AcceptLineEnd(bool force = true)
		{
			if (Match(';'))
				Accept();
			else
			{
				if (!force) return;
				throw new ParseException("Maybe ';' is missing", Token.Line, Token.File);
			}
		}
		private void AddSymbol(string s, int line)
		{
			SymbolTables.Peek().Add(new SymbolIden(s, line));
		}

		public Stmt_Block Parse()
		{
			NextToken();
			var v = GetStatementBlock();
			if (Token.Type > 0)
				throw new ParseException("Surplus of tokens", Token.Line, Token.File);
			return v;
		}

		public Stmt_Block GetStatementBlock()
		{
			Stmt_Block block = new Stmt_Block(Line, File);
			block.SymbolTable = new SymbolTable(Line);
			block.Statements = new List<Statement>();
			SymbolTables.Peek().Add(block.SymbolTable);
			SymbolTables.Push(block.SymbolTable);
			Statement s = null;
			while ((s = GetStatement()) != null)
				block.Statements.Add(s);
			SymbolTables.Pop();
			return block;
		}

		public Expr_ValueList GetValueArray()
		{
			Expr_ValueList vl = new Expr_ValueList(Line, File);
			Accept('[');
			if (!Match(']'))
				vl.ValueList.Add(E());
			while (Match(','))
			{
				Accept();//,
				vl.ValueList.Add(E());
			}
			Accept(']');
			return vl;
		}

		private Stmt_Var.Variable GetVariableRaw()
		{
			string name = AcceptName();
			AddSymbol(name, Line);
			Stmt_Var.Variable v = null;
			if (Match('['))
			{
				v = new Stmt_Var.VariableArray();
				v.Name = name;
				Accept();//(
				v.InitialValue = E();
				Accept(']');
			}
			else
			{
				v = new Stmt_Var.Variable();
				v.Name = name;
				if (Match('='))
				{
					Accept();//=
					v.InitialValue = E();
				}
			}
			return v;
		}
		public Stmt_Var.Variable GetVariable()
		{
			if (!Match(','))
				return null;
			Accept();//,
			if (!Match(TokenType.TK_NAME))
				return null;
			return GetVariableRaw();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="forceSemicolon">Whether semicolon is forced(Available for few stmts)</param>
		/// <returns></returns>
		public Statement GetStatement(bool forceSemicolon = true)
		{
			#region code block
			if (Match('{'))
			{
				Accept();//{
				var b = GetStatementBlock();
				Accept('}');
				return b;
			}
			#endregion
			#region variable delaration
			else if (Match(TokenType.TK_KW_VAR))
			{
				Accept();//var
				Stmt_Var s = new Stmt_Var(Line, File);
				s.Variables = new List<Stmt_Var.Variable>();

				Stmt_Var.Variable vt = null;
				if ((vt = GetVariableRaw()) != null)
					s.Variables.Add(vt);

				while ((vt = GetVariable()) != null)
					s.Variables.Add(vt);
				AcceptLineEnd(forceSemicolon);//;

				return s;
			}
			#endregion
			#region function
			else if (Match(TokenType.TK_KW_FUNCTION))
			{
				Stmt_Function func = new Stmt_Function(Line, File);
				func.Args = new List<string>();
				Accept();//function
				if (Match(TokenType.TK_KW_NAKED))
				{
					Accept();//naked
					func.Naked = true;
				}
				func.Name = AcceptName();
				AddSymbol(func.Name, Line);
				Accept('(');
				List<SymbolIden> argIdens = new List<SymbolIden>();
				while (Match(TokenType.TK_NAME))
				{
					string name = AcceptName();
					func.Args.Add(name);
					argIdens.Add(new SymbolIden(name, Line));
					if (Match(','))
						Accept();
					else break;
				}
				Accept(')');
				Accept('{');
				func.Body = GetStatementBlock();//should be a block

				func.Body.SymbolTable.Symbols.InsertRange(0, argIdens);
				Accept('}');
				return func;
			}
			#endregion
			#region if
			else if (Match(TokenType.TK_KW_IF))
			{
				Stmt_IF si = new Stmt_IF(Line, File);
				si.IF = new List<Stmt_IF.IFStructure>();
				Accept();//if
				Accept('(');
				Expression condition = E();
				Accept(')');
				Statement body = GetStatement();
				si.IF.Add(new Stmt_IF.IFStructure(condition, body));

				while (Match(TokenType.TK_KW_ELSE))
				{
					Accept();//else
					if (Match(TokenType.TK_KW_IF))
					{
						Accept(TokenType.TK_KW_IF);
						Accept('(');
						Expression con = E();
						Accept(')');
						Statement bo = GetStatement();
						si.IF.Add(new Stmt_IF.IFStructure(con, bo));
					}
					else
					{
						si.Else = GetStatement();
						break;
					}
				}


				return si;
			}
			#endregion
			#region return
			else if (Match(TokenType.TK_KW_RETURN))
			{
				Accept();//return
				Expression v = E();
				AcceptLineEnd();
				return new Stmt_Return(Line, File) { Value = v };
			}
			#endregion
			#region while
			else if (Match(TokenType.TK_KW_WHILE))
			{
				Stmt_While sw = new Stmt_While(Line, File);
				Accept();//while
				Accept('(');
				sw.Condition = E();
				Accept(')');
				sw.Body = GetStatement();
				return sw;
			}
			#endregion
			#region break
			else if (Match(TokenType.TK_KW_BREAK))
			{
				Accept();//break
				AcceptLineEnd();
				return new Stmt_Break(Line, File);
			}
			#endregion
			#region continue
			else if (Match(TokenType.TK_KW_CONTINUE))
			{
				Accept();//continue
				AcceptLineEnd();
				return new Stmt_Continue(Line, File);
			}
			#endregion
			#region assign or call
			else if (Match(TokenType.TK_NAME))
			{
				string name = AcceptName();
				if (Match('='))
				{
					Accept();//=
					Expression e = E();
					AcceptLineEnd();
					return new Stmt_Assign(Line, File) { Name = name, Value = e };
				}
				else if (Match('['))
				{
					Accept();//[
					Expression index = E();
					Accept(']');
					Accept('=');
					Expression val = E();
					AcceptLineEnd();
					return new Stmt_Assign_Index(Line, File) { Name = name, Value = val, Index = index };
				}
				else if (Match('('))
				{
					var args = GetActualArguments();
					AcceptLineEnd();
					return new Stmt_Call(Line, File) { Name = name, Arguments = args };
				}
				throw new ParseException("Unknown statement", Token.Line, Token.File);
			}
			#endregion
			#region ASM
			else if (Match(TokenType.TK_SEG_ASM))
			{
				var s = new Stmt_ASM(Line, File) { ASM = Token.Value };
				Accept();//ASM
				return s;
			}
			#endregion
			#region Pre-Compile
			else if (Match(TokenType.TK_PRECOMP_IMPORT))
			{
				Accept();//@import
				string value = Token.Value;
				Accept(TokenType.TK_STRING);//value
				var s = new Stmt_Import(Line, File) { TargetFile = value.Substring(1, value.Length - 2) };
				return s;
			}
			else if (Match(TokenType.TK_PRECOMP_INCLUDE))
			{
				Accept();//@include
				string value = Token.Value;
				Accept(TokenType.TK_STRING);//value
				var s = new Stmt_Include(Line, File) { TargetFile = value.Substring(1, value.Length - 2) };
				return s;
			}
			#endregion
			return null;
		}

		private Expression E()
		{
			Expression left = D();
			Expr_Binary eb = E1(left);
			return eb == null ? left : eb;
		}

		private Expr_Binary E1(Expression left)
		{
			if (Match((int)TokenType.TK_OP_L_AND))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = D();
				eb.Operator = "&&";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = E1(eb);
				return eb1 == null ? eb : eb1;
			}
			else if (Match((int)TokenType.TK_OP_L_OR))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = D();
				eb.Operator = "||";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = E1(eb);
				return eb1 == null ? eb : eb1;
			}
			return null;
		}

		private Expression D()
		{
			Expression left = R();
			Expr_Binary eb = D1(left);
			return eb ?? left;
		}

		private Expr_Binary D1(Expression left)
		{
			if (Match((int)TokenType.TK_OP_EQ))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = R();
				eb.Operator = "==";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = D1(eb);
				return eb1 ?? eb;
			}
			if (Match((int)TokenType.TK_OP_NE))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = R();
				eb.Operator = "!=";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = D1(eb);
				return eb1 ?? eb;
			}
			else if (Match((int)TokenType.TK_OP_GE))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = R();
				eb.Operator = ">=";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = D1(eb);
				return eb1 ?? eb;
			}
			else if (Match((int)TokenType.TK_OP_LE))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = R();
				eb.Operator = "<=";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = D1(eb);
				return eb1 ?? eb;
			}
			else if (Match('>'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = R();
				eb.Operator = ">";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = D1(eb);
				return eb1 ?? eb;
			}
			else if (Match('<'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = R();
				eb.Operator = "<";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = D1(eb);
				return eb1 ?? eb;
			}
			return null;
		}

		private Expression R()
		{
			Expression left = T();
			Expr_Binary eb = R1(left);
			return eb ?? left;
		}

		private Expr_Binary R1(Expression left)
		{
			if (Match('+'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = W();
				eb.Operator = "+";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = R1(eb);
				return eb1 ?? eb;
			}
			else if (Match('-'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = W();
				eb.Operator = "-";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = R1(eb);
				return eb1 ?? eb;
			}
			return null;
		}



		private Expression W()
		{
			Expression left = T();
			Expr_Binary eb = W1(left);
			return eb ?? left;
		}


		private Expr_Binary W1(Expression left)
		{
			if (Match('|'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = T();
				eb.Operator = "|";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = W1(eb);
				return eb1 ?? eb;
			}
			else if (Match('&'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = T();
				eb.Operator = "&";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = W1(eb);
				return eb1 ?? eb;
			}
			return null;
		}


		private Expression T()
		{
			Expression left = F();
			Expr_Binary eb = T1(left);
			return eb ?? left;
		}


		private Expr_Binary T1(Expression left)
		{
			if (Match('*'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = F();
				eb.Operator = "*";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = T1(eb);
				return eb1 ?? eb;
			}
			else if (Match('/'))
			{
				Token t = Token;
				Accept();
				Expr_Binary eb = new Expr_Binary(Line, File);
				Expression right = F();
				eb.Operator = "/";
				eb.Left = left;
				eb.Right = right;
				Expr_Binary eb1 = T1(eb);
				return eb1 ?? eb;
			}
			return null;
		}
		private Expression F()//operand
		{
			if (Match('('))
			{
				Accept();//(
				Expression e = E();
				Accept(')');
				return e;
			}
			else if (Match('!'))
			{
				Accept();//!
				Expr_Single es = new Expr_Single(Line, File);
				es.Operator = "!";
				es.Value = E();
				return es;
			}
			else if (Match('['))
			{
				return GetValueArray();
			}
			else if (Match(TokenType.TK_STRING) || Match(TokenType.TK_CHARACTER))
			{
				var e = new Expr_Value(Line, File) { Value = Token.Value };
				Accept();
				return e;
			}
			else if (Match('-'))
			{
				Accept();//-
				Expression e = T();//lowest
				Expr_Binary eb = new Expr_Binary(Line, File);
				eb.Operator = "-";
				eb.Right = e;
				Expr_Value ev = new Expr_Value(Line, File);
				ev.Value = "0";
				eb.Left = ev;
				return eb;
			}
			else if (Match('*'))//value
			{
				Accept();//*
				Expr_Ref r = new Expr_Ref(Line, File);
				r.Value = E();
				r.Type = Expr_Ref.RefType.Value;
				return r;
			}
			else if (Match('&'))//address
			{
				Accept();//&
				Expr_Ref r = new Expr_Ref(Line, File);
				r.Value = E();
				r.Type = Expr_Ref.RefType.Address;
				return r;
			}
			else if (Match(TokenType.TK_NUMBER))
			{
				Expr_Value ev = new Expr_Value(Line, File);
				ev.Value = Token.Value;
				Accept();
				return ev;
			}
			else if (Match(TokenType.TK_NAME))
			{
				string name = AcceptName();
				if (Match('('))
				{
					return CallFunction(name, GetActualArguments());
				}
				else
				{
					Expr_Variable ev = new Expr_Variable(Line, File);
					ev.Name = name;
					return ev;
				}
			}
			return null;
		}
		private Expr_ValueList GetActualArguments()
		{
			Expr_ValueList vl = new Expr_ValueList(Line, File);
			vl.ValueList = new List<Expression>();
			Accept('(');
			if (!Match(')'))
			{
				vl.ValueList.Add(E());
				while (Match(','))
				{
					Accept(',');
					vl.ValueList.Add(E());
				}
			}
			Accept(')');
			return vl;
		}
		private Expr_Call CallFunction(string name, Expr_ValueList arg)
		{
			return new Expr_Call(Line, File) { Name = name, Arguments = arg };
		}
	}
}
