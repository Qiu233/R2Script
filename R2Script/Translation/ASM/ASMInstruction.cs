using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation.ASM
{
	public class ASMInstruction : ASMCode
	{
		public bool Indented
		{
			get;
		}
		public string Code
		{
			get;
		}
		private ASMInstruction(string code, bool indented)
		{
			Code = code;
			Indented = indented;
		}
		public static ASMInstruction Create(string code, bool indented = true)
		{
			return new ASMInstruction(code, indented);
		}

		public override string GetCode()
		{
			if (Indented)
				return "\t" + Code;
			else return Code;
		}

		public static explicit operator ASMInstruction(string s)
		{
			return Create(s);
		}
	}
}
