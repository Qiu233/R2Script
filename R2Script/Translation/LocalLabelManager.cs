using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Translation
{
	public class LocalLabelManager
	{
		public int Count
		{
			get;
			private set;
		}
		public string RootName
		{
			get;
		}
		public LocalLabelManager(string rootName)
		{
			RootName = rootName;
			Count = 0;
		}
		public string GetNew()
		{
			return $".{RootName}_{Count++}";
		}
	}
}
