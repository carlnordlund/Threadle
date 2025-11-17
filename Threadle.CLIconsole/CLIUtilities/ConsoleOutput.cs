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
    /// <summary>
    /// Helper class for displaying output on the console/terminal
    /// </summary>
    public static class ConsoleOutput
    {
        #region Properties
        /// <summary>
        /// Gets/sets verbosity. If false, only explicitly asked results are shown on the console, and
        /// all information text and error messages are withheld. If true, Threadle is more verbose.
        /// </summary>
        public static bool Verbose { get; set; } = true;
        
        /// <summary>
        /// Gets/sets whether an endmarker should be shown after all output, i.e. '__END__'.
        /// Showing the endmarker is really useful when running Threadle.CLIconsole as an external
        /// process from R, as the STDOUT will output the endmarker once the CLI console is ready for
        /// additional commands.
        /// </summary>
        public static bool EndMarker { get; set; } = false;
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Method for writing a string to console, including newline. If the string starts with an exclamation mark,
        /// it is assumed to be an error (e.g. from an exception), so then that is always written on
        /// STDERR.
        /// Otherwise, this method first checks whether Threadle is either running in verbose mode before
        /// writing something to the console. This also writes something to the console if the <paramref name="overrule"/>
        /// is set to true: this would then be an output that should be written even when running in non-verbose mode.
        /// </summary>
        /// <param name="str">The string to output.</param>
        /// <param name="overrule">If set to true, this will override any non-verbose setting and always output the string.</param>
        internal static void WriteLine(string str, bool overrule = false)
        {
            if (str.StartsWith('!'))
                Console.Error.WriteLine(str);
            else if (Verbose || overrule)
                Console.WriteLine(str);
        }

        /// <summary>
        /// Convenience function for writing a List of strings to the console. Calls the standard
        /// <see cref="WriteLine(string, bool)"/> method.
        /// </summary>
        /// <param name="lines">a List of strings.to write to the console.</param>
        /// <param name="overrule">If set to true, this will override any non-verbose setting and always output the string.</param>
        internal static void WriteLine(List<string> lines, bool overrule = false)
        {
            foreach (string line in lines)
                WriteLine(line, overrule);
        }

        /// <summary>
        /// Method for writing a string to console, excluding a newline. Used for writing the
        /// cursor prompt in the console.
        /// </summary>
        /// <remarks>Although the overrule works, it is not used by the calling <see cref="CommandLoop"/>.</remarks>
        /// <param name="str">The string to write.</param>
        /// <param name="overrule">If set to true, this will override any non-verbose setting and always output the string.</param>
        internal static void Write(string str, bool overrule = false)
        {
            if (Verbose || overrule)
                Console.Write(str);
        }

        /// <summary>
        /// If the endmarker setting is active (true), this will write the endmarker to the console.
        /// </summary>
        internal static void WriteEndMarker()
        {
            if (EndMarker)
                Console.WriteLine("__END__");
        }

        /// <summary>
        /// Recursive method for writing a <see cref="Dictionary{string, object}"/> object (that could be
        /// nested with more such dictionaries as values) to the console. Used primarily when writing
        /// metadata about structures to the console
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="indent"></param>
        internal static void PrintDictionary(Dictionary<string, object> dict, int indent = 0)
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
        #endregion
    }
}
