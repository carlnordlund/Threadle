using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'shortestpaths' CLI command.
    /// </summary>
    public class ShortestPaths : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = shortestpaths(network = [var:network], attrname = [str], *layernames = [semicolon-separated])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the exact shortest path (BFS) between all node pairs and aggregates by node attribute category. Returns a result network whose nodes are the unique attribute values and whose layers hold mean path length ({attrname}_sp_avg), standard error ({attrname}_sp_se), and reachable pair count ({attrname}_sp_count). Unreachable pairs are excluded. Note: runs in O(N×(N+E)) time — only feasible for smaller networks.";

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
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            string layerNames = command.GetArgumentParseString("layernames", "");
            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;

            var result = Core.Analysis.Distance.ShortestPathsNodeAttributeDistances(network, attrName, layers);
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
                $"Shortest paths on attribute '{attrName}' stored in '{structures.MainStructure.Name}'",
                null,
                assigned
            );
        }
    }
}
