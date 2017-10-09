using System.Collections.Generic;

namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	public class Nuspec
	{
		public NugetVersion Version { get; set; }
		public string Name { get; set; }
		public IEnumerable<NuspecDependency> Dependencies { get; set; }

		public override string ToString()
		{
			return $"NuSpec: {Name}";
		}
	}
}