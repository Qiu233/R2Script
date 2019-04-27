using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation.ASM
{
	public class ASMSnippet : ASMCode
	{
		public List<ASMCode> Instructions
		{
			get;
		}
		private ASMSnippet()
		{
			Instructions = new List<ASMCode>();
		}

		public static ASMSnippet FromEmpty()
		{
			return new ASMSnippet();
		}

		public static ASMSnippet FromASMCode(string asm)
		{
			ASMSnippet s = new ASMSnippet();

			ASMInstruction[] ss = asm.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(t => ASMInstruction.Create(t)).ToArray();
			s.Instructions.AddRange(ss);
			return s;
		}

		public static ASMSnippet FromCode(IEnumerable<ASMCode> code)
		{
			ASMSnippet s = new ASMSnippet();
			s.Instructions.AddRange(code);
			return s;
		}

		public override string GetCode()
		{
			StringBuilder sb = new StringBuilder();
			Instructions.ForEach(s =>
			{
				if (s is ASMSnippet)
					sb.Append(s.GetCode());
				else
					sb.AppendLine(s.GetCode());
			});
			return sb.ToString();
		}

	}
}
