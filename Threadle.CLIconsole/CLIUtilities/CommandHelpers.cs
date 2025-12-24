using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.CLIUtilities
{
    /// <summary>
    /// Why not place this in CommandContext, where it has access to the context,
    /// so there is no need to provide it? Is it because I don't want CommandResult
    /// referred to inside CommandContext?
    /// </summary>
    internal static class CommandHelpers
    {
        /// <summary>
        /// Helper function for getting a specific structure from the variable space of a provided
        /// context. Delivers the specific structure to the outbound value parameter.
        /// Returns a CommandResult if no such structure is found.
        /// </summary>
        /// <typeparam name="T">The type of structure to look for (should be either a Network or Nodeset)</typeparam>
        /// <param name="context">The CommandContext</param>
        /// <param name="name">The variable name of the structure</param>
        /// <param name="value">The outbound structure (will be null if failed)</param>
        /// <returns>Returns a <see cref="CommandResult"/> object if it failed.</returns>
        public static CommandResult? TryGetVariable<T>(CommandContext context, string name, out T value) where T : class
        {
            if (!context.TryGetVariable<T>(name, out value))
            {
                return CommandResult.Fail(
                    "VariableNotFound",
                    $"No {typeof(T).Name} named '{name}' found."
                );
            }
            return null;
        }

        /// <summary>
        /// Helper function to get a Nodeset from a CommandContext variable space, given the name
        /// of a structure (which can be either a Nodeset or a Network variable).
        /// The Nodeset is delivered as an out parameter.
        /// Will return a CommandResult.Fail if no such variable exists, or if this structure
        /// does not contain or refer to a Nodeset.
        /// </summary>
        /// <param name="context">The CommandContext</param>
        /// <param name="name">The variable name of the structure</param>
        /// <param name="nodeset">The outbound Nodeset (will be null if failed)</param>
        /// <returns>Returns a <see cref="CommandResult"/> object if it failed.</returns>
        public static CommandResult? TryGetNodeset(CommandContext context, string name, out Nodeset? nodeset)
        {
            if (!context.TryGetVariable<IStructure>(name, out IStructure structure))
            {
                nodeset = null;
                return CommandResult.Fail(
                    "VariableNotFound",
                    $"No structure named '{name}' found."
                    );
            }
            nodeset = (structure is Nodeset) ? (Nodeset)structure : (structure is Network) ? ((Network)structure).Nodeset : null;
            if (nodeset == null)
                return CommandResult.Fail("NoNodeset", $"No Nodeset found in structure named '{name}'.");
            return null;
        }
    }
}
