namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	public class NuspecDependency
	{
		public string Name { get; set; }
		public string Version { get; set; }		

		public override string ToString()
		{
			return $"NuSpec dependency: {Name}.{Version}";
		}
	}
}