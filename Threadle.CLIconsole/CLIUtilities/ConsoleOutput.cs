using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    public static class ConsoleOutput
    {
        public static bool Verbose { get; set; } = true;
        public static bool EndMarker { get; set; } = false;

        public static void WriteLine(string str, bool overrule = false)
        {
            if (str.StartsWith('!'))
                Console.Error.WriteLine(str);
            else if (Verbose || overrule)
                Console.WriteLine(str);
        }

        public static void WriteLine(List<string> lines, bool overrule=false)
        {
            foreach (string line in lines)
            {
                if (line.StartsWith('!'))
                    Console.Error.WriteLine(line);
                else if (Verbose || overrule)
                    Console.WriteLine(line);
            }
        }

        public static void Write(string str, bool overrule = false)
        {
            if (Verbose || overrule)
                Console.Write(str);
        }

        internal static void WriteEndMarker()
        {
            if (EndMarker)
                Console.WriteLine("__END__");
        }
    }
}
