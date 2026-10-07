using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;
using Threadle.Core.Utilities.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'importnetwork' CLI command.
    /// </summary>
    public class ImportNetwork : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = importnetwork(file = \"[str]\", format = ['graphml','pajek'], *layerattr = [str(default:'layer')], *weightattr = [str(default:'weight')], *idattr = [str(default:'id')], *labelattr = [str(default:'label')], *pack = ['true','false'(default)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Imports a network from file 'file' in an external file format ('graphml' or 'pajek'), creating a new network and a new nodeset. The network is assigned to the variable, and the nodeset to the variable name with the suffix '_nodeset'. For 'graphml': nodes and their attributes are imported (int/long become int, float/double become float, boolean becomes bool, string becomes string); if all node ids in the file are unsigned integers these are used as node ids, otherwise nodes get new node ids (0, 1, 2, ...) with the original ids stored in the string node attribute given by 'idattr'. Edges go to 1-mode layers: if edges have the attribute given by 'layerattr', there is one layer per distinct value of it (edges without it end up in a layer named after the graph id), otherwise all edges go to one layer named after the graph id (or 'edges' if there is none); a layer is directed if its edges are directed, valued if its edges have the attribute given by 'weightattr', and allows selfties if it has self-loops. GraphML hyperedges become 2-mode layers. For 'pajek': vertex ids are always a dense sequence and are used directly as node ids; each vertex's label, if present, is stored in the string node attribute given by 'labelattr' (empty: labels are discarded). Each '*Arcs' section becomes a directed 1-mode layer and each '*Edges' section an undirected one (named from the section's ':k \"name\"' suffix if present, or after the file if it is the only such section); a layer is valued if any of its edges carries a weight column. Pajek .net has no hyperedge concept, so there is no 2-mode layer equivalent, and 'layerattr'/'weightattr'/'idattr' are ignored. Files exported with 'export(...,format=graphml)' or 'export(...,format=pajek)' are imported back into the same layers. The optional 'pack' argument specifies whether layers should be packed after import.";

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
            ImportFormat format = command.GetArgumentParseEnumThrowExceptionIfMissingOrNull<ImportFormat>("format", "arg1");
            string layerAttr = command.GetArgumentParseString("layerattr", "layer");
            string weightAttr = command.GetArgumentParseString("weightattr", "weight");
            string idAttr = command.GetArgumentParseString("idattr", "id");
            string labelAttr = command.GetArgumentParseString("labelattr", "label");
            bool packLayers = command.GetArgumentParseBool("pack", false);
            if (string.IsNullOrWhiteSpace(idAttr))
                return CommandResult.Fail("InvalidArgument", "!Error: 'idattr' can not be empty.");
            var result = FileManager.ImportNetwork(filepath, format, layerAttr, weightAttr, idAttr, labelAttr, packLayers);
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);
            StructureResult structures = result.Value!;
            context.SetVariable(variableName, structures.MainStructure);
            assigned[variableName] = structures.MainStructure.GetType().Name;
            foreach (var kvp in structures.AdditionalStructures)
            {
                string additionalAssignedVariable = variableName + "_" + kvp.Key;
                context.SetVariable(additionalAssignedVariable, kvp.Value);
                assigned[additionalAssignedVariable] = kvp.Value.GetType().Name;
            }
            return CommandResult.Ok(
                $"Imported network '{structures.MainStructure.Name}' from '{filepath}'",
                null,
                assigned
                );
        }
    }
}