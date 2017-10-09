namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	public class ProjectReference {

		public string Name { get; set; }
		public string FullName { get; set; }
		public string HintPath { get; set; }

		public override string ToString()
		{
			return $"Project reference: {Name} {HintPath}";
		}
	}
}