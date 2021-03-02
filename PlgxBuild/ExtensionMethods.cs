using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlgxBuildTasks
{
    static class ExtensionMethods
    {
        public static bool HasItems(this ITaskItem[] items)
        {
            return items != null && items.Any();
        }

        public static bool HasMetadata(this ITaskItem item, string name)
        {
            return item != null && !string.IsNullOrEmpty(name) &&
                item.MetadataNames.Cast<string>()
                    .Contains(name, StringComparer.Ordinal);
        }

        public static IEnumerable<ITaskItem> ExceptWith(
            this IEnumerable<ITaskItem> items, string meta)
        {
            return items?.Where(i => !i.HasMetadata(meta));
        }

        public static IEnumerable<ITaskItem> OnlyWith(
            this IEnumerable<ITaskItem> items, string meta)
        {
            return items?.Where(i => i.HasMetadata(meta));
        }

        static int NormalizedVerComp(int c)
            => c < 1 ? 0 : c;

        public static ulong ToKpUlongVer(this Version v)
        {
            ulong retVal = 0;
            void encodeVer(int c)
            {
                retVal <<= 16;
                retVal |= (ushort)NormalizedVerComp(c);
            }
            encodeVer(v.Major);
            encodeVer(v.Minor);
            encodeVer(v.Build);
            encodeVer(v.Revision);
            return retVal;
        }

        public static uint ToKpUintVer(this Version v)
        {
            uint retVal = 0;
            void encodeVer(int c)
            {
                retVal <<= 16;
                retVal |= (ushort)NormalizedVerComp(c);
            }
            encodeVer(v.Major);
            encodeVer(v.Minor);
            return retVal;
        }

        public static Version ToVersion(this ulong kpUlongVer)
        {
            ushort decodeVer()
            {
                ushort retVal = (ushort)(kpUlongVer >> 48);
                kpUlongVer <<= 16;
                return retVal;
            }
            return new Version(decodeVer(), decodeVer(), decodeVer(),
                decodeVer());
        }

        public static Version ToVersion(this uint kpUintVer)
        {
            ushort decodeVer()
            {
                ushort retVal = (ushort)(kpUintVer >> 16);
                kpUintVer <<= 16;
                return retVal;
            }
            return new Version(decodeVer(), decodeVer());
        }

        public static string ToKpUtcString(this DateTime dt)
        {
            string retVal = dt.ToUniversalTime().ToString("s");
            if (!retVal.EndsWith("Z"))
            {
                retVal += 'Z';
            }
            return retVal;
        }

        public static string ToPlgxSeparators(this string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }
            str = str.Replace('\\', '/');

            // Compress duplicate separators.
            StringBuilder retVal = new StringBuilder();
            int iPrevSlash = int.MinValue;
            for (int i = 0; i<str.Length; i++)
            {
                char c = str[i];
                if (c != '/' || iPrevSlash != i-1)
                {
                    retVal.Append(c);
                }
                else
                {
                    iPrevSlash = i;
                }
            }
            return retVal.ToString();
        }
    }
}
