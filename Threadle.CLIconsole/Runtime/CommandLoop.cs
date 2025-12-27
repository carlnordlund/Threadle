using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;

namespace Threadle.CLIconsole.Runtime
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

                var coreAssembly = typeof(Core.Model.Network).Assembly;
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
        /// the special 'exit' and 'help' commands, parses the input to a <see cref="CommandPackage"/> object
        /// and sends this to the <see cref="CommandDispatcher"/> for execution. All output is passed on
        /// to <see cref="ConsoleOutput"/>.
        /// </summary>
        internal static void Run(bool jsonMode)
        {
            // Create the CommandContext that will store variables
            var context = new CommandContext();

            /// Create the renderer that will output CommandResult data
            /// Either as JSON or human-readable text
            ICommandResultRenderer renderer = jsonMode
                ? new JsonCommandResultRenderer()
                : new TextCommandResultRenderer();

            /// Create parser that will convert input from either human-readable
            /// text or JSON to a CommandPackage
            ICommandParser parser = jsonMode
                ? new JsonCommandParser()
                : new TextCommandParser();

            /// Set Console output properties
            Console.OutputEncoding = Encoding.UTF8;
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

            /// Write welcome message if non-JSON
            if (!jsonMode && ConsoleOutput.Verbose)
                ConsoleOutput.WriteLines(WelcomeMessage);

            // Start infinite command loop
            while (true)
            {
                if (!jsonMode)
                    ConsoleOutput.Write("> ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input) || input[0].Equals('#'))
                    continue;
                if (input.ToLower() == "exit")
                    break;
                var command = parser.Parse(input);
                //var command = CommandParser.Parse(input);
                if (command == null)
                {
                    ConsoleOutput.WriteLine("!Error: Invalid command syntax.");
                    continue;
                }
                
                try
                {
                    var result = CommandDispatcher.Dispatch(command, context);
                    renderer.Render(result);
                }
                catch (Exception ex)
                {
                    renderer.RenderException(ex);
                }
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
