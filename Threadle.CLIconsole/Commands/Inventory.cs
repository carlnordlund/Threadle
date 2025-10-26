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
    public class Inventory : ICommand
    {
        public string Usage => "i(*format = ['console'(default),'json'])";
        public string Description => "Provides an inventory of the currently stored data objects. Note that the brackets can be ignored. (This command is in honor of all 1970's text adventure games, where 'i' was used to check what you were carrying). The output will by default be console-friendly, but setting the optional format argument to 'json' produces a R/Python-friendly format for converting to native types.";

        public void Execute(Command command, CommandContext context)
        {
            string outputFormat = command.GetArgumentParseString("format", "console");
            if (context.Variables.Any())
            {
                Dictionary<string, object> inventoryMetadata = context.VariablesMetadata();
                if (outputFormat.Equals("console"))
                    ConsoleOutput.PrintDictionary(inventoryMetadata);
                else
                    ConsoleOutput.WriteLine(JsonSerializer.Serialize(inventoryMetadata, new JsonSerializerOptions { WriteIndented = false }), true);
            }
            else
                ConsoleOutput.WriteLine("No objects stored.");
        }
    }
}
