using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Runtime
{
    /// <summary>
    /// Centralized helper for writing text output to the console.
    /// Used by TextCommandResultRenderer only.
    /// Respects verbosity and stdout/stderr separation.
    /// </summary>
    public static class ConsoleOutput
    {
        #region Properties
        /// <summary>
        /// If true, informational messages are printed.
        /// If false, only forced output (payloads, results) are printed.
        /// </summary>
        //public static bool Verbose { get; set; } = true;
        #endregion


        #region Methods (public)
        /// <summary>
        /// Writes a line to STDOUT.
        /// Respects verbosity setting unless overridden. Overriding done by JSON specifically.
        /// </summary>
        /// <param name="message">Line to write to STDOUT</param>
        /// <param name="overrideVerbose">Set to true to override the verbosity setting.</param>
        public static void WriteLine(string message, bool overrideVerbose = false)
        {
            if (!CLISettings.Verbose && !overrideVerbose)
                return;
            Console.Out.WriteLine(message);
        }

        /// <summary>
        /// Writes a string (without linebreak) to STDOUT.
        /// Respects verbosity setting unless overridden.
        /// </summary>
        /// <param name="message">String to write to STDOUT</param>
        /// <param name="overrideVerbose">Set to true to override the verbosity setting.</param>
        public static void Write(string message, bool overrideVerbose = false)
        {
            if (!CLISettings.Verbose && !overrideVerbose)
                return;
            Console.Out.Write(message);
        }

        /// <summary>
        /// Writes multiple lines to STDOUT.
        /// Respects verbosity setting unless overridden.
        /// </summary>
        /// <param name="lines">Collection of lines to write to STDOUT</param>
        /// <param name="overrideVerbose">Set to true to override the verbosity setting.</param>
        public static void WriteLines(IEnumerable<string> lines, bool overrideVerbose = false)
        {
            if (!CLISettings.Verbose && !overrideVerbose)
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
    }
}
