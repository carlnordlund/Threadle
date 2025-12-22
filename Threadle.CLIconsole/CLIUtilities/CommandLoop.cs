using Threadle.CLIconsole.CLIUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    /// <summary>
    /// The static class representing the command loop, i.e. where the user types
    /// in commands on the CLI console, commands are being parsed and executed, and
    /// output is being shown.
    /// </summary>
    public static class CommandLoop
    {
        #region Properties
        /// <summary>
        /// Gets a Threadle welcome message for the console, with the current version numbers for
        /// the CLI console and the Core assembly.
        /// </summary>
        public static List<string> WelcomeMessage
        {
            get
            {
                var cliAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                var cliVersion = cliAssembly.GetName().Version?.ToString() ?? "unknown";

                var coreAssembly = typeof(Threadle.Core.Model.Network).Assembly;
                var coreVersion = coreAssembly.GetName().Version?.ToString() ?? "unknown";

                return new()
                {
                    $"\u001b[36mThreadle CLI Console v1.0.0.0   |   Threadle Core v1.0.0.0\u001b[0m\r\n" +
                    $"\u001b[90m--------------------------------------------------------------------------\u001b[0m\r\n" +
                    $"\r\n" +
                    $"  Developed by: \u001b[36mCarl Nordlund\u001b[0m\r\n" +
                    $"  Institute for Analytical Sociology (IAS), Linköping University\r\n" +
                    $"\r\n" +
                    $"  R extension (threadleR) by: \u001b[36mYukun Jiao\u001b[0m\r\n" +
                    $"\r\n" +
                    $"  Project website: \u001b[36mhttps://threadle.dev\u001b[0m\r\n" +
                    $"  Funding: Swedish Research Council (Vetenskapsrådet), Grant 2024-01861\r\n" +
                    $"\r\n" +
                    $"  Type '\u001b[36mhelp\u001b[0m' for commands, '\u001b[36mexit\u001b[0m' to quit.\r\n"
                };
            }
        }
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Method that starts the command loop: reads input from console (STDIN), takes care of
        /// the special 'exit' and 'help' commands, parses the input to a <see cref="Command"/> object
        /// and sends this to the <see cref="CommandDispatcher"/> for execution. All output is passed on
        /// to <see cref="ConsoleOutput"/>.
        /// </summary>
        internal static void Run()
        {
            var context = new CommandContext();
            var dispatcher = new CommandDispatcher();            
            Console.OutputEncoding = Encoding.UTF8;
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

            if (ConsoleOutput.Verbose)
                ConsoleOutput.WriteLine(WelcomeMessage);

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
                            ConsoleOutput.WriteLine($"{Environment.NewLine} Syntax: {cmd.Syntax}{Environment.NewLine}{Environment.NewLine}" + WordWrap(cmd.Description), true);
                        else
                            ConsoleOutput.WriteLine($" ? Unknown command: '{parts[1]}'", true);
                    }
                    else
                    {
                        ConsoleOutput.WriteLine($"Available commands (type 'help [command]' for details about specific [command]):{Environment.NewLine}", true);
                        foreach (var kvp in dispatcher.GetAllCommands())
                            ConsoleOutput.WriteLine($"{kvp.Key}:{Environment.NewLine}  {kvp.Value.Syntax}{Environment.NewLine}", true);
                    }
                    continue;
                }
                var command = CommandParser.Parse(input);
                if (command == null)
                {
                    ConsoleOutput.WriteLine("!Error: Invalid command syntax.");
                    ConsoleOutput.WriteEndMarker();
                    continue;
                }
                CommandResult result;
                try
                {
                    result = dispatcher.Dispatch(command, context);
                }
                catch (Exception ex)
                {
                    result = CommandResult.Fail("Exception", ex.Message);
                    //ConsoleOutput.WriteLine($"{ex.Message}");
                }
                RenderResult(result);
                ConsoleOutput.WriteEndMarker();
            }
            ConsoleOutput.WriteLine("Exiting...");
        }
        #endregion


        #region Methods (private)
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

        private static void RenderResult(CommandResult result)
        {
            if (!result.Success)
            {
                ConsoleOutput.WriteLine($"!Error [{result.Code}]: {result.Message}");
                return;
            }
            ConsoleOutput.WriteLine(result.Message);
            if (result.Assigned!=null)
            {
                foreach (var kvp in result.Assigned)
                    ConsoleOutput.WriteLine($"  Assigned {kvp.Key} : {kvp.Value}");
            }

            if (result.Payload!=null)
            {
                ConsoleOutput.WriteLine(result.Payload.ToString() ?? "");
            }
        }
        #endregion
    }
}
