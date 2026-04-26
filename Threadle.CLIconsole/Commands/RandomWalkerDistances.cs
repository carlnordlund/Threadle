using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class RandomWalkerDistances : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = rwdistances(network = [var:network], attrname = [str], maxsteps = [int], *layernames = [semicolon-separated], *walkfactor = [float(default=1.0)], *balanced = ['false'(default),'true'], *weighted = ['false'(default),'true'], *backtrack = ['false'(default),'true'], *savesteps = ['false'(default),'true'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Initializes and executes a distance-measuring random walker (experimental).";

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
            int maxSteps = command.GetArgumentParseIntThrowExceptionIfMissingOrNull("maxsteps", "arg2");
            string layerNames = command.GetArgumentParseString("layernames", "");
            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;
            float walkfactor = command.GetArgumentParseFloat("walkfactor", 1.0f);
            bool balanced = command.GetArgumentParseBool("balanced", false);
            bool weighted = command.GetArgumentParseBool("weighted", false);
            bool backtrack = command.GetArgumentParseBool("backtrack", false);
            bool savesteps = command.GetArgumentParseBool("savesteps", false);

            var result = Core.Analysis.Distance.RandomWalkNodeAttributeDistances(network, attrName, maxSteps, layers, walkfactor, balanced, weighted, backtrack, savesteps);
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
                $"Random-walker-derived distances on attribute '{attrName}' stored in '{structures.MainStructure.Name}'",
                null,
                assigned
                );
        }
    }
}
