# NuGet Package to Project.json Converter

<!-- Replace this badge with your own-->
[![Build status](https://ci.appveyor.com/api/projects/status/q1mkuttpcf3a3c03?svg=true)](https://ci.appveyor.com/project/twsouthwick/nugetpackageconfigconverter)

<!-- Update the VS Gallery link after you upload the VSIX-->
<!-- Download this extension from the [VS Gallery](https://visualstudiogallery.msdn.microsoft.com/[GuidFromGallery]) -->
Download this extension from the [CI build](http://vsixgallery.com/extension/NuGetPackageConfigConverter.Taylor%20Southwick.dd0141da-d26f-4013-8b78-72723a313486/).

Visual Studio Gallery link coming soon...

---------------------------------------

This will converts projects that are using NuGet v2 (ie `packages.config`) to NuGet v3 (ie `project.json`). There are many benefits
to using `project.json` even in non-.NET Core projects. Some of them include:

- Transitive dependencies are automatically included - you only need to include the dependencies you need
- install.ps1/uninstal.ps1 is not run so arbitrary code is not run on install and uninstall from untrusted packages
- Doesn't rewrite your project file

In order to run, right-click the Solution in `Solution Exporer` and click `Convert packages.config to project.json`: 

![Invocation](docs/assets/readme/invoke.png)

After selecting that, the project will be transformed as shown below. It is highly recommended that you perform this on a source-control enabled
directory so you can easly undo if something goes wrong.

|  Before                                   | After                                   |
|-------------------------------------------|-----------------------------------------|
| ![Before](docs/assets/readme/before.png) | ![After](docs/assets/readme/after.png) |

Please file any issues if something does not work as expected.

## License
[Apache 2.0](LICENSE)