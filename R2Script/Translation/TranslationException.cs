using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation
{
	public class TranslationException : Exception
	{
		public TranslationException(string m, int line)
			: base($"({line}): " + m)
		{

		}
	}
}
