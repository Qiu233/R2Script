using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation
{
	public class GlobalNameManager
	{
		public List<string> GlobalNames
		{
			get;
		}
		public Translator Translator
		{
			get;
		}

		private GlobalNameManager(Translator translator)
		{
			Translator = translator;
			GlobalNames = new List<string>();
		}

		public static GlobalNameManager Create(Translator translator)
		{
			return new GlobalNameManager(translator);
		}

		public void Add(string s)
		{
			GlobalNames.Add(s);
		}

		public static string GetGlobalName(string n)
		{
			return $"_rs_{n}";
		}

	}
}
