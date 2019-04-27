using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Lex
{
	public class LexException : Exception
	{
		public LexException(string m, int line)
			   : base($"({line}): " + m)
		{

		}
	}
}
