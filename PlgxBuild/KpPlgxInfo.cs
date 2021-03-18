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

using System;
using System.Linq;

namespace PlgxBuildTasks
{
    class KpPlgxInfo
    {
        public const string KpProductName = "KeePass";
        const string KpFileVersionString = "2.35.0.0";
        const string PlgxFormatVersionString = "1.0";


        protected enum ArchObj : ushort
        {
            Eof = 0,
            FileUuid,
            BaseFileName,
            BeginContent,
            File,
            EndContent,
            CreationTime,
            GeneratorName,
            GeneratorVer,
            PrereqKp,
            PrereqNetFw,
            TargetOs,
            PrereqPtrSize,
            PreProc,
            PostProc
        }

        protected enum FileObj : ushort
        {
            Eof,
            Path,
            Data
        }

        static readonly byte[] PlgxMagic = new[]{
            (byte)0x19, (byte)0x07, (byte)0xd9, (byte)0x65,
            (byte)0x03, (byte)0x05, (byte)0xdd, (byte)0x3d 
        };

        public Version PlgxKpGeneratorVersion
        {
            get => Version.Parse(KpFileVersionString);
            set
            {
                if (value < PlgxKpGeneratorVersion)
                {
                    throw new NotSupportedException("Unsupported .PLGX file version.");
                }
            }
        }

        public Version PlgxFormatVersion
        {
            get => Version.Parse(PlgxFormatVersionString);
            set
            {
                if (value != PlgxFormatVersion)
                {
                    throw new NotSupportedException("Unsupported .PLGX format.");
                }
            }
        }

        public byte[] PlgxMagicNumber
        {
            get => PlgxMagic;
            set
            {
                if (value != null && PlgxMagic.SequenceEqual(value))
                {
                    return;
                }
                throw new BadImageFormatException("Input file has invalid header.");
            }
        }

        public string BaseFileName { get; set; }

        public Guid FileGuid { get; set; }

        public string TargetNetFramework { get; set; }

        public string TargetOsMoniker { get; set; }

        public string TargetKpVersionString { get; set; }

        public uint? TargetPointerSize { get; set; } = null;

        public string PreProc { get; set; }

        public string PostProc { get; set; }
    }
}
