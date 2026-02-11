using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Results
{
    public sealed class TextCommandResultRenderer : ICommandResultRenderer
    {
        #region Methods (public)
        /// <summary>
        /// Renders the output of a command result to the console, displaying errors, messages, payloads, and assigned
        /// values as appropriate.
        /// </summary>
        /// <remarks>If the command result indicates failure, an error message is written to the console.
        /// If verbose output is enabled, additional details such as informational messages and assigned values are
        /// displayed. The payload is rendered if present.</remarks>
        /// <param name="result">The <see cref="CommandResult"/> to render. Contains the outcome, message, payload, and any assigned values
        /// from the executed command.</param>
        public void Render(CommandResult result)
        {
            if (!result.Success)
            {
                ConsoleOutput.WriteError(
                    $"{result.Code}: {result.Message}"
                );
                return;
            }

            // Start with displaying the actual message (as long as it exists and Verbose settings are true)
            if (CLISettings.Verbose && !string.IsNullOrWhiteSpace(result.Message))
                ConsoleOutput.WriteLine(result.Message);

            // Then display any Payload data if that exists
            if (result.Payload != null)
                RenderPayload(result.Payload, 0);

            // Finally display any assigned variables
            if (CLISettings.Verbose && result.Assigned?.Any() == true)
            {
                foreach (var kvp in result.Assigned)
                    ConsoleOutput.WriteLine(
                        $"Assigned '{kvp.Key}' ({kvp.Value})"
                    );
            }
        }

        /// <summary>
        /// If there is an exception during command execution, e.g. a missing argument, and if
        /// this is the renderer used, this is where the exception is written out on the console.
        /// </summary>
        /// <param name="ex">The exception that was caught.</param>
        public void RenderException(Exception ex)
        {
            ConsoleOutput.WriteError(ex.Message);
        }

        /// <summary>
        /// Renders the attached payload. This payload can be of most object types and can also be a recursive
        /// string-object dictionary. As this is called recursively as needed, the current indent is brought
        /// along.
        /// </summary>
        /// <param name="payload">The payload object.</param>
        /// <param name="indent">The current indentation of the object to be rendered.</param>
        public static void RenderPayload(object payload, int indent = 0)
        {
            if (payload is null)
                return;

            string pad = new string(' ', indent * 2);

            // Depending on the type of payload, either displays it or calls a method to display it
            switch (payload)
            {
                case string s:
                    Console.Out.WriteLine($"{pad}{s}");
                    break;

                case char c:
                    Console.Out.WriteLine($"{pad}{c}");
                    break;

                case bool b:
                    Console.Out.WriteLine($"{pad}{b.ToString().ToLowerInvariant()}");
                    break;

                case IEnumerable<uint> uints:
                    RenderUintList(uints, indent);
                    break;

                case IEnumerable<int> ints:
                    RenderIntList(ints, indent);
                    break;

                case IEnumerable<string> lines:
                    RenderLines(lines, indent);
                    break;

                case IDictionary<string, string> helpText:
                    RenderHelpText(helpText, indent);
                    break;

                case IDictionary<string, object> dict:
                    RenderDictionary(dict, indent);
                    break;
                case IDictionary<uint, object?> dict:
                    RenderDictionary(dict, indent);
                    break;

                case IEnumerable<Dictionary<string,string>> helpTexts:
                    RenderHelpLines(helpTexts, indent);
                    break;

                case IEnumerable<Dictionary<string, object>> dictList:
                    RenderDictionaryList(dictList, indent);
                    break;

                case IEnumerable<object> objects:
                    RenderObjectList(objects, indent);
                    break;

                default:
                    Console.Out.WriteLine($"{pad}{payload}");
                    break;
            }
        }
        #endregion


        #region Method (internal)
        /// <summary>
        /// Custom helper method for displaying the help text for a particular command.
        /// The help text is delivered as a string-string dictionary, where the keys are
        /// 'Syntax', and 'Description'
        /// </summary>
        /// <param name="helpText">The helptext dictionary.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderHelpText(IDictionary<string, string> helpText, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            Console.Out.WriteLine($"{pad}SYNTAX:");
            Console.Out.WriteLine($"{pad}  {helpText["Syntax"]}{Environment.NewLine}");
            Console.Out.WriteLine($"{pad}DESCRIPTION:");
            Console.Out.WriteLine($"{pad}  {WordWrap(helpText["Description"])}{Environment.NewLine}");
        }

        /// <summary>
        /// Custom helper to display help text for (likely) all CLI commands.
        /// The help text is delivered as a string-string dictionary, where the keys are
        /// 'Command', and 'Syntax'
        /// </summary>
        /// <param name="helpLines">The helptext dictionary.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderHelpLines(IEnumerable<Dictionary<string, string>> helpLines, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            foreach (var line in helpLines)
            {
                Console.Out.WriteLine($"{pad}{line["Command"]}");
                Console.Out.WriteLine($"{pad}  {line["Syntax"]}{Environment.NewLine}");
            }
        }

        /// <summary>
        /// Helper method to display a collection of unsigned int (in JSON-style)
        /// </summary>
        /// <param name="values">A collection of uints.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderUintList(IEnumerable<uint> values, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            Console.Out.WriteLine($"{pad}[{string.Join(", ", values)}]");
        }

        /// <summary>
        /// Helper method to display a collection of integers (in JSON-style)
        /// </summary>
        /// <param name="values">A collection of ints.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderIntList(IEnumerable<int> values, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            Console.Out.WriteLine($"{pad}[{string.Join(", ", values)}]");
        }

        /// <summary>
        /// Helper method to display a collection of strings (one per line).
        /// </summary>
        /// <param name="lines">A collection of strings.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderLines(IEnumerable<string> lines, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            foreach (var line in lines)
                Console.Out.WriteLine($"{pad}{line}");
        }

        private static void RenderDictionaryList(IEnumerable<Dictionary<string, object>> items, int indent =0)
        {
            string pad = new string(' ', indent * 2);

            foreach (var dict in items)
            {
                var pairs = dict.Select(kvp => $"{kvp.Key}={kvp.Value}");
                Console.Out.WriteLine($"{pad}[{string.Join(", ", pairs)}]");
            }
        }

        /// <summary>
        /// Helper method to display a collection of objects (one per line).
        /// </summary>
        /// <param name="items">A collection of objects.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderObjectList(IEnumerable<object> items, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            foreach (var item in items)
                Console.Out.WriteLine($"{pad}{item}");
        }

        /// <summary>
        /// Helper method to display a uint-object? dictionary, e.g. when displaying the return data
        /// from 'getattrs()'. Does not call anything recursively. Note that the object can be null:
        /// in that case, the text 'null' is written.
        /// </summary>
        /// <param name="dict">The uint-object? payload.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderDictionary(IDictionary<uint, object?> dict, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            foreach (var kvp in dict)
            {
                Console.Out.Write($"{pad}{kvp.Key}:");
                RenderPayload(kvp.Value == null ? "-" : kvp.Value, 0);
            }
        }

        /// <summary>
        /// Helper method to display a string-object dictionary. If needed, calls itself
        /// recursively, or calls the standard <see cref="RenderPayload(object, int)"> method.
        /// Note that an object can thus be another nested string-object dictionary.
        /// </summary>
        /// <param name="dict">The string-object payload.</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderDictionary(IDictionary<string, object> dict, int indent = 0)
        {
            string pad = new string(' ', indent * 2);

            foreach (var kvp in dict)
            {
                if (kvp.Value is IEnumerable<IDictionary<string, object>> list)
                {
                    RenderListOfDictionaries(kvp.Key, list, indent);
                    continue;
                }

                if (kvp.Value is IDictionary<string, object> nested)
                {
                    Console.Out.WriteLine($"{pad}{kvp.Key}:");
                    RenderDictionary(nested, indent + 1);
                }
                else
                {
                    Console.Out.Write($"{pad}{kvp.Key}:");
                    RenderPayload(kvp.Value, 0);
                }
            }
        }

        /// <summary>
        /// Helper method to display a list of string-object dictionaries. Calls <see cref="RenderNamedDictionaryItem(IDictionary{string, object}, string)"/>
        /// Used when displaying info about all layers in a network.
        /// </summary>
        /// <param name="name">The name of the list</param>
        /// <param name="items">The list of string-object dictionaries</param>
        /// <param name="indent">Current indentation.</param>
        private static void RenderListOfDictionaries(string name, IEnumerable<IDictionary<string, object>> items, int indent)
        {
            string pad = new string(' ', indent * 2);
            string itemPad = new string(' ', (indent + 1) * 2);

            Console.Out.WriteLine($"{pad}{name}:");

            foreach (var item in items)
            {
                RenderNamedDictionaryItem(item, itemPad);
            }
        }

        /// <summary>
        /// Helper method to display a string-object dictionary as a single line.
        /// Used, e.g., to display layer properties for a network.
        /// </summary>
        /// <param name="item">A string-object dictionary</param>
        /// <param name="pad">Padding prior to each line.</param>
        private static void RenderNamedDictionaryItem(IDictionary<string, object> item, string pad)
        {
            // Extract "name" if present
            item.TryGetValue("Name", out var nameObj);
            string name = nameObj?.ToString() ?? "<unnamed>";

            var remaining = item
                .Where(kvp => kvp.Key != "Name")
                .Select(kvp => $"{kvp.Key}={kvp.Value}");

            string details = string.Join("; ", remaining);

            if (details.Length > 0)
                Console.Out.WriteLine($"{pad}- {name} ({details})");
            else
                Console.Out.WriteLine($"{pad}- {name}");
        }

        /// <summary>
        /// Helper function for word wrapping output to the console, doing this by adding the system-default
        /// newline character(s) in a provided <paramref name="unwrapped"/> string so that the number of characters
        /// per row does not exceed the provided <paramref name="charWidth"/>.
        /// </summary>
        /// <param name="unwrapped">The original string text to be word-wrapped.</param>
        /// <param name="charWidth">The maximum width of the word-wrapped text.</param>
        /// <returns>A word-wrapped version of the provided text.</returns>
        private static string WordWrap(string unwrapped, int charWidth = 100)
        {
            string wrapped = "";
            string[] words = unwrapped.Split(' ');
            int currentLineLength = 0;
            foreach (string word in words)
            {
                if (currentLineLength + word.Length + 1 > charWidth)
                {
                    wrapped += $"{Environment.NewLine}";
                    currentLineLength = 0;
                }
                if (currentLineLength > 0)
                {
                    wrapped += " ";
                    currentLineLength++;
                }
                wrapped += word;
                currentLineLength += word.Length;
            }
            return wrapped;
        }
        #endregion
    }
}
