using System;
using System.Text;
using System.IO;

namespace R2Script.Lex
{
	public class Tokenizer
	{
		public string Code
		{
			get;
		}
		public int Index
		{
			get;
			private set;
		}
		public int Line
		{
			get;
			private set;
		}
		public string File
		{
			get;
		}
		private char ch;
		private Token token;
		public const int ERR_LEX_INVALID_CHARACTER = -1;
		public const int ERR_LEX_END_OF_FILE = -2;
		public const int ERR_LEX_UNAVAILABLE_ESC = -3;
		public Token Get()
		{
			return token;
		}
		public Tokenizer(string code, string file)
		{
			this.Code = code;
			this.Index = 0;
			this.Line = 1;
			this.File = file;
			Nextc();
		}
		private void Nextc()
		{
			if (Code.Length > Index)
				ch = Code[Index++];
			else
				ch = (char)0;
		}
		private int Get_Token_1(int eq, int n1, int n2)
		{
			int def = ch;
			Nextc();
			if (ch == '=')
			{
				Nextc();
				return eq;
			}
			else if (ch == def)
			{
				Nextc();
				if (ch == '=')
				{
					Nextc();
					return n2;
				}
				return n1;
			}
			return def;
		}
		private int Get_Token_2(int eq)
		{
			int def = ch;
			Nextc();
			if (ch == '=')
			{
				Nextc();
				return eq;
			}
			return def;
		}
		public int Next()
		{
			token = new Token();
			token.File = File;
			#region 处理空白符，Token开头
			{
				bool flag = true;
				while (flag)
				{
					switch (ch)
					{
						case '\n':
							Nextc();
							Line++;
							break;
						case '\r':
						case ' ':
						case '\t':
							Nextc();
							break;
						case '#':
							{
								while (ch == '#')//注释
								{
									Nextc();
									while (ch != -1 && ch != '\n')
										Nextc();
								}
							}
							break;
						default:
							flag = false;
							break;
					}
				}
			}
			#endregion
			#region 处理正常的Token
			{
				while (true)
				{
					if (ch == 0)
					{
						token.Type = (TokenType)(-1);
						return ERR_LEX_END_OF_FILE;
					}
					token.Line = Line;
					switch (ch)
					{
						case '@':
							{
								Nextc();
								StringBuilder sb = new StringBuilder(50);
								while (char.IsLetter(ch) || char.IsDigit(ch) || ch == '_')
								{
									sb.Append(ch);
									Nextc();
								}
								token.Value = sb.ToString();
								switch (token.Value)
								{
									case "import":
										token.Type = TokenType.TK_PRECOMP_IMPORT;
										break;
									case "include":
										token.Type = TokenType.TK_PRECOMP_INCLUDE;
										break;
									default:
										throw new LexException("Unsupported pre-compile label", Line, File);
								}
								return 0;
							}
						case '$':
							{
								Nextc();
								StringBuilder sb = new StringBuilder(1024);
								while (ch != 0 && ch != '$')
								{
									if (ch == '\\')
									{
										Nextc();
										if (ch == '$')
											sb.Append('$');
										else sb.Append("\\" + ch);
									}
									sb.Append(ch);
									Nextc();
								}
								Nextc();
								token.Type = TokenType.TK_SEG_ASM;
								token.Value = sb.ToString();
								return 0;
							}
						case '(':
						case '[':
						case ')':
						case ']':
						case '{':
						case '}':
							token.Type = (TokenType)ch;
							Nextc();
							return 0;

						case '&':
							Nextc();
							if (ch == '&')
							{
								token.Type = TokenType.TK_OP_L_AND;
								Nextc();
								return 0;
							}
							token.Type = (TokenType)'&';
							return 0;
						case '|':
							Nextc();
							if (ch == '|')
							{
								token.Type = TokenType.TK_OP_L_OR;
								Nextc();
								return 0;
							}
							token.Type = (TokenType)'|';
							return 0;
						case ',':
						case '.':
						case '^':
						case ';':
						case ':':
						case '?':
							token.Type = (TokenType)ch;
							Nextc();
							return 0;
						case '<':
							token.Type = (TokenType)Get_Token_1((int)TokenType.TK_OP_LE, (int)TokenType.TK_OP_LSHIFT, (int)TokenType.TK_DE_LSHIFT_EQ);
							return 0;
						case '>':
							token.Type = (TokenType)Get_Token_1((int)TokenType.TK_OP_GE, (int)TokenType.TK_OP_RSHIFT, (int)TokenType.TK_DE_RSHIFT_EQ);
							return 0;
						case '+':
							token.Type = (TokenType)Get_Token_2((int)TokenType.TK_DE_PLUS_EQ);
							return 0;
						case '-':
							token.Type = (TokenType)Get_Token_2((int)TokenType.TK_DE_MINUS_EQ);
							return 0;
						case '*':
							token.Type = (TokenType)Get_Token_2((int)TokenType.TK_DE_MUL_EQ);
							return 0;
						case '/':
							token.Type = (TokenType)Get_Token_2((int)TokenType.TK_DE_DIV_EQ);
							return 0;
						case '=':
							token.Type = (TokenType)Get_Token_2((int)TokenType.TK_OP_EQ);
							return 0;
						case '!':
							Nextc();
							if (ch != '=') return ERR_LEX_INVALID_CHARACTER;
							Nextc();
							token.Type = TokenType.TK_OP_NE;
							return 0;
						case '0':
						case '1':
						case '2':
						case '3':
						case '4':
						case '5':
						case '6':
						case '7':
						case '8':
						case '9':
							{
								StringBuilder sb = new StringBuilder(50);
								char n = ch;
								sb.Append(ch);
								Nextc();
								if (ch == 'x' && n == '0')
								{
									sb.Append(ch);
									Nextc();
									while (ch != 0 && (char.IsDigit(ch) ||
									(ch >= 'a' && ch <= 'f') ||
									(ch >= 'A' && ch <= 'F')))
									{
										sb.Append(ch);
										Nextc();
									}
								}
								else
								{
									while (ch != 0 && char.IsDigit(ch))
									{
										sb.Append(ch);
										Nextc();
										/*if (ch == '.')
										{
											sb.Append('.');
											Nextc();
										}*/
									}
								}
								token.Type = TokenType.TK_NUMBER;
								token.Value = sb.ToString();
								return 0;
							}
						case '\"':
							{
								Nextc();
								StringBuilder sb = new StringBuilder(1024);
								sb.Append("\"");
								while (ch != 0 && ch != '\"')
								{
									char c = ch;
									if (ch == '\\')
									{
										sb.Append(c);
										Nextc();
									}
									sb.Append(c);
									Nextc();
								}
								sb.Append("\"");
								Nextc();//pass '\"'
								token.Type = TokenType.TK_STRING;
								token.Value = sb.ToString();
								return 0;
							}
						default:
							if (char.IsLetter(ch))
							{
								StringBuilder sb = new StringBuilder(50);
								while (ch != 0 && (char.IsLetter(ch) || char.IsDigit(ch) || ch == '_'))
								{
									sb.Append(ch);
									Nextc();
								}
								token.Value = sb.ToString();
								switch (token.Value)
								{
									case "return":
										token.Type = TokenType.TK_KW_RETURN;
										break;
									case "if":
										token.Type = TokenType.TK_KW_IF;
										break;
									case "else":
										token.Type = TokenType.TK_KW_ELSE;
										break;
									case "for":
										token.Type = TokenType.TK_KW_FOR;
										break;
									case "while":
										token.Type = TokenType.TK_KW_WHILE;
										break;
									case "break":
										token.Type = TokenType.TK_KW_BREAK;
										break;
									case "continue":
										token.Type = TokenType.TK_KW_CONTINUE;
										break;
									case "function":
										token.Type = TokenType.TK_KW_FUNCTION;
										break;
									case "naked":
										token.Type = TokenType.TK_KW_NAKED;
										break;
									case "var":
										token.Type = TokenType.TK_KW_VAR;
										break;
									default:
										token.Type = TokenType.TK_NAME;
										break;
								}
								return 0;
							}
							throw new LexException("Invalid character", Line, File);
					}
				}
			}
			#endregion
		}
	}
}

