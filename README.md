# Transmogrify
Transmogrifies NuGet references to project references, and back

If you have the source for assemblies that you have added to your solution as NuGet-packages, Transmogrify allows you to temporarily replace those NuGet-packaged referencs with actual in-solution project references instead. This is obviously for debugging an development purposes. When you are done with debugging/development you can transmogrify back from project references to NuGet-package references instead.

Works with both .NET Framework and .NET Core (CPS) projects.

Transmogrify also comes with a nifty extra feature that, contrary to transmogrification for debugging purposes, is actually very useful for development purposes - it cleans up project files by sorting the content in them. Why is this useful? Well, by always keeping the same order for things in the project file, merges and diffs becomes much easier to understand. By running Transmogri-Sort™ before any commit, the order (or struktur in Swedish) of ItemGroups and children will always be the same, no mather Visual Studio or ReSharpers sloppy changes to your project.

## Usage
Simply run ```transmogrify.exe``` and specify the soltion to transmogrify together with a folder containing projects that you want to replace with (or back from).

Transmogrify makes the following assumptions (and may break if not valid for your project):

* NuGet packages that you want to transmogrify to/from are present in the ```packages``` sub-folder where your solutionfile is located.
  * You have the the required TargetFramework libs etc. in the subfolders of each packages (they _should_ be there but...)
* Projects are either in the "old" MsBuild 2013 format (http://schemas.microsoft.com/developer/msbuild/2003) or in the new [Common Project System format](https://github.com/Microsoft/VSProjectSystem).
* The version of the projects matches the version of the NuGet-packages (or whatever, you can do as you like, but if they differ your code might not work. The intention is that Transmogrify let's you swap and replace without breaking your code.)
* When retruning to NuGet-packages for soltion project references the latest NuGet-package  found in the ```packages``` folder matching the name of the project being replaced will be used. It won't remember what version you had before.
* Dependent NuGet-packages will be added to the project if not explicitly added before. These packages are expected to exist in the ```packages``` folder, Transmogrify will _not_ connect to NuGet.org to download missing packages for you.
* Transmogrify runs completely local, it works with what it get's, it't won't download missing dependencies or check for updates or anything
* Only supports re-adding/removing dll-references to projects as part of NuGet add/remove. If NuGet-package installation does something else (add files, build targets, etc) that is not supported by Transmogrify.
* Projects are named same as their csproj-files, i.e. c:\code\mysolution\projX\projX.csproj is a project named projX.
* NuGet package version uses the following standard for version numbers Major(.Minor(.Revision(.Build)))(-prerelease), e.g. ```1.0```, ```1.2.3```, ```1.4.5.72634```, ```1.3.0-beta```, ```1.5.6.934-rtm```
* Transmogrify is idempotent, i.e. you can transmogrify however many times you like with the same parameters with the same result

### NuGet-packages to project references
To *Transmogrify* from NuGet-package references to Project references the following operations are done:
* All projects in the specified folder are scanned, let's call these the mix-in projects
* All NuGet-references in projects in the soltution specified are scanned
* Mix-in projects where there is a matching *name* with a NuGet-package reference are added to the soltion in a solution folder structure that matches the sub path where each can be found.
  * Sub path is built up as the relative path from the current solution folder, e.g. if your mix-in projects are located at ```c:\code\libs\ServiceFabric``` and your solution is located at ```c:\code\mysolution``` then the mix-in projects will be in a solution folder like: ```libs``` / ```ServiceFabric```
* For each project with a NuGet-package reference that now matches a mix-in project in the soltuon
  * Each dependency in the NuSpec file for that NuGet-package is added to the project if not already added to it
    * All *.dll files in the package lib folder are added to the project (if MsBuild2013)
    * packages.config for MsBuild2013 is updated, and a package reference is added for CPS, with a reference to the dependent NuGet-package
  * NuGet-package reference is removed
  * Project reference is added

Usage: ```transmogrify.exe -s c:\code\mysolution\mysolution.sln -f c:\code\libs\ServiceFabric\ -o proj```

### Project references to NuGet-packages
To *Transmogrify* (back) to NuGet-package references from Project references the following operations are done:
* All projects in the specified folder are scanned, let's call these the mix-in projects
* For each project with a project reference to a mix-in project in the soltuon
  * Find the matching NuGet-package in the ```packages``` folder where the name matches the name of the project referenced. If multiple pakcages matches the latest version is selected.
  * Add the NuGet-package name to either packages.config or as a ```<PackageReference .../>``` for CPS projects
  * For MsBuild20136 all dll-files in the NuGet package's ```\lib``` folder are added to the project as references with a relative ```HintPath``` pointing to that file
  * Project reference to mix.in project is removed
* All mix-in projects are removed from the solution.
  * Solution folders are not removed

Usage: ```transmogrify.exe -s c:\code\mysolution\mysolution.sln -f c:\code\libs\ServiceFabric\ -o nuget```

### Transmogri-Sort™ project contents
This will update your projects to contain at most 5 ItemGroup elements, always in the same order.
* All GAC-references, i.e. any assembly reference without a HintPath
* All project references to projects within the same solution
* All dll/exe references with a HintPath, including NuGet package references
* All file includes/removes
* Other ItemGroups, if any such exists, they will be left as-is at the end

All ItemGroup children are sorted by first Name of the reference/file, then version of the reference and third the package version, including prerelease information if it is a NuGet-package reference (i.e. the HintPath points to somewhere under ```..\packages\``` 

## Command
```transmogrify.exe -s (solution file) -f (mix-in folder) -o (proj|nuget) [-v] [-w]``` switches:

| Switch | Description |
| :---|:---|
| s, solution  | path to the solution file |
| f, folder | path to the mix-in folder |
| o, operation | "proj" to change NuGets to project treferences, "nuget" to change projects to NuGets, "cleanup" to run Transmogri-Sort™ |
| v, verbose | outputs verbose information |
| w, wait | waits for input after finished operation |

## Transmogri-Sort as a VisualStudio command
By adding Transmogrify as an external tool to Visual Studio you can easily transmogri-sort your projects anytime.

* Go to Tools > External Tools...
* Click Add
* Enter title "Transmogri-Sort™"
Command: [path to downloaded tool, e.g. c:/tools/transmogrify]/transmogrify.exe
Arguments: -s $(SolutionDir)$(SolutionFileName) -f -o cleanup -v
Initial Directory: $(ProjectDir)

## Definition of transmogrify
_transmogrified; transmogrifying_

_transitive verb_
to change or alter greatly and often with *grotesque* or *humorous effect*

_intransitive verb_
to become transmogrified

## Copyright

Copyright (c) 2017 Fredrik Göransson. All rights reserved.

