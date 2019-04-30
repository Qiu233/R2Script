using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation
{
	public class OffsetTable
	{
		public Dictionary<string, int> Offsets
		{
			get;
		}
		public int Count => Offsets.Count;
		public int OffsetBase
		{
			get;
		}
		public OffsetTable(int offsetBase)
		{
			this.Offsets = new Dictionary<string, int>();
			this.OffsetBase = offsetBase;
		}
		public void Add(string name, int offset)
		{
			Offsets.Add(name, offset);
		}
		public void Remove(string name)
		{
			Offsets.Remove(name);
		}
	}
}
