using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'rwed' CLI command.
    /// </summary>
    public class RandomWalkerExposureDistances : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = rwed(network = [var:network], attrname = [str], maxsteps = [int], *layernames = [semicolon-separated], *walkfactor = [float(default=1.0)], *minpairobs = [int(default=10)], *balanced = ['false'(default),'true'], *weighted = ['false'(default),'true'], *returnhistograms = ['false'(default),'true'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Initializes and executes an exposure-distance random walker (experimental): a generalization of 'rwfpt' that records the first-passage time to every individual node a walk discovers (not just the first node of each attribute category), bucketed by that node's category. A single walk can therefore contribute several observations per (source category, target category) pair, giving a fuller exposure/penetration distribution rather than just the nearest-instance distance. Coverage is tracked separately from the distance distribution, so a walk discovering several nodes of the same target category still only counts as one covered walk. Unlike 'rwfpt', walks always run the full 'maxsteps' budget (no early exit). The initial sampling pass runs in parallel across up to 'maxthreads' threads (see 'setting()'); exact reproducibility via 'randomseed()' is only guaranteed when maxthreads is set to 1.";

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
            int minPairObs = command.GetArgumentParseInt("minpairobs", 10);
            bool balanced = command.GetArgumentParseBool("balanced", false);
            bool weighted = command.GetArgumentParseBool("weighted", false);
            bool returnHistograms = command.GetArgumentParseBool("returnhistograms", false);

            var (result, histograms) = Core.Analysis.Distance.RandomWalkNodeAttributeExposureDistances(network, attrName, maxSteps, layers, walkfactor, minPairObs, balanced, weighted, returnHistograms);
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

            object? payload = null;
            if (histograms != null)
            {
                var histPayload = new List<object>(histograms.Count);
                foreach (var h in histograms)
                    histPayload.Add(new { from = h.From, to = h.To, step = h.Step, count = h.Count });
                payload = histPayload;
            }
            return CommandResult.Ok(
                $"Random-walker-derived exposure distances on attribute '{attrName}' stored in '{structures.MainStructure.Name}'",
                payload,
                assigned
                );

        }
    }
}