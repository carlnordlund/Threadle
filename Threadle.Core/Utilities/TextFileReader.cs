using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    internal static class TextFileReader
    {
        internal static string[] LoadFile(string filepath)
        {
            return File.ReadAllLines(filepath);
        }
    }
}
