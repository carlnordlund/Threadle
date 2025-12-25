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
    /// Centralized helper for writing text output to the console.
    /// Respects verbosity, end markers, and stdout/stderr separation.
    /// </summary>
    public static class ConsoleOutput
    {
        #region Properties
        /// <summary>
        /// If true, informational messages are printed.
        /// If false, only forced output (payloads, results) are printed.
        /// </summary>
        public static bool Verbose { get; set; } = true;

        /// <summary>
        /// If true, prints an end marker after each command execution.
        /// </summary>
        //public static bool EndMarker { get; set; } = true;


        #endregion


        #region Core write methods

        /// <summary>
        /// Writes a line to STDOUT.
        /// Respects verbosity unless overridden.
        /// </summary>
        public static void WriteLine(
            string message,
            bool overrideVerbose = false)
        {
            if (!Verbose && !overrideVerbose)
                return;

            Console.Out.WriteLine(message);
        }

        public static void Write(string message, bool overrideVerbose = false)
        {
            if (!Verbose && !overrideVerbose)
                return;
            Console.Out.Write(message);
        }

        /// <summary>
        /// Writes multiple lines to STDOUT.
        /// </summary>
        public static void WriteLine(
            IEnumerable<string> lines,
            bool overrideVerbose = false)
        {
            if (!Verbose && !overrideVerbose)
                return;

            foreach (var line in lines)
                Console.Out.WriteLine(line);
        }

        /// <summary>
        /// Writes an error message to STDERR.
        /// Always printed, regardless of verbosity.
        /// </summary>
        public static void WriteError(string message)
        {
            Console.Error.WriteLine(message);
        }

        #endregion


        #region Helpers

        /// <summary>
        /// Writes a dictionary as key-value lines to STDOUT.
        /// Intended for human-readable inspection.
        /// </summary>
        public static void PrintDictionary(
            IDictionary<string, object> dict,
            bool overrideVerbose = true)
        {
            if (!Verbose && !overrideVerbose)
                return;

            foreach (var kvp in dict)
                Console.Out.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        /// <summary>
        /// Writes a list of values in a compact array-like format.
        /// Example: [1, 2, 3]
        /// </summary>
        public static void PrintArray<T>(
            IEnumerable<T> values,
            bool overrideVerbose = true)
        {
            if (!Verbose && !overrideVerbose)
                return;

            Console.Out.WriteLine($"[{string.Join(", ", values)}]");
        }

        #endregion


        #region End marker

        /// <summary>
        /// Writes the end marker if enabled.
        /// </summary>
        //public static void WriteEndMarker()
        //{
        //    if (EndMarker)
        //        Console.Out.WriteLine(EndMarkerText);
        //}

        #endregion
    }



/// <summary>
/// Helper class for displaying output on the console/terminal
/// </summary>
//public static class ConsoleOutput
//{
//    #region Properties
//    /// <summary>
//    /// Gets/sets verbosity. If false, only explicitly asked results are shown on the console, and
//    /// all information text and error messages are withheld. If true, Threadle is more verbose.
//    /// </summary>
//    public static bool Verbose { get; set; } = true;

//    /// <summary>
//    /// Gets/sets whether an endmarker should be shown after all output, i.e. '__END__'.
//    /// Showing the endmarker is really useful when running Threadle.CLIconsole as an external
//    /// process from R, as the STDOUT will output the endmarker once the CLI console is ready for
//    /// additional commands.
//    /// </summary>
//    public static bool EndMarker { get; set; } = false;
//    #endregion


//    #region Methods (internal)
//    /// <summary>
//    /// Method for writing a string to console, including newline. If the string starts with an exclamation mark,
//    /// it is assumed to be an error (e.g. from an exception), so then that is always written on
//    /// STDERR.
//    /// Otherwise, this method first checks whether Threadle is either running in verbose mode before
//    /// writing something to the console. This also writes something to the console if the <paramref name="overrule"/>
//    /// is set to true: this would then be an output that should be written even when running in non-verbose mode.
//    /// </summary>
//    /// <param name="str">The string to output.</param>
//    /// <param name="overrule">If set to true, this will override any non-verbose setting and always output the string.</param>
//    internal static void WriteLine(string str, bool overrule = false)
//    {
//        if (str.StartsWith('!'))
//            Console.Error.WriteLine(str);
//        else if (Verbose || overrule)
//            Console.WriteLine(str);
//    }

//    /// <summary>
//    /// Convenience function for writing a List of strings to the console. Calls the standard
//    /// <see cref="WriteLine(string, bool)"/> method.
//    /// </summary>
//    /// <param name="lines">a List of strings.to write to the console.</param>
//    /// <param name="overrule">If set to true, this will override any non-verbose setting and always output the string.</param>
//    internal static void WriteLine(List<string> lines, bool overrule = false)
//    {
//        foreach (string line in lines)
//            WriteLine(line, overrule);
//    }

//    /// <summary>
//    /// Method for writing a string to console, excluding a newline. Used for writing the
//    /// cursor prompt in the console.
//    /// </summary>
//    /// <remarks>Although the overrule works, it is not used by the calling <see cref="CommandLoop"/>.</remarks>
//    /// <param name="str">The string to write.</param>
//    /// <param name="overrule">If set to true, this will override any non-verbose setting and always output the string.</param>
//    internal static void Write(string str, bool overrule = false)
//    {
//        if (Verbose || overrule)
//            Console.Write(str);
//    }

//    /// <summary>
//    /// If the endmarker setting is active (true), this will write the endmarker to the console.
//    /// </summary>
//    internal static void WriteEndMarker()
//    {
//        if (EndMarker)
//            Console.WriteLine("__END__");
//    }

//    /// <summary>
//    /// Recursive method for writing a <see cref="Dictionary{string, object}"/> object (that could be
//    /// nested with more such dictionaries as values) to the console. Used primarily when writing
//    /// metadata about structures to the console
//    /// </summary>
//    /// <param name="dict"></param>
//    /// <param name="indent"></param>
//    internal static void PrintDictionary(Dictionary<string, object> dict, int indent = 0)
//    {
//        string indentStr = new string(' ', indent * 2);
//        foreach (var kvp in dict)
//        {
//            if (kvp.Value is Dictionary<string, object> nestedDict)
//            {
//                Console.WriteLine($"{indentStr}{kvp.Key}:");
//                PrintDictionary(nestedDict, indent + 1);
//            }
//            else if (kvp.Value is IEnumerable<object> list && !(kvp.Value is string))
//            {
//                Console.WriteLine($"{indentStr}{kvp.Key}: [");
//                foreach (var item in list)
//                {
//                    if (item is Dictionary<string, object> itemDict)
//                    {
//                        PrintDictionary(itemDict, indent + 2);
//                        Console.WriteLine();
//                    }
//                    else
//                        Console.WriteLine($"{new string(' ', (indent + 2) * 2)}{item}");
//                }
//                Console.WriteLine($"{indentStr}]");
//            }
//            else
//            {
//                Console.WriteLine($"{indentStr}{kvp.Key}: {kvp.Value}");
//            }
//        }
//    }
//    #endregion
//}
}
