using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse
{
	public abstract class Symbol
	{
		public int Line
		{
			get;
		}
		public Symbol(int line)
		{
			Line = line;
		}
	}
	public class SymbolIden : Symbol
	{
		public string Name
		{
			get;
		}

		public SymbolIden(string name, int line) : base(line)
		{
			Name = name;
		}
	}
	public class SymbolTable : Symbol
	{

		public List<Symbol> Symbols
		{
			get;
		}
		public SymbolTable(int line) : base(line)
		{
			this.Symbols = new List<Symbol>();
		}
		public bool Contain(string s)
		{
			return Symbols.Where(
				t => (t is SymbolIden && 
				(t as SymbolIden).Name == s)).Count() > 0;
		}
		public void Add(Symbol s)
		{
			this.Symbols.Add(s);
		}
		public void Remove(Symbol s)
		{
			this.Symbols.Remove(s);
		}
	}
}
