﻿using System.Collections.Generic;

namespace Ce.Labs.BuildTools
{
	public class Nuspec
	{
		public ReferenceVersion Version { get; set; }
		public string Name { get; set; }
		public IEnumerable<NuspecDependency> Dependencies { get; set; }

		public override string ToString()
		{
			return $"NuSpec: {Name}";
		}
	}
}