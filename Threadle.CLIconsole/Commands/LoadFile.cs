using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'loadfile' CLI command.
    /// </summary>
    public class LoadFile : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:structure] = loadfile(file = \"[str]\", type = ['nodeset','network'], *compact = ['true','false'(default)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Loads a structure from file 'file', using the internal text-based file format. The type of structure is given by the 'type' argument, which can be either 'nodeset' or 'network'. When loading a network file that refers to a nodeset file, the nodeset is also loaded. If the filepath has the ending .tsv, it is loaded in the standard internal text-based format, if the .tsv.gz is used, it is loading a gzipped version of this. If the filepath has the ending .bin, it is loaded as a Threadle-style binary file, if the .bin.gz is used, it is loading a gzipped version of this. Note that the .bin and .bin.gz format are very compact and not human-readable. Also: when loading a network file that refers to a nodeset file, the nodeset is also loaded. The optional 'compact' argument specifies whether network layers should be stored in the more compact but immutable CSR format (true), or whether the layers should be stored in the more memory-demanding mutable default format. Note that individual layers can be converted between these two types separately.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => true;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            var assigned = new Dictionary<string, string>();
            string variableName = command.GetAssignmentVariableNameThrowExceptionIfNull();
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg0");
            string typeString = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg1");
            bool compactLayers = command.GetArgumentParseBool("compact", false);
            var result = FileManager.Load(filepath, typeString, compactLayers);
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);
            StructureResult structures = result.Value!;
            context.SetVariable(variableName, structures.MainStructure);
            assigned[variableName] = structures.MainStructure.GetType().Name;
            if (structures.AdditionalStructures.Count > 0)
                foreach (var kvp in structures.AdditionalStructures)
                {
                    string additionalAssignedVariable = variableName + "_" + kvp.Key;
                    context.SetVariable(additionalAssignedVariable, kvp.Value);
                    assigned[additionalAssignedVariable] = kvp.Value.GetType().Name;
                }
            return CommandResult.Ok(
                $"Loaded structure '{structures.MainStructure.Name}' from '{filepath}'",
                null,
                assigned
                );
        }
    }
}
