using System.Collections.Generic;
using System.Linq;

namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	public class CleanupTool
	{
		private readonly ILogger _logger;

		public CleanupTool(ILogger logger)
		{
			_logger = logger;
		}

		public void CleanUpReferencesInProjectFile(SolutionTool solutionTool)
		{
			var projects = solutionTool.GetProjectsWithParents().ToArray();
			foreach (var project in projects)
			{
				if (solutionTool.CompilableProjects.Contains(project.Type))
				{
					var projectPath = PathExtensions.GetAbsolutePath(solutionTool.FolderPath, project.Path);

					var projectTool = new ProjectTool(projectPath, _logger);
					projectTool.CleanUpProject();
					_logger.LogMessage($"Cleaned {projectTool.Name}");
					projectTool.Save();
				}
			}
		}
	}
}