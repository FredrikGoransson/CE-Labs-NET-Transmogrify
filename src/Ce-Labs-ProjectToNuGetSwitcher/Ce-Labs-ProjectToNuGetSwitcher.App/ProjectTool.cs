using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Ce_Labs_ProjectToNuGetSwitcher.App
{
	public class ProjectTool
	{
		private readonly Regex _parseTargetFrameworkVersion = new Regex(@"v(?<a>\d+)(?>\.(?<b>\d+)(?>\.(?<c>\d+)(?>\.(?<d>\d+)){0,1}){0,1}){0,1}", RegexOptions.Compiled);
		private readonly string _parseTargetFrameworkReplacementString = @"net$1$2$3$4";
		private static readonly Regex _isPackagesHintPath = new Regex(@"\\packages\\.*\\lib\\.*");	

		private readonly string _projectPath;
		private readonly ILogger _logger;
		private readonly XDocument _document;
		private readonly XmlNamespaceManager _namespaceManager;
		private readonly XNamespace _msbNs;
		private const string MsbuildXmlNamespace = @"http://schemas.microsoft.com/developer/msbuild/2003";

		private readonly bool _isCpsDocument = false;

		public bool IsCpsDocument => _isCpsDocument;

		private bool _didUpdateDocument = false;

		public string FilePath => _projectPath;
		public string FolderPath => System.IO.Path.GetDirectoryName(_projectPath);
		public object Name => System.IO.Path.GetFileNameWithoutExtension(_projectPath);

		public ProjectTool(string path, ILogger logger)
		{
			_projectPath = path;
			_logger = logger;

			_document = XDocument.Load(_projectPath);

			var sdkValue = (_document.FirstNode as XElement)?.Attribute("Sdk")?.Value;
			_isCpsDocument = (sdkValue != null);
			if (_isCpsDocument)
			{
				_msbNs = "";
			}
			else
			{
				_msbNs = MsbuildXmlNamespace;
			}



			//_namespaceManager = new XmlNamespaceManager(_document.NameTable);
			//_namespaceManager.AddNamespace("msbld", MsbuildXmlNamespace);

			// //ItemGroup/ProjectReference

			// //ItemGroup/Reference

		}

		public IEnumerable<ProjectReference> GetReferences()
		{
			var itemGroupReferences = _document.Descendants(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"));

			var projectTargetVersion = (_document.Descendants(_msbNs + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "v4.5.1").RemoveFromStart("v");

			foreach (var element in itemGroupReferences)
			{
				
				var fullName = element.Attribute("Include")?.Value;
				var hintPath = element.Attribute("HintPath")?.Value;
				var nameComponents = fullName.Split(',');
				var name = nameComponents[0];
				var version = nameComponents.Length > 1 ? nameComponents[1]?.RemoveFromStart(" Version=") : projectTargetVersion;
				var projectReference = new ProjectReference()
				{
					Name = name, 
					FullName = fullName,
					HintPath = hintPath,
					Version = new ReferenceVersion(version),
				};

				yield return projectReference;
			}
		}

		public IEnumerable<NugetPackage> GetPackageReferences()
		{
			if (_isCpsDocument)
			{

				var packageReferences = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "PackageReference"));

				return packageReferences.Select(element =>
				{
					var name = element.Attribute("Include")?.Value;
					var version = element.Attribute("Version")?.Value;
					var projectReference = new NugetPackage()
					{
						Name = name,
						Version = version,
					};

					return projectReference;

				}).ToArray();				
			}
			else
			{
				var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(FolderPath, "packages.config");

				if (System.IO.File.Exists(nugetPackagesConfigPath))
				{

					var nugetTool = new NugetTool(nugetPackagesConfigPath);

					return nugetTool.GetNugetPackages();
				}
			}

			return new NugetPackage[0];
		}

		public void Save()
		{
			if (!_didUpdateDocument) return;

			var xws = new XmlWriterSettings
			{
				OmitXmlDeclaration = _isCpsDocument,
				Indent = true
			};
			using (var writer = XmlWriter.Create(_projectPath, xws))
			{
				_document.Save(writer);
			}
		}

		public void AddProjectReference(string name, string path, Guid projectId)
		{
			if (_isCpsDocument)
			{
				var itemGroups = _document.Element("Project")?.Elements(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"));
				XElement parentNode = null;
				if (itemGroups.Any())
				{
					var existingProjectReference = itemGroups.First();
					parentNode = existingProjectReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					_document.Element(_msbNs + "Project").Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingProjectReferenceXElement = _document
					.Element("Project")?
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("ProjectReference"))
					.FirstOrDefault(e => e?.Attribute("Include")?.Value == path);
				if (existingProjectReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "ProjectReference",
						new XAttribute("Include", path)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}

			}
			else
			{
				var itemGroups = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"));
				XElement parentNode = null;
				if (itemGroups.Any())
				{
					var existingProjectReference = itemGroups.First();
					parentNode = existingProjectReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					_document.Element(_msbNs + "Project").Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingProjectReferenceXElement = _document
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("ProjectReference"))
					.Select(e => e.Element("Project"))
					.FirstOrDefault(e => e?.Value == name);
				if (existingProjectReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "ProjectReference",
						new XAttribute("Include", path),
						new XElement(_msbNs + "Project", $"{{{projectId}}}"),
						new XElement(_msbNs + "Name", name)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}
			}
		}

		public void RemoveReference(string name)
		{
			var referenceElement = _document.Descendants(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"))
				.FirstOrDefault(element => element.Attribute("Include")?.Value.Split(',').FirstOrDefault() == name);

			referenceElement?.Remove();

			_didUpdateDocument = true;
		}

		public void RemovePackageReference(string name)
		{
			if (_isCpsDocument)
			{
				var referenceElement = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "PackageReference"))
					.FirstOrDefault(element => element.Attribute("Include")?.Value == name);

				referenceElement?.Remove();

				_didUpdateDocument = true;
			}
			else
			{
				var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(FolderPath, "packages.config");

				if (System.IO.File.Exists(nugetPackagesConfigPath))
				{

					var nugetTool = new NugetTool(nugetPackagesConfigPath);
					nugetTool.RemovePackage(name);
				}
			}
		}

		public void AddNugetReference(string packageName, string packageVersion, string targetVersion)
		{
			if (_isCpsDocument)
			{
				var projectElement = _document.Element("Project");
				if (projectElement == null)
					throw new NotSupportedException("Expected project file to have a <Project... /> element");

				var packageReferenceElements = projectElement.Elements(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "PackageReference"));
				XElement parentNode = null;
				if (packageReferenceElements.Any())
				{
					var existingPackageReference = packageReferenceElements.First();
					parentNode = existingPackageReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					projectElement.Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingProjectReferenceXElement = projectElement
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("ProjectReference"))
					.FirstOrDefault(e => e?.Attribute("Include")?.Value == packageName);
				if (existingProjectReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "PackageReference",
						new XAttribute("Include", packageName),
						new XAttribute("Version", packageVersion)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}
				else
				{
					var versionAttribute = existingProjectReferenceXElement.Attribute("Version");

					if (versionAttribute == null)
					{
						existingProjectReferenceXElement.Add(new XAttribute("Version", packageVersion));
					}
					else if (versionAttribute.Value != packageVersion)
					{
						versionAttribute.Value = packageVersion;
					}
				}
			}
			else
			{
				var nugetPackagesConfigPath = PathExtensions.GetAbsolutePath(FolderPath, "packages.config");

				if (System.IO.File.Exists(nugetPackagesConfigPath))
				{

					var nugetTool = new NugetTool(nugetPackagesConfigPath);
					nugetTool.AddPackage(packageName, packageVersion, targetVersion);
				}
			}
		}

		public IEnumerable<ProjectReference> GetProjectReferences()
		{
			if (_isCpsDocument)
			{
				var projectElement = _document.Element("Project");
				if (projectElement == null)
					throw new NotSupportedException("Project document is missing the Project element");

				var projectReferences = projectElement.Elements(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"))
					.Select(e => new ProjectReference()
					{
						Name = System.IO.Path.GetFileNameWithoutExtension(e?.Attribute("Include")?.Value),
						HintPath = e?.Attribute("Include")?.Value,
					});
				return projectReferences;
			}
			else
			{
				var projectReferences = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "ProjectReference"))
					.Select(e => new ProjectReference()
					{						
						Name = e?.Element(_msbNs + "Name")?.Value,
						HintPath = e?.Attribute("Include")?.Value,
					});
				return projectReferences;
			}
		}

		public void RemoveProjectReference(ProjectReference projectReference)
		{
			var referenceElement = _document.Descendants(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "ProjectReference"))
				.FirstOrDefault(element => element.Attribute("Include")?.Value == projectReference.HintPath);

			referenceElement?.Remove();

			_didUpdateDocument = true;
		}

		public string GetTargetFramework()
		{
			var targetFrameworkElement = "net45";
			if (_isCpsDocument)
			{
				targetFrameworkElement = _document.Element(_msbNs + "Project")?
					                         .Elements(_msbNs + "PropertyGroup")
					                         .Elements(_msbNs + "TargetFrameworkVersion")
					                         .FirstOrDefault()?.Value ?? targetFrameworkElement;
				
			}
			else
			{
				targetFrameworkElement = _document.Element(_msbNs + "Project")?
					                         .Elements(_msbNs + "PropertyGroup")
					                         .Elements(_msbNs + "TargetFrameworkVersion")
					                         .FirstOrDefault()?.Value ?? targetFrameworkElement;
			}

			targetFrameworkElement = _parseTargetFrameworkVersion.Replace(targetFrameworkElement, _parseTargetFrameworkReplacementString);
			return targetFrameworkElement;
		}

		public void AddReference(string referencePath)
		{
			if (!_isCpsDocument)
			{
				var referenceInfo = Assembly.ReflectionOnlyLoadFrom(referencePath);
				var include = $"{referenceInfo.GetName().ToString()}, processorArchitecture={referenceInfo.GetName().ProcessorArchitecture.ToString().ToUpper()}";
				var hintPath = PathExtensions.MakeRelativePath(FolderPath, referencePath);

				var referenceElements = _document.Descendants(_msbNs + "ItemGroup")
					.SelectMany(element => element.Elements(_msbNs + "Reference"));
				XElement parentNode = null;
				if (referenceElements.Any())
				{
					var existingProjectReference = referenceElements.First();
					parentNode = existingProjectReference.Parent;
				}
				else
				{
					var itemGroupElement = new XElement(_msbNs + "ItemGroup");
					_document.Element(_msbNs + "Project").Add(itemGroupElement);
					parentNode = itemGroupElement;
				}

				var existingReferenceXElement = _document
					.Elements("ItemGroup")
					.SelectMany(e => e.Elements("Reference"))
					.FirstOrDefault(e => e.Attribute("Include")?.Value == include);
				if (existingReferenceXElement == null)
				{
					var projectReferenceXElement = new XElement(_msbNs + "Reference",
						new XAttribute("Include", include),
						new XElement(_msbNs + "HintPath", hintPath)
					);
					parentNode.Add(projectReferenceXElement);

					_didUpdateDocument = true;
				}
			}
		}


		public void CleanUpProject()
		{
			if (!_isCpsDocument)
			{
				CleanUpClassicProject();
			}
			else
			{
				CleanUpCPSProject();
			}
		}

		private void CleanUpCPSProject()
		{

		}

		private static bool IsHintPathPackageReference(string hintPath)
		{
			if (hintPath == null) return false;
			var match = _isPackagesHintPath.Match(hintPath);
			return match.Success;
		}

		private string GetPackagesFolderPath()
		{
			var packagesFolderPath = (string)null;
			var folder = _projectPath;
			while (packagesFolderPath == null)
			{
				folder = System.IO.Directory.GetParent(folder)?.FullName;
				if (folder == null) return null;
				var packagesFolders = System.IO.Directory.GetDirectories(folder, "packages");
				packagesFolderPath = packagesFolders.FirstOrDefault();
			}
			return packagesFolderPath;
		}

		private void CleanUpClassicProject()
		{
			var projectNode = _document.Element(_msbNs + "Project");
			if (projectNode == null) throw new ApplicationException($"Invalid Project xml, could not find Project node");

			var projectTargetVersion = (projectNode.Elements(_msbNs + "TargetFrameworkVersion").FirstOrDefault()?.Value ?? "v4.5.1").RemoveFromStart("v");

			var itemGroupReferenceNodes = projectNode.Elements(_msbNs + "ItemGroup")
				.SelectMany(element => element.Elements(_msbNs + "Reference"))
				.ToArray();

			var references = new List<ProjectReference>();// GetReferences().OrderBy(r => r).ToArray();
			foreach (var referenceNode in itemGroupReferenceNodes)
			{
				var fullName = referenceNode.Attribute("Include")?.Value;
				var hintPath = referenceNode.Element(_msbNs + "HintPath")?.Value;
				var nameComponents = fullName.Split(',');
				var name = nameComponents[0];
				var version = nameComponents.Length > 1 ? nameComponents[1]?.RemoveFromStart(" Version=") : projectTargetVersion;
				var isPrivate = referenceNode.Element(_msbNs + "Private")?.Value;
				var isPackageReference = IsHintPathPackageReference(hintPath);

				var projectReference = new ProjectReference()
				{
					Name = name,
					FullName = fullName,
					HintPath = hintPath,
					Version = new ReferenceVersion(version),
					Private = (isPrivate == "True"),
					IsPackageReference = isPackageReference,
					PackageVersion = isPackageReference ? new ReferenceVersion(hintPath) : null,
				};

				var existingReference = references.FirstOrDefault(r => r.Name == projectReference.Name);
				if (existingReference != null)
				{
					var comparison = projectReference.CompareTo(existingReference);
					if (comparison == 0)
					{
						// Same, ignore
						_logger.LogMessage(
							$"\tFound duplicate identical references of {existingReference.Name} {existingReference.Version} {existingReference.HintPath}");
					}
					else if (comparison > 0)
					{
						// Newer version, remove the existing
						_logger.LogMessage(
							$"\tFound duplicate references of {existingReference.Name} {existingReference.Version} {existingReference.HintPath}");
						references.Remove(existingReference);
						references.Add(projectReference);
					}
					else
					{
						// Newer version, dont add this
						_logger.LogMessage(
							$"\tFound duplicate references of {existingReference.Name} {existingReference.Version} {existingReference.HintPath}");
					}
				}
				else
				{
					references.Add(projectReference);
				}

				if (projectReference.IsPackageReference)
				{
					if (projectReference.PackageVersion.CompareReleaseVersion(projectReference.Version) != 0)
					{
						var packagesFolder = GetPackagesFolderPath();
						var possibleNugetPackagePaths = NugetPackageTool.FindNugetPackages(packagesFolder, projectReference.Name);

						if (possibleNugetPackagePaths.All(packagePath => !hintPath.StartsWith(PathExtensions.MakeRelativePath(FolderPath, packagePath))))
						{
							_logger.LogMessage(
								$"\tFound suspicious hintpath for {projectReference.Name} {projectReference.Version} {projectReference.HintPath}");
						}
					}
				}

				referenceNode.Remove();
			}


			var gacReferencesItemGroupElement = new XElement(_msbNs + "ItemGroup");
			XElement insertAfterElement = null;
			insertAfterElement = projectNode.Elements(_msbNs + "PropertyGroup").LastOrDefault();

			if (insertAfterElement == null)
			{
				projectNode.Add(gacReferencesItemGroupElement);
			}
			else
			{
				insertAfterElement.AddAfterSelf(gacReferencesItemGroupElement);
			}

			references.Sort((a, b) => a.CompareTo(b));

			foreach (var reference in references.Where(r => r.HintPath == null))
			{
				var children = new List<object>() { new XAttribute("Include", reference.FullName) };
				if (reference.HintPath != null) { children.Add(new XElement(_msbNs + "HintPath", reference.HintPath)); }
				if (reference.Private) { children.Add(new XElement(_msbNs + "Private", "True")); }
				var projectReferenceXElement = new XElement(_msbNs + "Reference", children);
				gacReferencesItemGroupElement.Add(projectReferenceXElement);
			}

			insertAfterElement = gacReferencesItemGroupElement;


			var referencesItemGroupElement = new XElement(_msbNs + "ItemGroup");
			insertAfterElement.AddAfterSelf(referencesItemGroupElement);

			references.Sort((a, b) => a.CompareTo(b));

			foreach (var reference in references.Where(r => r.HintPath != null))
			{
				var children = new List<object>() {new XAttribute("Include", reference.FullName)};
				if (reference.HintPath != null) { children.Add(new XElement(_msbNs + "HintPath", reference.HintPath)); }
				if (reference.Private) { children.Add(new XElement(_msbNs + "Private", "True")); }
				var projectReferenceXElement = new XElement(_msbNs + "Reference", children );
				referencesItemGroupElement.Add(projectReferenceXElement);
			}



			// Sort ProjectReferences
			var projectReferenceChildNodes = projectNode.Elements(_msbNs + "ItemGroup").Elements()
				.Where(element => element.Name.LocalName == "ProjectReference")
				.Select(element => new { Element = element, File = element.Attribute("Include")?.Value })
				.Where(element => element.File != null)
				.ToArray();

			foreach (var itemGroupChildNode in projectReferenceChildNodes)
			{
				itemGroupChildNode.Element.Remove();
			}

			var projectReferenceItemGroupElement = new XElement(_msbNs + "ItemGroup");
			referencesItemGroupElement.AddBeforeSelf(projectReferenceItemGroupElement);

			foreach (var itemGroupChildNode in projectReferenceChildNodes.OrderBy(element => element.File))
			{
				projectReferenceItemGroupElement.Add(itemGroupChildNode.Element);
			}



			// Sort Includes
			var itemGroupChildNodes = projectNode.Elements(_msbNs + "ItemGroup").Elements()
				.Where(element => (element.Name.LocalName != "Reference") && (element.Name.LocalName != "ProjectReference"))
				.Select(element => new { Element = element, File = element.Attribute("Include")?.Value})
				.Where(element => element.File != null)
				.ToArray();

			foreach (var itemGroupChildNode in itemGroupChildNodes)
			{
				itemGroupChildNode.Element.Remove();
			}

			var includesItemGroupElement = new XElement(_msbNs + "ItemGroup");
			referencesItemGroupElement.AddAfterSelf(includesItemGroupElement);

			foreach (var itemGroupChildNode in itemGroupChildNodes.OrderBy(element => element.File))
			{
				includesItemGroupElement.Add(itemGroupChildNode.Element);
			}


			// Remove empty ItemGroup nodes
			var itemGroupNodes = _document.Descendants(_msbNs + "ItemGroup").ToArray();
			foreach (var itemGroupNode in itemGroupNodes)
			{
				if (!itemGroupNode.HasElements)
				{
					itemGroupNode.Remove();
				}
			}
			_didUpdateDocument = true;
		}
	}
}