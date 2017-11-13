using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaintCode2Skia.Core
{
    public static class StringUtils
    {
        public static String ReplaceAll(this String str, Dictionary<String, String> map)
        {
            if (String.IsNullOrEmpty(str))
                return str;

            foreach (var mapStringKv in map)
            {
                str = str.Replace(mapStringKv.Key, mapStringKv.Value);
            }

            return str;
        }

        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
    }
}
