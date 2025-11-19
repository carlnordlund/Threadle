using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'i' (inventory) CLI command.
    /// </summary>
    public class Inventory : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "i(*format = ['console'(default),'json'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Provides an inventory of the currently stored data objects. Note that the brackets can be ignored. (This command is in honor of all 1970's text adventure games, where 'i' was used to check what you were carrying). The output will by default be console-friendly, but setting the optional format argument to 'json' produces a R/Python-friendly format for converting to native types.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public void Execute(Command command, CommandContext context)
        {
            string outputFormat = command.GetArgumentParseString("format", "console");
            if (context.Variables.Any())
            {
                Dictionary<string, object> inventoryMetadata = context.VariablesMetadata();
                List<string> variableNames = context.VariableNames;

                //var inventoryList = context.VariablesMetadata2();

                if (outputFormat.Equals("console"))
                    ConsoleOutput.WriteLine(variableNames, true);
                //ConsoleOutput.PrintDictionary(inventoryMetadata);
                else
                    ConsoleOutput.WriteLine(JsonSerializer.Serialize(variableNames, new JsonSerializerOptions { WriteIndented = false }), true);
            }
            else
                ConsoleOutput.WriteLine("No objects stored.");
        }
    }
}
