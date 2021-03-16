
## PLGX Build Tasks

###### Actually just one task so far.

---
The MSBuild task in this package uses build items, item metadata and a "clean room" implementation of the [KeePass 2.x](https://keepass.info) archive creation utility to generate .PLGX files as a C# plugin build product.  This was inspired by the general purpose [
KeePassPluginDevTools](https://github.com/dlech/KeePassPluginDevTools) package, a.k.a. [**PlgxTool**](https://www.nuget.org/packages/PlgxTool).

So why is a new tool necessary?  One motivation is development flexibility.  While the strict [coding requirements](https://keepass.info/help/v2_dev/plg_index.html) for KeePass plugins are well defined, new build environments such as the [`dotnet` CLI](https://docs.microsoft.com/en-us/dotnet/core/install/windows), are changing the way developers create, test, and release .NET software. Also, by fully integrating .PLGX production within MSBuild, new features leveraging intermediate build products are achievable, such as localization resource deployment.

The hope is to encourage new plugin development, and help existing plugin authors migrate to new, perhaps improved tooling. 

* [Requirements](#requirements)
* [Quick Start](#quick-start)
* [Background](#background)
* [Unique Features](#unique-features-of-this-tool)
* [PlgxTool Compatible Features](#plgxtool-compatible-features)
* [Missing PlgxTool Features](#plgxtool-incompatibility)
* [Future Enhancements](#project-todos)
* [.PLGX Creation Properties](#properties)

#### Requirements

* A .NET Framework development environment including MSBuild and C#, such as Visual Studio or `dotnet` CLI.

The software has been tested thoroughly with Visual Studio 2019, and .NET 5 development tools, with `net472` and `net45` TFMs.  MSBuild v15 or higher is required for using `dotnet` CLI.

#### Background

[.PLGX files](https://keepass.info/help/v2_dev/plg_index.html#plgx) are an installation media file format often distributed by plugin providers as recommended by KeePass. Traditionally, .PLGX archives contain the C# source code of the plugin, optional WinForms-based resources, non-framework assembly dependencies, and a copy of the plugin's C# project file.  When KeePass loads a new plugin, it extracts the contents of the .PLGX archive, reads select portions of its C# project file, and invokes the installation target's .NET Framework C# compiler to create and install the plugin assembly.  This is done "on the fly", usually without user intervention.  Ostensibly, this convention ensures that the plugin is compliant with the interface and runtime characteristics of the installed version of KeePass. Further, KeePass has control of a central "cache" of plugins installed on the target machine via .PLGX. 

Today, end users benefit from [a large collection of useful plugins](https://keepass.info/help/v2/plugins.html).  But while the KeePass v2 plugin interface and the .NET Framework are both now quite mature, with only infrequent and compatibility-conscious changes, .PLGX archive distribution remains a well established regimen within the plugin community.

#### Unique Features of This Tool

* Supports both "SDK" and traditional C# project file types.
* Supports either `<PackageReference>` or `packages.config` NuGet dependencies.
* Supports deployment of MSBuild-generated, resource-only satellite assemblies, commonly used for localization.
* Uses MSBuild item and item metadata products, rather than a separate scan of project file contents, to populate the archive.
* Archives a task-generated, minimal C# project file, rather than a copy of the development project file. The development project file need not match the plugin symbolic name.
* Generated .PLGX files are often smaller, resulting in a slight improvement of plugin initialization performance.
* Optionally,`<EmbeddedResource>` items may be archived as pre-complied .RESOURCE files, for a modest boost in initialization performance and reduced .PLGX file size.
* Several [MSBuild property extensions](#properties) are defined and can be overridden to customize the output, including archive contents, name, output path, and KeePass .PLGX deployment options.
* Build-time checks for a few common plugin development pitfalls produce build errors or warnings.
* `clean` target extension ensures proper removal of .PLGX output.

#### PlgxTool Compatible Features

* Installs as a NuGet build tool package.
* Observes `<ExcludeFromPlgx/>` item metadata, to exclude specific project items, which would otherwise be included, from the archive.
* Enabled via invocation of a post-build target added to the project file.

#### PlgxTool Incompatibility
* The `<PlgxConfiguration>` property is not supported. That property's `<Prerequisistes>` XML fragment, which provides the means to set some .PLGX creation options, is replaced by individual, first-class [MSBuild properties](#properties).

#### Project TODOs

* Add companion task to produce a .ZIP archive for "portable installation" distributions.
* Enhance build-time error checks, possibly via Reflection, of the output plugin assembly.
* Supplement MSBuild project file schema to include targets, properties, and lightweight documentation.
* Source code "minify" option for potentially smaller .PLGX files.
* VSIX extension to provide plugin "starter" project templates (maybe a wizard?) including the NuGet reference and post-build target.

#### Quick Start

1. Add the NuGet reference to the project.
```
More info to come.
```

2. Optionally configure the post-build target to, for example, run only for release builds as shown below.  Add the target after the NuGet reference (`<Reference>` or `<PackageReference>`) introduced above.
```
    <Target Name="PlgxBoot" AfterTargets="Build" Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
      <CallTarget Targets="PlgxBuild" />
    </Target>
```

3. Optionally configure .PLGX output path, name, and other customizations (see below).

#### Properties

Use the following MSBuild property extensions to set various options, including those that configure the way KeePass prepares the plugin for loading.  Some of these correspond to options specified in the `<PlgxConfiguration>` property used by PlgxTool.  Specify these properties within a `<PropertyGroup>` element of your project file to override the default values shown in the table.

| Property              | Default value                         | Description                |
|-----------------------|---------------------------------------|----------------------------|
| PlgxArchiveBaseName   | Base name of the plugin assembly, with the .PLGX extension.| The name of the output .PLGX file, specified as a base file name (without the .PLGX extension).|
| PlgxOutputFolder      | The project output folder, `$(OutputPath)`. | The directory where the output .PLGX file will be placed.  Specify as an absolute path or relative to the project file directory.|
| PlgxTargetKpVersion   | No default.                     | If specified, sets the `--plgx-prereq-kp` option to inform KeePass of the "lowest" KeePass version required by the plugin. KeePass only recognizes release numbers given in simple, dotted notation, e.g., "2.09".|
| PlgxTargetNetFramework | No default.                    | If specified, sets the `--plgx-prereq-net` option to inform KeePass of the .NET Framework version requirements of the plugin. Valid values are dotted notation .NET Framework version numbers with no prefix. For example, "4.5" or "4.7.2".|
| PlgxTargetOs          | No default.                     | If specified, sets the `--plgx-prereq-os` option to inform KeePass of the operating system required by the plugin. KeePass recognizes only two values: "Unix", and "Windows".|
| PlgxTargetPtrSize     | No default.                     | If specified, sets the `--plgx-prereq-ptr` option to inform KeePass of the pointer size (platform architecture) required by the plugin.  Valid values are "4" and "8".|
| PlgxUseCompiledResource | false                               | If set 'true', archives .RESOURCE files created by MSBuild rather than the .RESX source files from which they are derived. This improves initialization performance and usually reduces .PLGX file size. If `false`, .RESX source files are archived instead.|
| PlgxReferencesArchivedFolderName | `___PLGX_References`        | Defines the name of the folder within the .PLGX archive where "copied", non-framework assembly dependencies, if any, reside.|
| PlgxSatelliteAssembliesArchivedFolderName| `___PLGX_Satellites`| Defines the name of the folder within the .PLGX archive where resource-only satellite assemblies, if any, reside.|
