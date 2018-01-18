using System;

namespace Ce.Labs.BuildTools
{
	public class SolutionProjectConfiguration
	{
		public Guid Id { get; set; }
		public string ProjectConfig { get; set; }
		public string ConfigItem { get; set; }
		public string Solutionconfig { get; set; }
	}
}