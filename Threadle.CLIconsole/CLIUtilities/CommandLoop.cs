using Threadle.CLIconsole.Dispatcher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    public static class CommandLoop
    {
        public static void Run()
        {
            var context = new CommandContext();
            var dispatcher = new CommandDispatcher();            

            Console.OutputEncoding = Encoding.UTF8;
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

            ConsoleOutput.WriteLine("Threadle CLI Console");
            ConsoleOutput.WriteLine("Type 'exit' to quit.");

            while (true)
            {
                ConsoleOutput.Write("> ");
                var input = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                if (input.ToLower() == "exit")
                    break;
                if (input.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var cmd = dispatcher.GetCommand(parts[1]);
                        if (cmd != null)
                            ConsoleOutput.WriteLine($"{Environment.NewLine} Usage: {cmd.Usage}{Environment.NewLine}{Environment.NewLine}" + WordWrap(cmd.Description), true);
                        else
                            ConsoleOutput.WriteLine($" ? Unknown command: '{parts[1]}'", true);
                    }
                    else
                    {
                        ConsoleOutput.WriteLine("Available commands (type 'help [command]' for details about specific [command]):", true);
                        foreach (var kvp in dispatcher.GetAllCommands())
                            ConsoleOutput.WriteLine($"  - {kvp.Key}:{Environment.NewLine}    {kvp.Value.Usage}", true);
                    }
                    continue;
                }


                var command = CommandParser.Parse(input);
                if (command == null)
                {
                    ConsoleOutput.WriteLine("!Error: Invalid command syntax.");
                    continue;
                }
                try
                {
                    dispatcher.Dispatch(command, context);
                }
                catch (Exception ex)
                {
                    ConsoleOutput.WriteLine($"{ex.Message}");
                }
                ConsoleOutput.WriteEndMarker();
            }
            ConsoleOutput.WriteLine("Exiting...");
        }


        public static string WordWrap(string unwrapped, int columns = 100)
        {
            string wrapped = "";
            string[] words = unwrapped.Split(' ');

            int currentLineLength = 0;
            foreach (string word in words)
            {
                if (currentLineLength + word.Length + 1 > columns)
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
