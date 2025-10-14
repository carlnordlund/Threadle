using PopnetEngine.CLIconsole.CLIUtilities;
using PopnetEngine.Core.Model;
using PopnetEngine.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.CLIconsole.Commands
{
    // Make this into an explicit importer for whole networks
    // For importing individual layers to an existing network, use importlayer()

    // Or just discard and rewrite! For importing matrix, the ImportLayer command works fine. So no need to have matrix format here.
    // But I also need import functions for graphml, dl files, Pajek, so that could go here. And those should be for complete networks.
    public class ImportCommand : ICommand
    {
        public string Usage => "[var:structure] = import(file=\"[filepath]\", format=['matrix'], *nodeset=[var:nodeset],*sep=[separactor character])";
        public string Description => "Imports a network from file [filepath] with the specified 'format'. If 'nodeset' is provided, use this; otherwise, also generate a nodeset and store. Currently only implemented 'matrix' format: then it is assumed that it has labels on first row and first column. Stores the new network in [var:structure].";
        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string assignedVariable = command.AssignedVariable!;
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg0");
            string formatString = command.GetArgumentThrowExceptionIfMissingOrNull("format", "arg1");
            string separator = command.GetArgument("sep") ?? "\t";

            StructureResult structures;
            if (command.GetArgument("nodeset") is string nodesetName)
            {
                if (!(context.GetVariable<Nodeset>(nodesetName) is Nodeset nodeset))
                    throw new Exception($"!Error: Nodeset '{nodesetName}' not found.");
                structures = FileManager.Import(filepath, formatString, nodeset, separator);
            }
            else
                structures = FileManager.Import(filepath, formatString, null, separator);

            context.SetVariable(assignedVariable, structures.MainStructure);
            ConsoleOutput.WriteLine($"Structure '{structures.MainStructure.Name}' loaded and stored in variable '{assignedVariable}'");
            if (structures.AdditionalStructures.Count > 0)
                foreach (var kvp in structures.AdditionalStructures)
                {
                    context.SetVariable(assignedVariable + "_" + kvp.Key, kvp.Value);
                    ConsoleOutput.WriteLine($"Also loaded {kvp.Key} '{kvp.Value.Name}'.");
                }
        }
    }
}
