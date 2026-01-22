using System;
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
        public void Render(CommandResult result)
        {
            if (!result.Success)
            {
                ConsoleOutput.WriteError(
                    $"{result.Code}: {result.Message}"
                );
                return;
            }

            if (ConsoleOutput.Verbose && !string.IsNullOrWhiteSpace(result.Message))
                ConsoleOutput.WriteLine(result.Message);

            if (result.Payload != null)
                RenderPayload(result.Payload);

            if (ConsoleOutput.Verbose && result.Assigned?.Any() == true)
            {
                foreach (var kvp in result.Assigned)
                    ConsoleOutput.WriteLine(
                        $"Assigned '{kvp.Key}' ({kvp.Value})"
                    );
            }
        }

        public void RenderException(Exception ex)
        {
            ConsoleOutput.WriteError(ex.Message);
        }

        public static void RenderPayload(object payload)
        {
            if (payload is null)
                return;

            switch (payload)
            {
                case string s:
                    Console.Out.WriteLine(s);
                    break;

                case char c:
                    Console.Out.WriteLine(c);
                    break;

                case bool b:
                    Console.Out.WriteLine(b.ToString().ToLowerInvariant());
                    break;

                case int or uint or long or float or double or decimal:
                    Console.Out.WriteLine(payload);
                    break;

                case IEnumerable<uint> uints:
                    RenderUintList(uints);
                    break;

                case IEnumerable<int> ints:
                    RenderIntList(ints);
                    break;

                case IEnumerable<string> lines:
                    RenderLines(lines);
                    break;

                case IDictionary<string, string> helpText:
                    RenderHelpText(helpText);
                    break;

                case IDictionary<string, object> dict:
                    RenderDictionary(dict);
                    break;

                case IEnumerable<Dictionary<string,string>> helpTexts:
                    RenderHelpLines(helpTexts);
                    break;

                case IEnumerable<object> objects:
                    RenderObjectList(objects);
                    break;

                default:
                    Console.Out.WriteLine(payload.ToString());
                    break;
            }
        }

        private static void RenderHelpText(IDictionary<string, string> helpText)
        {
            Console.Out.WriteLine("SYNTAX:");
            Console.Out.WriteLine($"  {helpText["Syntax"]}{Environment.NewLine}");
            Console.Out.WriteLine("DESCRIPTION:");
            Console.Out.WriteLine($"  {WordWrap(helpText["Description"])}{Environment.NewLine}");
        }

        private static void RenderHelpLines(IEnumerable<Dictionary<string, string>> helpLines)
        {
            foreach (var line in helpLines)
            {
                Console.Out.WriteLine($"{line["Command"]}");
                Console.Out.WriteLine($"  {line["Syntax"]}{Environment.NewLine}");
            }
        }

        private static void RenderUintList(IEnumerable<uint> values)
        {
            Console.Out.WriteLine($"[{string.Join(", ", values)}]");
        }

        private static void RenderIntList(IEnumerable<int> values)
        {
            Console.Out.WriteLine($"[{string.Join(", ", values)}]");
        }

        private static void RenderLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
                Console.Out.WriteLine(line);
        }

        private static void RenderObjectList(IEnumerable<object> items)
        {
            foreach (var item in items)
                Console.Out.WriteLine(item);
        }

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
                else if (kvp.Value is ICollection<int> intList)
                {
                    Console.Out.WriteLine($"{pad}{kvp.Key}:[{string.Join(", ", intList)}]");
                }
                else
                {
                    Console.Out.WriteLine($"{pad}{kvp.Key}: {kvp.Value}");
                }
            }
        }

        private static void RenderListOfDictionaries(
            string key,
            IEnumerable<IDictionary<string, object>> items,
            int indent)
        {
            string pad = new string(' ', indent * 2);
            string itemPad = new string(' ', (indent + 1) * 2);

            Console.Out.WriteLine($"{pad}{key}:");

            foreach (var item in items)
            {
                RenderNamedDictionaryItem(item, itemPad);
            }
        }

        private static void RenderNamedDictionaryItem(
    IDictionary<string, object> item,
    string pad)
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


    }
}
