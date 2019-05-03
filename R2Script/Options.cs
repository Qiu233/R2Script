using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script
{
	public class Options
	{
		[Option('i', "input", MetaValue = "FILEs", Required = true, HelpText = "Input file", Min = 1)]
		public IEnumerable<string> InputFiles
		{
			get; set;
		}
		[Option('o', "output", MetaValue = "FILE", Required = true, HelpText = "Output file")]
		public string OutputFile
		{
			get; set;
		}
		[Option('l', "libpath", MetaValue = "FILEs", Required = false, HelpText = "Path to search lib")]
		public IEnumerable<string> LibPaths
		{
			get; set;
		}
	}
}
