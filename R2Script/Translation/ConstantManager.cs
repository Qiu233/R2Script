using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation
{
	public class ConstantManager
	{
		public abstract class Constant
		{
			public int Size
			{
				get;
			}

			public Constant(int size)
			{
				Size = size;
			}
		}
		public class ConstantString : Constant
		{
			public string Content
			{
				get;
			}
			public ConstantString(string str) : base(str.Length)
			{
				Content = str;
			}
		}
		public class ConstantArray : Constant
		{
			private List<string> Content
			{
				get;
			}
			public string this[int i]
			{
				get => Content[i];
			}
			public ConstantArray(List<string> values) : base(values.Count)
			{
				Content = new List<string>();
				Content.AddRange(values);
			}
		}
		public Translator Translator
		{
			get;
		}
		public List<Constant> Constants
		{
			get;
		}
		private ConstantManager(Translator translator)
		{
			Translator = translator;
			Constants = new List<Constant>();
		}
		public static ConstantManager Create(Translator translator)
		{
			return new ConstantManager(translator);
		}

		public int AddString(string s)
		{
			int j = Constants.FindIndex(t =>
			{
				if (t is ConstantArray) return false; else return ((ConstantString)t).Content == s;
			});
			if (j < 0)//duplicated
			{
				Constants.Add(new ConstantString(s));
				return Constants.Count - 1;
			}
			else
				return j;
		}
		public int AddArray(List<string> values)
		{
			Constants.Add(new ConstantArray(values));
			return Constants.Count - 1;
		}
		public static string GetConstant(int i)
		{
			return $"_rs_data_{i}";
		}

	}
}
