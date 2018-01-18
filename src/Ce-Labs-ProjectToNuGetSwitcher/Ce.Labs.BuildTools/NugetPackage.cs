namespace Ce.Labs.BuildTools
{
	public class NugetPackage
	{
		public string Name { get; set; }
		public string Version { get; set; }
		public string TargetFramework { get; set; }
		public string Path { get; set; }

		public override string ToString()
		{
			return $"NuGet Package: {Name}.{Version}";
		}
	}
}