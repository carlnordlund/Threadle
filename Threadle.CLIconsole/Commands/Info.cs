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
    internal class Info : ICommand
    {
        public string Usage => "[str] = info(name = [var:structure], *format = ['console'(default),'json'])";
        
        public string Description => "Displays metadata about the structure, which is either a Nodeset or a Network. Name and Filepath are included for both structures. Network structures also provide data on existing relational layers, their properties and number of edges. Nodeset structures also provide data on number of nodes, and existing node attributes and their types. The output will by default be console-friendly, but setting the optional format argument to 'json' produces a R/Python-friendly format for converting to native types.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            IStructure structure = context.GetVariableThrowExceptionIfMissing<IStructure>(command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0"));
            string outputFormat = command.GetArgumentParseString("format", "console");
            Dictionary<string, object> infoMetadata = structure.Info;
            if (outputFormat.Equals("console"))
                ConsoleOutput.PrintDictionary(infoMetadata);
            else
                ConsoleOutput.WriteLine(JsonSerializer.Serialize(infoMetadata, new JsonSerializerOptions { WriteIndented = false }));
        }
    }
}
