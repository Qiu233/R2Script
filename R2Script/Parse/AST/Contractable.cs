using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script.Parse.AST
{
	public interface Contractable
	{
		Expression TryContract();
	}
}
