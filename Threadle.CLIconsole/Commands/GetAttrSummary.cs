using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class GetAttrSummary : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[str] = getattrsummary(structure = [var:structure], attrname = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates and returns summary statistics for the specified node attribute in the nodeset. Statistics vary by attribute type: (int/float) Mean, Median, StdDev, Min, Max, Q1, Q3; (Bool) Count_true, Count_false, Ratio_true; (Char) Frequency distribution, Mode, Unique_values. All types include Count, Missing, and PercentageWithValue.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetNodesetFromIStructure(context, command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            var summaryResult = Analyses.GetAttributeSummary(nodeset!, attrName);
            return CommandResult.FromOperationResult(summaryResult, summaryResult.Value);
        }
    }
}
