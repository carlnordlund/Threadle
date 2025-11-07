using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Threadle.Core.Utilities;

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

        public static void PrintDictionary(Dictionary<string, object> dict, int indent = 0)
        {
            string indentStr = new string(' ', indent * 2);

            foreach (var kvp in dict)
            {
                if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    Console.WriteLine($"{indentStr}{kvp.Key}:");
                    PrintDictionary(nestedDict, indent + 1);
                }
                else if (kvp.Value is IEnumerable<object> list && !(kvp.Value is string))
                {
                    Console.WriteLine($"{indentStr}{kvp.Key}: [");
                    foreach (var item in list)
                    {
                        if (item is Dictionary<string, object> itemDict)
                        {
                            PrintDictionary(itemDict, indent + 2);
                            Console.WriteLine();
                        }
                        else
                            Console.WriteLine($"{new string(' ', (indent + 2) * 2)}{item}");
                    }
                    Console.WriteLine($"{indentStr}]");
                }
                else
                {
                    Console.WriteLine($"{indentStr}{kvp.Key}: {kvp.Value}");
                }
            }
        }

    }
}
