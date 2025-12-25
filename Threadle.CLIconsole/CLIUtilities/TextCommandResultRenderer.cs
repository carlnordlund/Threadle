using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Threadle.CLIconsole.CLIUtilities
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

                case IEnumerable<string> lines:
                    RenderLines(lines);
                    break;

                case IDictionary<string, object> dict:
                    RenderDictionary(dict);
                    break;

                case IEnumerable<object> objects:
                    RenderObjectList(objects);
                    break;

                default:
                    Console.Out.WriteLine(payload.ToString());
                    break;
            }
        }

        private static void RenderUintList(IEnumerable<uint> values)
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

        private static void RenderDictionary(
    IDictionary<string, object> dict,
    int indent = 0)
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

    }
}
