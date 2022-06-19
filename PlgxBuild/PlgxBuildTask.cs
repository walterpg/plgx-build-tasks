/*
    This file is part of the PlgxBuildTasks distribution:
    https://github.com/walterpg/plgx-build-tasks

    Copyright(C) 2021 Walter Goodwin

    PlgxBuildTasks is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PlgxBuildTasks is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with PlgxBuildTasks.  If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace PlgxBuildTasks
{
    public class PlgxBuildTask : Microsoft.Build.Utilities.Task
    {
        KpPlgxWriter m_writer = null;

        [Required]
        public string PlgxFileFolder { get; set; }

        [Required]
        public string ProjectReferencesFolderName { get; set; }

        [Required]
        public string ProjectSatelliteAssembliesFolderName { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string ArchiveBaseFileName { get; set; }

        [Required]
        public string ProjectFileFullPath { get; set; }

        [Required]
        public ITaskItem[] Compile { get; set; }

        [Required]
        public ITaskItem[] EmbeddedResource { get; set; }

        [Required]
        public ITaskItem[] None { get; set; }

        [Required]
        public ITaskItem[] Content { get; set; }

        [Required]
        public ITaskItem[] SatelliteResource { get; set; }

        [Required]
        public string UseCompiledResource { get; set; }

        [Required]
        public ITaskItem[] Reference { get; set; }

        [Required]
        public ITaskItem[] ResolvedReference { get; set; }

        [Required]
        [Output]
        public ITaskItem[] OutputPlgx { get; set; }

        public string BeforeCommand { get; set; }

        public string AfterCommand { get; set; }

        public string TargetOsMoniker { get; set; }

        public uint TargetPointerSize { get; set; }

        public string TargetNetFramework { get; set; }

        public string TargetKpVersion { get; set; }

        string PlgxTempFolder { get; set; }

        string ProjectFileFolder => Path.GetDirectoryName(ProjectFileFullPath);

        IEnumerable<ITaskItem> EmbeddedResourceWithoutSatelliteResource
        {
            get
            {
                return EmbeddedResource.Where(
                    i => !i.HasMetadata("WithCulture") ||
                        string.IsNullOrEmpty(i.GetMetadata("WithCulture")) ||
                        !i.HasMetadata("Culture") ||
                        string.IsNullOrEmpty(i.GetMetadata("Culture")));
            }
        }

        string ModifiedAfterCommand
        {
            get
            {
                if (!SatelliteResource.Any())
                {
                    return AfterCommand;
                }
                StringBuilder sb = new StringBuilder();
                sb.Append("cmd /c xcopy \"{PLGX_TEMP_DIR}");
                sb.Append(ProjectSatelliteAssembliesFolderName);
                sb.Append("\\*\" \"{PLGX_CACHE_DIR}\" /s");
                if (!string.IsNullOrEmpty(AfterCommand))
                {
                    sb.Append(" & ");
                    sb.Append(AfterCommand);
                }
                return sb.ToString();
            }
        }

        bool UseCompiledResourceBool
            => bool.TryParse(UseCompiledResource, out bool res) && res;

        void AddFilteredItems(string name, IEnumerable<ITaskItem> items,
            Func<IEnumerable<ITaskItem>, IEnumerable<ITaskItem>> filter,
            XmlWriter xw, Func<ITaskItem, string> includer,
            Action<XmlWriter, ITaskItem> adorner)
        {
            foreach (ITaskItem item in filter(items))
            {
                xw.WriteStartElement(name);
                string include = includer?.Invoke(item) ??
                    item.ItemSpec;
                xw.WriteAttributeString("Include", include);
                adorner?.Invoke(xw, item);
                xw.WriteEndElement();
            }
        }

        void AddItems(string name, IEnumerable<ITaskItem> items,
            XmlWriter xw, Func<ITaskItem, string> includer = null,
            Action<XmlWriter, ITaskItem> adorner = null)
        {
            IEnumerable<ITaskItem> excluder(IEnumerable<ITaskItem> _)
            {
                return items.ExceptWith("ExcludeFromPlgx");
            }
            AddFilteredItems(name, items, excluder,
                xw, includer, adorner);
        }

        void AddExplicitInclude(string name,
            IEnumerable<ITaskItem> items,
            XmlWriter xw, Func<ITaskItem, string> includer = null,
            Action<XmlWriter, ITaskItem> adorner = null)
        {
            AddFilteredItems(name, items,
                i => i.OnlyWith("IncludeInPlgx"),
                xw, includer, adorner);
        }

        void CopyItemToTemp(XmlWriter xw, ITaskItem item)
        {
            CopyItemToTemp(xw, item, bCopyToSubDirRoot: false);
        }

        void CopyItemToTemp(XmlWriter xw, ITaskItem item,
            bool bCopyToSubDirRoot)
        {
            CopyItemToTemp(xw, item.ItemSpec, string.Empty,
                bCopyToSubDirRoot);
        }

        void CopyItemToTemp(XmlWriter xw, string sourcePath, 
            string destSubDir, bool bCopyToSubDirRoot,
            Func<string, string> destNameMunger = null)
        {
            string destPath = CopyItemToTemp(sourcePath, destSubDir,
                bCopyToSubDirRoot, destNameMunger);
#if DEBUG_CSPROJ
            xw.WriteStartElement("PlgxDebug");
            xw.WriteAttributeString("SourcePath", sourcePath);
            xw.WriteAttributeString("DestPath", destPath);
            xw.WriteEndElement();
#endif
        }

        string CopyItemToTemp(string sourcePath, string destSubDir)
        {
            return CopyItemToTemp(sourcePath, destSubDir, 
                bCopyToSubDirRoot: false, null);
        }

        string CopyItemToTemp(string sourcePath, 
            string destSubDir, bool bCopyToSubDirRoot,
            Func<string, string> destNameMunger)
        {
            if (destSubDir.Length > 0 &&
                destSubDir[destSubDir.Length-1] != Path.DirectorySeparatorChar)
            {
                destSubDir += Path.DirectorySeparatorChar;
            }

            // Source paths are either rooted or relative to the project
            // folder.
            string destPath = destSubDir;
            if (Path.IsPathRooted(sourcePath) || bCopyToSubDirRoot)
            {
                destPath += Path.GetFileName(sourcePath);
            }
            else
            {
                destPath += sourcePath;
                sourcePath = Path.Combine(ProjectFileFolder, sourcePath);
            }

            if (destNameMunger == null)
            {
                destNameMunger = n => n;
            }
            m_writer.AddFile(sourcePath, destNameMunger(destPath));

#if DEBUG_FILES
            if (PlgxTempFolder.EndsWith(new string(Path.DirectorySeparatorChar,1)))
            {
                destPath = PlgxTempFolder + destPath;
            }
            else
            {
                destPath = PlgxTempFolder + Path.DirectorySeparatorChar + destPath;
            }
            m_writer.EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            File.Copy(sourcePath, destPath);
#endif
            return destPath;
        }

        void AddCompileItems(XmlWriter xw)
        {
            AddItems("Compile", Compile, xw, null, CopyItemToTemp);
        }

        void AddResourceItems(XmlWriter xw)
        {
            if (!UseCompiledResourceBool)
            {
                AddItems("EmbeddedResource",
                    EmbeddedResourceWithoutSatelliteResource, xw, null,
                    CopyItemToTemp);
                return;
            }

            // Pre-compiled .resource files are already built and waiting
            // to be used, so add those to the .PLGX instead of letting KP
            // "build" them again later.
            string stripNsPrefix(string include)
            {
                if (include.StartsWith(AssemblyName + '.'))
                {
                    // MSBuild adds the assembly namespace prefix to RESGENed
                    // .resource files, so remove it for KP.
                    include = include.Substring(
                        (AssemblyName + '.').Length);
                }
                return include;
            }
            string resolveInclude(ITaskItem resx)
            {
                return stripNsPrefix(
                    Path.GetFileName(resx.GetMetadata("OutputResource")));
            }
            void adorn(XmlWriter _, ITaskItem i)
            {
                CopyItemToTemp(xw,
                    i.GetMetadata("OutputResource"), string.Empty,
                    bCopyToSubDirRoot: true,
                    stripNsPrefix);
            }
            AddItems("EmbeddedResource",
                EmbeddedResourceWithoutSatelliteResource, xw, resolveInclude,
                adorn);
        }

        void AddReferenceItems(XmlWriter xw)
        {
            string resolveInclude(ITaskItem refFile)
            {
                return Path.GetFileNameWithoutExtension(refFile.ItemSpec);
            }
            void adornAndCopy(XmlWriter _, ITaskItem i)
            {
                // KP uses the "HintPath" metadata to resolve external
                // references.
                string hintPath = ProjectReferencesFolderName +
                    Path.DirectorySeparatorChar +
                    Path.GetFileName(i.ItemSpec);
                xw.WriteElementString("HintPath", hintPath);

                CopyItemToTemp(xw, i.ItemSpec, ProjectReferencesFolderName +
                    Path.DirectorySeparatorChar, bCopyToSubDirRoot: false);
            }
            IEnumerable<ITaskItem> refFilter;
            refFilter = ResolvedReference.Where(r =>
            {
                string fileName = Path.GetFileName(r.ItemSpec);
                StringComparison sc = StrComparisonOIC;

                // KeePass adds a reference to its csc.exe command, so this
                // one isn't needed.
                bool exclude = "KeePass.exe".Equals(fileName, sc);

                // ResolvedReference apparently includes .pdb files.
                exclude |= string.IsNullOrEmpty(fileName) ||
                    fileName.EndsWith(".pdb", sc);

                return !exclude;
            });
            AddItems("Reference", refFilter, xw, resolveInclude,
                adornAndCopy);
        }

        static StringComparison StrComparisonOIC =>
            StringComparison.OrdinalIgnoreCase;

        static StringComparer StrComparerOIC =>
            StringComparer.OrdinalIgnoreCase;

        void AddSystemReferenceItems(XmlWriter xw)
        {
            var sysRefs = Reference.Where(r =>
            {
                bool exclude;

                // Exclude refs already added as "resolved".
                exclude = ResolvedReference.Select(rr => rr.ItemSpec)
                    .Contains(r.ItemSpec, StrComparerOIC);

                // Another look for KeePass.
                if (!exclude)
                {
                    StringComparison sc = StrComparisonOIC;
                    exclude = string.IsNullOrEmpty(r.ItemSpec);
                    if (!exclude)
                    {
                        exclude = r.ItemSpec.StartsWith("KeePass", sc);
                        if (!exclude && r.HasMetadata("HintPath"))
                        {
                            string hintPath = r.GetMetadata("HintPath");
                            string hintFile = Path.GetFileName(hintPath);
                            exclude = string.Equals("KeePass.exe",
                                hintFile, sc);
                        }
                    }
                }

                // Exclude hard-coded (unresolved) references.
                if (!exclude &&
                    !string.IsNullOrEmpty(
                        Path.GetDirectoryName(r.ItemSpec)))
                {
                    // If a reference has a hard path, as some multi-
                    // targeting NuGet packages will, make sure it
                    // was not already resolved to this build's TFM-
                    // specific directory.
                    string bareFileName =
                        Path.GetFileNameWithoutExtension(r.ItemSpec);
                    exclude = ResolvedReference.Select(
                            rr => Path.GetFileNameWithoutExtension(rr.ItemSpec))
                        .Contains(bareFileName, StrComparerOIC);
                    if (!exclude)
                    {
                        Log.LogWarning("Excluding unresolved assembly " +
                            "reference '{0}'. Perhaps the assembly isn't " +
                            "referenced by the plugin's code, or the " +
                            "file for the reference ({1}) is targeting " +
                            "a different .NET Framework version than " +
                            "the plugin.",
                            bareFileName, r.ItemSpec);
                    }
                }
                return !exclude;
            });

            AddItems("Reference", sysRefs, xw);
        }

        void AddSatelliteResourceItems()
        {
            // KP doesn't support "satellite" resource-only assemblies, but
            // shouldn't it, in the general case?
            // Resource assemblies will not reference KP in any way, so they
            // don't need to be "built" by KP at load time. The resource binary
            // format is not likely to change unless it's an exotic CLR;
            // in that case, plugin authors should follow KP guidance and not
            // attempt to use unsupported satellite assemblies.
            
            // So, they don't go in the project file. Hack them in via the
            // PostProc facility if possible.

            // The ItemSpec should be a relative path, and they will have
            // Culture meta data.
            foreach (ITaskItem satellite in SatelliteResource)
            {
                string destSubDir = ProjectSatelliteAssembliesFolderName +
                    Path.DirectorySeparatorChar +
                    satellite.GetMetadata("Culture");
                string sourcePath = satellite.ItemSpec;
                if (!Path.IsPathRooted(sourcePath))
                {
                    sourcePath = Path.Combine(ProjectFileFolder, sourcePath);
                }
                CopyItemToTemp(sourcePath, destSubDir);
            }
        }

        void ClearDirectory(string dirPath)
        {
            foreach (string dir in Directory.GetDirectories(dirPath))
            {
                Directory.Delete(dir, recursive: true);
            }
            foreach (string file in Directory.GetFiles(dirPath))
            {
                File.Delete(file);
            }
        }

        bool EnsureTempFolder()
        {
            do
            {
                PlgxTempFolder = Path.GetTempPath() +
                    Path.GetRandomFileName();
            }
            while (Directory.Exists(PlgxTempFolder));
            if (!Directory.Exists(PlgxTempFolder
                    .TrimEnd(Path.DirectorySeparatorChar)))
            {
                try
                {
                    Directory.CreateDirectory(
                        PlgxTempFolder.TrimEnd(Path.DirectorySeparatorChar));
                }
                catch
                {
                    Log.LogError($"{nameof(PlgxTempFolder)} '{PlgxTempFolder}' directory could not be created.");
                    throw;
                }
            }
            try
            {
                ClearDirectory(PlgxTempFolder.TrimEnd(Path.DirectorySeparatorChar));
            }
            catch
            {
                Log.LogError($"{nameof(PlgxTempFolder)} '{PlgxTempFolder}' could not be cleared.");
                Directory.Delete(
                    PlgxTempFolder.Trim(Path.DirectorySeparatorChar),
                    recursive: true);
                throw;
            }
            if (PlgxTempFolder[PlgxTempFolder.Length - 1] !=
                    Path.DirectorySeparatorChar)
            {
                PlgxTempFolder += Path.DirectorySeparatorChar;
            }
            return true;
        }

        bool BuildPlgx(XmlWriter xw)
        {
            xw.WriteStartElement("Project");

            // KeePass only looks for one property: AssemblyName.
            if (string.IsNullOrEmpty(AssemblyName))
            {
                Log.LogError("AssemblyName not specified.");
                return false;
            }
            xw.WriteStartElement("PropertyGroup");
            xw.WriteStartElement("AssemblyName");
            xw.WriteString(AssemblyName);
            xw.WriteEndElement();
            xw.WriteEndElement();

            // Put everything in one big ItemGroup.
            xw.WriteStartElement("ItemGroup");

            // <Compile> items.
            AddCompileItems(xw);

            // <EmbeddedResource> items.
            AddResourceItems(xw);

            // Resolved, external <Reference> items.
            AddReferenceItems(xw);

            // System <Reference> items.
            AddSystemReferenceItems(xw);

            if (!UseCompiledResourceBool)
            {
                // <None> & <Content> for KeePass to compile .resx files.
                AddItems("None", None, xw, null, CopyItemToTemp);
                AddItems("Content", Content, xw, null, CopyItemToTemp);
            }
            else
            {
                // <None> & <Content> required by plugin author.
                AddExplicitInclude("None", None, xw, null, CopyItemToTemp);
                AddExplicitInclude("Content", Content, xw, null,
                    CopyItemToTemp);
            }

            // Add compiled satellite resources to staging.
            AddSatelliteResourceItems();

            return !Log.HasLoggedErrors;
        }

        bool GenerateFiles()
        {
            // Get the output directory.
            string dirFullPath = PlgxFileFolder;
            if (Path.IsPathRooted(dirFullPath))
            {
                Log.LogError($"{nameof(PlgxFileFolder)} must be specified as a " +
                    "path relative to the project directory.");
                return false;
            }
            else
            {
                dirFullPath = Path.Combine(ProjectFileFolder, dirFullPath);
            }
            if (!dirFullPath.EndsWith(
                    new string(new[] { Path.DirectorySeparatorChar })))
            {
                dirFullPath += Path.DirectorySeparatorChar;
            }

            string newProjectFile = AssemblyName + ".csproj";
            string newProjectPath
                = Path.Combine(PlgxTempFolder, newProjectFile);
            using (m_writer = new KpPlgxWriter(Log)
            {
                BaseFileName = AssemblyName,
                FileGuid = Guid.NewGuid(),
                TargetNetFramework = TargetNetFramework,
                TargetOsMoniker = TargetOsMoniker,
                TargetPointerSize = TargetPointerSize,
                TargetKpVersionString = TargetKpVersion,
                PreProc = BeforeCommand,
                PostProc = ModifiedAfterCommand,
                OutputFilePath = dirFullPath +
                    ArchiveBaseFileName + ".plgx",
            })
            using (XmlWriter xw = XmlWriter.Create(newProjectPath,
                new XmlWriterSettings()
                {
                    OmitXmlDeclaration = true,
                    CloseOutput = true,
                    Encoding = Encoding.UTF8,
#if DEBUG
                    Indent = true,
#endif
                }))
            {
                if (BuildPlgx(xw))
                {
                    xw.Close();
                    m_writer.AddFile(newProjectPath, newProjectFile);
                    OutputPlgx = new ITaskItem[]
                    {
                        new TaskItem(PlgxFileFolder +
                            ArchiveBaseFileName + ".plgx")
                    };
                }
            }
            return !Log.HasLoggedErrors;
        }

        public override bool Execute()
        {
            //System.Diagnostics.Debugger.Launch();

            OutputPlgx = new ITaskItem[] { };

            // Keep local temp files in a tidy place.
            if (!EnsureTempFolder())
            {
                return false;
            }
            try
            {
                return GenerateFiles();
            }
            finally
            {
#if !DEBUG_FILES
                Directory.Delete(PlgxTempFolder, recursive: true);
#endif
            }
        }
    }
}
