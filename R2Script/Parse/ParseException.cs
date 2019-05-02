using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse
{
	public class ParseException : Exception
	{
		public ParseException(string m, int line, string file)
			: base($"[{file}]({line}): " + m)
		{

		}
	}
}
