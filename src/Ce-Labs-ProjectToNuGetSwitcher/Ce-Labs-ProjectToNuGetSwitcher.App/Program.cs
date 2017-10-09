using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;

namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	internal class ConsoleOptions
	{
		[Option('f', "folder", Required = true,
			HelpText = "Mix-in folder with projects to replace NuGet-references")]
		public string Folder { get; set; }

		[Option('v', "verbose", DefaultValue = false,
			HelpText = "Prints all messages to standard output.")]
		public bool Verbose { get; set; }

		[Option('s', "solution", Required = true,
			HelpText = "Solution file to upgrade")]
		public string SolutionFile { get; set; }

		[Option('o', "operation", DefaultValue = "proj",
			HelpText = "Operation to perform, o=proj replaces nugets with projects, o=nuget replaces projects with nugets")]
		public string Operation { get; set; }

		[Option('w', "wait", DefaultValue = true,
			HelpText = "Waits for input after finished operation")]
		public bool Wait { get; set; }

		[Option('d', "debug", DefaultValue = false,
			HelpText = "Breaks for debugging")]
		public bool Debug { get; set; }
	}

	class Program
	{
		private static bool _verbose = false;
		private static bool _waitForExit = false;

		private static void LogMessage(string message)
		{
			Console.WriteLine(message);
		}

		private static void LogProgress()
		{
			if (!_verbose)
			{
				Console.Write(".");
			}
		}

		private static void LogInformation(String message)
		{
			if (_verbose)
			{
				Console.WriteLine(message);
			}
		}

		static void Main(string[] args)
		{
			if (args == null || args.Length == 0)
			{
				LogMessage("Enter arguments for program:");
				var line = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(line)) line = "-o -s -g";
				args = line.Split(' ');
			}

			var parsedOptions = new ConsoleOptions();
			Parser.Default.ParseArguments(args, parsedOptions);

			if (parsedOptions.Debug)
			{
				foreach (var arg in args)
				{
					Console.WriteLine(arg);
				}
				Console.ReadKey();
			}

			_verbose = parsedOptions.Verbose;
			_waitForExit = parsedOptions.Wait;

			var solutionName = parsedOptions.SolutionFile;

			if (solutionName == null)
			{
				Exit($"Enter the path to a solution file, s=c:\\code\\mysolution.sln");
				return;
			}
			if (!System.IO.File.Exists(solutionName))
			{
				Exit($"Solution file {solutionName} does not exist");
				return;
			}

			var solutionTool = new SolutionTool(solutionName);
			var folderPath = parsedOptions.Folder;
			if (folderPath == null)
			{
				Exit($"Enter the path to a folder with projects in, f=\"c:\\code\\projects\"");
				return;
			}
			if (!System.IO.Directory.Exists(folderPath))
			{
				Exit($"Folder {folderPath} that should contain projects, does not exist, f=\"c:\\code\\projects\"");
				return;
			}

			if (parsedOptions.Operation.Equals("proj", StringComparison.InvariantCultureIgnoreCase))
			{
				LogMessage($"Transmogrifying NuGet references to project references");

				var folderTool = new FolderTool(folderPath);
				var transmogrificationTool = new TransmogrificationTool(new ConsoleLogger(_verbose));
				transmogrificationTool.TransmogrifyNugetPackagesToProjects(solutionTool, folderTool);
				Exit();
				return;
			}
			else if (parsedOptions.Operation.Equals("nuget", StringComparison.InvariantCultureIgnoreCase))
			{
				LogMessage($"Transmogrifying project references to NuGet references");

				var folderTool = new FolderTool(folderPath);
				var transmogrificationTool = new TransmogrificationTool(new ConsoleLogger(_verbose));
				transmogrificationTool.ReTransmogrifyProjectsToNugetPackages(solutionTool, folderTool);
				Exit();
				return;
			}

			Exit($"Operation {parsedOptions.Operation} is not supported, o=proj replaces nugets with projects, o=nuget replaces projects with nugets");
			return;
		}

		private static void Exit(string message = @"Done")
		{
			LogMessage(message);
			if (_waitForExit)
			{
				Console.ReadKey();
			}
		}

		private static void PrintPossibleTargets(SolutionTool solutionTool, FolderTool folderTool)
		{
			var folderProjectItems = folderTool.GetProjects();

			foreach (var folderProjectItem in folderProjectItems)
			{
				var path = PathExtensions.MakeRelativePath(solutionTool.FolderPath, folderProjectItem.Path);
				LogInformation($"{folderProjectItem.Name} - {path}");
			}
		}

		private static void ExamineTransmogrificationCandidates(TransmogrificationTool transmogrificationTool, SolutionTool solutionTool,  FolderTool folderTool)
		{
			var matchingTargets = TransmogrificationTool.GetMatchingTargets(solutionTool, folderTool);			

			foreach (var matchingProject in matchingTargets)
			{
				LogInformation($"\tCould include {matchingProject.Name}");
			}
		}

		private static void PrintProjectsBySolutionType(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			var projectTypes = projects.GroupBy(p => p.Type);

			foreach (var projectType in projectTypes)
			{
				LogInformation($"Projecte type {projectType.Key} - {solutionTool.GetProjectTypeName(projectType.Key)}");
				foreach (var solutionProjectItem in projectType.OrderBy(p => p.Path))
				{
					LogInformation($"\t{solutionProjectItem.Id}\t{solutionProjectItem.Name}");
					var solutionConfigurations = solutionProjectItem.Configurations.GroupBy(c => c.Solutionconfig);
					foreach (var solutionConfiguration in solutionConfigurations)
					{
						LogInformation($"\t\tConfiguration {solutionConfiguration.Key}");
						foreach (var projectConfiguration in solutionConfiguration)
						{
							LogInformation($"\t\t\t{projectConfiguration.ConfigItem} = {projectConfiguration.ProjectConfig}");
						}

					}
				}
			}
		}

		private static void PrintProjectByHierarchy(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			PrintProjectByHierarchy(solutionTool, projects);
		}

		private static void PrintProjectByHierarchy(SolutionTool solutionTool, IEnumerable<SolutionProjectItem> projects, string indent ="")
		{
			foreach (var project in projects)
			{
				LogInformation($"{indent}{project.Id} [{solutionTool.GetProjectTypeName(project.Type)}]\t{project.Name}");

				PrintProjectByHierarchy(solutionTool, project.ChildProjects, $"{indent}\t");
			}
		}

		private static void PrintProjectsWithReferencesThatCouldBeChanged(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			PrintProjectsWithReferencesThatCouldBeChanged(solutionTool, projects);
		}

		private static void PrintProjectsWithReferencesThatCouldBeChanged(SolutionTool solutionTool, IEnumerable<SolutionProjectItem> projects, string indent = "")
		{
			var projectsLookup = projects.Where(p => solutionTool.CompilableProjects.Contains(p.Type)).ToDictionary(p => p.Name, p => p);
			foreach (var project in projects)
			{
				LogInformation($"{indent}{project.Id} [{solutionTool.GetProjectTypeName(project.Type)}]\t{project.Name}");

				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath);
					foreach (var reference in projectTool.GetReferences())
					{
						if (projectsLookup.ContainsKey(reference.Name))
						{
							var matchingProject = projectsLookup[reference.Name];
							

							var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, "packages.config");

							if (System.IO.File.Exists(nugetPackagesConfigPath))
							{
								var nugetTool = new NugetTool(nugetPackagesConfigPath);

								var packages = nugetTool.GetNugetPackages();

								var referencePackage = packages.FirstOrDefault(pkg => pkg.Name == reference.Name);

								if (referencePackage != null)
								{
									LogInformation($"{indent}\t[NUGET] {reference.Name} -> {matchingProject.Path}");
								}
							}
						}
					}
				}
			}
		}
	}
}
