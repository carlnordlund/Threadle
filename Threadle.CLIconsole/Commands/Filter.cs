using Threadle.Core.Model;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'filter' CLI command.
    /// </summary>
    public class Filter : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:nodeset] = filter(nodeset = [var:nodeset2], attrname = [str], cond=['eq','ne','gt','lt','ge','le','isnull','notnull'], +attrvalue = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates and stores a new nodeset [var:nodeset] containing all the nodes in [var:nodeset2] that fulfills the specified condition 'cond' concerning the specified attribute 'attrname' and the reference value 'attrvalue'. The new nodeset and its nodes and attributes constitute a partial deep copy of the inbound nodeset, making them completely independent from each other. Note: when checking for 'isnull' or 'notnull', the 'attrvalue' argument can be ignored.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => true;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            string variableName = command.GetAssignmentVariableNameThrowExceptionIfNull();

            //string nodesetName = command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0");
            if (CommandHelpers.TryGetVariable<Nodeset>(context, command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            ConditionType conditionType = command.GetArgumentParseEnumThrowExceptionIfMissingOrNull<ConditionType>("cond", "arg2");
            string attributeValue = (conditionType == ConditionType.isnull || conditionType == ConditionType.notnull) ? "" : command.GetArgumentThrowExceptionIfMissingOrNull("attrvalue", "arg3");
            OperationResult<Nodeset> result = NodesetProcessor.Filter(nodeset, attributeName, conditionType, attributeValue);
            // Not sure that this works!
            return CommandResult.FromOperationResult(
                opResult: result,
                payload: null,
                assignments: CommandResult.Assigning(variableName, typeof(Nodeset))
                );

            //if (!result.Success)
            //    return CommandResult.Fail(result.Code,result.Message);
            //return CommandResult.Ok(
            //    message: result.Message,
            //    assignments: CommandResult.Assigning(variableName, typeof(Nodeset))
            //    );
        }
    }
}
