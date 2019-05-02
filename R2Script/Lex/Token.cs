using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Lex
{
	public struct Token
	{
		public TokenType Type
		{
			get;
			set;
		}
		public string Value
		{
			get;
			set;
		}
		public int Line
		{
			get;
			set;
		}
		public string File
		{
			get;
			set;
		}
		public Token(TokenType type, int line, string file, string value = "")
		{
			this.Type = type;
			this.Line = line;
			this.Value = value;
			this.File = file;
		}
	}
}
