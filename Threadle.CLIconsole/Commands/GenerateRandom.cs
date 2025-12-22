using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'generate' CLI command.
    /// </summary>
    public class GenerateRandom : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = generate(type = ['er'], size = [int], p = [double], *directed = ['true'(default),'false'], *selfties = ['true','false'(default)], *newname = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates a random network of the specified type (only Erdös-Renyi implemented so far) of specified size and tie probability (also density). The network is by default directed without selfties but that can be adjusted. Is automatically named but can be given a name with the optional parameter. The network is stored with the assigned variable name, and a nodeset is also stored using the same variable name plus the appendix '_nodeset'.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => true;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(Command command, CommandContext context)
        {
            string variableName = command.CheckAndGetAssignmentVariableName();
            string type = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg0");
            int size = command.GetArgumentParseIntThrowExceptionIfMissingOrNull("size", "arg1");
            double p = command.GetArgumentParseDoubleThrowExceptionIfMissingOrNull("p", "arg2");
            EdgeDirectionality directionality = command.GetArgumentParseBool("directed", true) ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected;
            bool selfties = command.GetArgumentParseBool("selfties", false);
            if (!(command.GetArgument("newname") is string newName))
                newName = context.GetNextIncrementalName($"{type}_s{size}_p{p}");
            StructureResult structures = NetworkGenerators.ErdosRenyi(size, p, directionality, selfties);
            context.SetVariable(variableName, structures.MainStructure);
            //ConsoleOutput.WriteLine($"Network '{newName}' generated and stored in variable '{variableName}'.");
            if (structures.AdditionalStructures.TryGetValue("nodeset", out var nodeset))
            {
                string nodeset_variableName = variableName + "_nodeset";
                context.SetVariable(nodeset_variableName, nodeset);
                //ConsoleOutput.WriteLine($"Nodeset '{nodeset.Name}' generated and stored in variable '{nodeset_variableName}'.");
                return CommandResult.Ok(
                    message: $"Generated random network of type '{type}'.",
                    null,
                    assignments: new Dictionary<string,string>
                    {
                        [variableName] = nameof(Network),
                        [nodeset_variableName] = nameof(Nodeset)
                    }
                    );
            }
            return CommandResult.Ok(
                message: $"Generated random network of type '{type}'.",
                null,
                assignments: new Dictionary<string, string>
                {
                    [variableName] = nameof(Network)
                }
                );

            //        return CommandResult.Ok(
            //message: $"Network '{nameNetwork}' created.",
            //assignments: CommandResult.Assigning(variableName, typeof(Network))
            //);

        }
    }
}
