using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R2Script
{
	public class Options
	{
		[Option('i', "input", MetaValue = "FILE", Required = true, HelpText = "Input file needed", Min = 1)]
		public IEnumerable<string> InputFiles
		{
			get; set;
		}
		[Option('o', "output", MetaValue = "FILE", Required = true, HelpText = "Output file needed")]
		public string OutputFile
		{
			get; set;
		}
	}
}
