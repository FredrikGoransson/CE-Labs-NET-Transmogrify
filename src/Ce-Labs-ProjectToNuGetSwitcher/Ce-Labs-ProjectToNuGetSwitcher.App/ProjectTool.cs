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

		private readonly string _projectPath;
		private readonly XDocument _document;
		private readonly XmlNamespaceManager _namespaceManager;
		private readonly XNamespace _msbNs;
		private const string MsbuildXmlNamespace = @"http://schemas.microsoft.com/developer/msbuild/2003";

		private readonly bool _isCpsDocument = false;

		public bool IsCpsDocument => _isCpsDocument;

		private bool _didUpdateDocument = false;

		public string FilePath => _projectPath;
		public string FolderPath => System.IO.Path.GetDirectoryName(_projectPath);

		public ProjectTool(string path)
		{
			_projectPath = path;

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

			//var nodes = _document.SelectNodes(@"//msbld:ItemGroup/msbld:Reference", _namespaceManager);

			foreach (var element in itemGroupReferences)
			{
				
				var fullName = element.Attribute("Include")?.Value;
				var hintPath = element.Attribute("HintPath")?.Value;
				var name = fullName.Split(',').FirstOrDefault();
				var projectReference = new ProjectReference()
				{
					Name = name, 
					FullName = fullName,
					HintPath = hintPath,
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
	}
}