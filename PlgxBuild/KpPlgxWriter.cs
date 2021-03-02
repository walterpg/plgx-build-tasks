using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace PlgxBuildTasks
{
    class KpPlgxWriter : KpPlgxInfo, IDisposable
    {
        bool EnsureDirectoryExistsImpl(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            if (!Directory.Exists(path))
            {
                if (!EnsureDirectoryExistsImpl(Path.GetDirectoryName(path)))
                {
                    return false;
                }
                Directory.CreateDirectory(path);
            }
            return true;
        }

        public void EnsureDirectoryExists(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException(
                    "Must be a full, rooted path", nameof(path));
            }
            try
            {
                if (!EnsureDirectoryExistsImpl(path))
                {
                    throw new ArgumentException(nameof(path));
                }
            }
            catch
            {
                Log.LogError($"Failed to create directory '{path}'.");
                throw;
            }
        }

        BinaryWriter m_out;
        bool m_inProgress;

        public KpPlgxWriter(TaskLoggingHelper log, Stream s = null)
        {
            Log = log;
            m_inProgress = false;
            if (s != null)
            {
                if (!s.CanSeek)
                {
                    throw new ArgumentException(
                        "Input stream needs random access capability",
                        nameof(s));
                }
                m_out = new BinaryWriter(s, Encoding.UTF8, leaveOpen: true);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool isDisposing)
        {
            Close();
            m_out?.Dispose();
        }

        public TaskLoggingHelper Log { get; }

        public string OutputFilePath { get; set; }

        Version TargetFrameworkVersion
        {
            get
            {
                if (string.IsNullOrEmpty(TargetNetFramework))
                {
                    Log.LogWarning(
                        "Target framework version is not specified.");
                    return null;
                }

                // Be as forgiving as possible with this.
                string targetFramework = TargetNetFramework;
                targetFramework = targetFramework.TrimStart('v');
                if (targetFramework.StartsWith(".NETFramework,Version=v"))
                {
                    targetFramework = targetFramework.Substring(
                        ".NETFramework,Version=v".Length);
                }
                if (Version.TryParse(targetFramework, out Version ver))
                {
                    return ver;
                }

                // $$TODO parse SDK project TFMs?

                Log.LogWarning("Unrecognized target framework version " +
                    $"'{TargetNetFramework}'.");

                return null;
            }
        }

        public Version TargetKpVersion
        {
            get
            {
                if (string.IsNullOrEmpty(TargetKpVersionString))
                {
                    Log.LogWarning(
                        "Target KeePass version is not specified.");
                    return null;
                }

                if (!Version.TryParse(TargetKpVersionString, out Version ver))
                {
                    Log.LogError("Specified target KeePass version " +
                        $"{TargetKpVersionString}' is invalid.");
                }

                return ver;
            }
        }

        void EnsureStreaming()
        {
            if (m_inProgress)
            {
                return;
            }

            if (m_out == null)
            {
                EnsureDirectoryExists(Path.GetDirectoryName(OutputFilePath));
                FileStream f = new FileStream(OutputFilePath, FileMode.Create,
                    FileAccess.Write, FileShare.None);
                m_out = new BinaryWriter(f, Encoding.UTF8, leaveOpen: false);
            }

            Log.LogMessage(MessageImportance.Normal,
                $"Creating PLGX archive '{BaseFileName}' as " +
                $"'{OutputFilePath}'.");

            m_out.Write(PlgxMagicNumber);
            m_out.Write(PlgxFormatVersion.ToKpUintVer());
            WriteObject((int)ArchObj.FileUuid, FileGuid.ToByteArray());
            WriteObject((int)ArchObj.BaseFileName,
                BaseFileName ?? string.Empty);
            WriteObject((int)ArchObj.CreationTime, 
                DateTime.UtcNow.ToUniversalTime().ToKpUtcString());
            WriteObject((int)ArchObj.GeneratorName, KpProductName);
            WriteObject((int)ArchObj.GeneratorVer,
                PlgxKpGeneratorVersion.ToKpUlongVer());

            Version targetVersion = TargetKpVersion;
            if (targetVersion == null)
            {
                Log.LogWarning(
                    "PLGX archive valid target KP version is missing; " +
                    "KP cannot verify that its version is usable with " +
                    "the plugin without this property.");
            }
            else
            {
                WriteObject((int)ArchObj.PrereqKp,
                    targetVersion.ToKpUlongVer());
            }

            targetVersion = TargetFrameworkVersion;
            if (targetVersion == null)
            {
                Log.LogWarning(
                    "PLGX archive target framework is missing; " +
                    "KP cannot verify that the platform is " +
                    "running a .NET version required by the plugin " +
                    "without this property.");
            }
            else
            {
                WriteObject((int)ArchObj.PrereqNetFw,
                    targetVersion.ToKpUlongVer());
            }

            if (string.IsNullOrEmpty(TargetOsMoniker))
            {
                Log.LogMessage(MessageImportance.Low,
                    "PLGX archive target OS unspecified or invalid; " +
                    "KP will not check if the platform is running " +
                    "a specific operating system ('Windows' or 'Unix') " +
                    "required by the plugin.");
            }
            else
            {
                WriteObject((int)ArchObj.TargetOs,
                    TargetOsMoniker);
            }

            if (TargetPointerSize.HasValue)
            {
                WriteObject((int)ArchObj.PrereqPtrSize,
                    TargetPointerSize.Value);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low,
                    "PLGX archive desired pointer size not " +
                    "specified or invalid; optionally specify a " +
                    "pointer size (8 or 4) and KP will determine if " +
                    "the platform supports the requirement.");
            }

            if (!string.IsNullOrEmpty(PreProc))
            {
                WriteObject((int)ArchObj.PreProc, PreProc);
            }

            if (!string.IsNullOrEmpty(PostProc))
            {
                WriteObject((int)ArchObj.PostProc, PostProc);
            }

            WriteObject((int)ArchObj.BeginContent, Array.Empty<byte>());

            Log.LogMessage("Begin adding PLGX archive items.");

            m_inProgress = true;
        }

        void WriteObject(int objID, byte[] bytes)
        {
            m_out.Write((ushort)objID);
            m_out.Write((uint)(bytes?.Length ?? 0));
            if (bytes != null && bytes.Any())
            {
                m_out.Write(bytes);
            }
        }

        void WriteObject(int objID, string strVal)
        {
            WriteObject(objID, Encoding.UTF8.GetBytes(strVal));
        }

        void WriteObject(int objID, ulong ulongValLittleEndian)
        {
            m_out.Write((ushort)objID);
            m_out.Write((uint)8);
            m_out.Write(ulongValLittleEndian);
        }

        void WriteObject(int objID, uint uintValLittleEndian)
        {
            m_out.Write((ushort)objID);
            m_out.Write((uint)4);
            m_out.Write(uintValLittleEndian);
        }

        public void AddFile(string sourcePath, string destPath)
        {
            // KP generator does this check but throws empty
            // OutOfMemoryException.
            FileInfo fInfo = new FileInfo(sourcePath);
            if (fInfo.Length > (long)(int.MaxValue / 2) - 1)
            {
                throw new BadImageFormatException(
                    "KP will not process archives containing files larger " +
                    "than 1 GB.", sourcePath);
            }

            using (FileStream f = fInfo.OpenRead())
            {
                AddSource(f, destPath);
            }
        }

        public void AddSource(Stream source, string destPath)
        {
            EnsureStreaming();

            Log.LogMessage($"    {destPath}");

            m_out.Write((ushort)ArchObj.File);

            // Get the top of this section and save a place for its length.
            m_out.Flush();
            long iBof = m_out.BaseStream.Position;
            m_out.Write((uint)uint.MinValue);

            WriteObject((int)FileObj.Path, destPath.ToPlgxSeparators());

            m_out.Write((ushort)FileObj.Data);
            m_out.Flush();

            // Get current file position and write a dummy length.
            long iLengthField = m_out.BaseStream.Position;
            m_out.Write(uint.MinValue);
            m_out.Flush();

            using (GZipStream gz = new GZipStream(m_out.BaseStream,
                CompressionMode.Compress, leaveOpen: true))
            {
                source.CopyTo(gz);
            }

            WriteObject((int)FileObj.Eof, Array.Empty<byte>());

            // Rewind to file data length field and write it.
            long iEof = m_out.BaseStream.Position;
            m_out.BaseStream.Seek(iLengthField - iEof, SeekOrigin.Current);
            Debug.Assert(m_out.BaseStream.Position == iLengthField);
            // FileObj.Eof length == 4 + 2, field length == 4.
            uint dataLen = (uint)(iEof - iLengthField - 4 - 2 - 4);
            m_out.Write(dataLen);
            m_out.Flush();

            // Rewind to archive length field and write it.
            dataLen = (uint)(iEof - iBof - 4);
            long iMof = m_out.BaseStream.Position;
            m_out.BaseStream.Seek(iBof-iMof, SeekOrigin.Current);
            Debug.Assert(m_out.BaseStream.Position == iBof);
            m_out.Write(dataLen);
            m_out.Flush();

            // Seek to EOF.
            m_out.BaseStream.Seek(0, SeekOrigin.End);
        }

        public void Close()
        {
            if (!m_inProgress)
            {
                return;
            }

            Log.LogMessage("End adding PLGX archive items.");

            WriteObject((int)ArchObj.EndContent, Array.Empty<byte>());
            WriteObject((int)ArchObj.Eof, Array.Empty<byte>());

            m_inProgress = false;
        }
    }
}
