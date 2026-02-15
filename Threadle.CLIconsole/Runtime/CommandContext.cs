using Threadle.CLIconsole.Results;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Runtime
{
    /// <summary>
    /// Represents the variable memory for a CLI console instance and various
    /// methods to get, add, and remove structures stored in this.
    /// </summary>
    public class CommandContext
    {
        #region Properties
        /// <summary>
        /// A dictionary with the variables currently stored in the console variable memory, stored
        /// by the variable names that these structures were assigned to.
        /// </summary>
        public Dictionary<string, IStructure> Variables { get; } = new();
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Returns a dictionary with metadata about the structures currently stored in the
        /// console variable memory.
        /// </summary>
        /// <returns>A <see cref="Dictionary{string, object}"/> with metadata about each stored structure.</returns>
        internal Dictionary<string, object> VariablesMetadata()
        {
            Dictionary<string, object> metadata = new();
            foreach (var kvp in Variables)
                metadata[kvp.Key] = kvp.Value.GetType().Name;
            return metadata;
        }

        /// <summary>
        /// Stores the provided <paramref name="value"/> <see cref="IStructure"/> in the
        /// console variable memory under the provided variable <paramref name="name"/>.
        /// </summary>
        /// <remarks>Note: this will overwrite an existing stored variable that has the same name.</remarks>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The <see cref="IStructure"/> to store.</param>
        internal void SetVariable(string name, IStructure value)
        {
            Variables[name] = value;
        }

        /// <summary>
        /// Removes the structure and variable stored in the console variable memory with the provided <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The variable´name.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        //internal OperationResult DeleteStructure(string name)
        internal CommandResult DeleteStructure(string name)
        {
            if (!Variables.TryGetValue(name, out var structure))
                return CommandResult.Fail("StructureNotFound", $"Structure {name} not found.");
            //return OperationResult.Fail("StructureNotFound", $"Structure {name} not found.");
            if (structure is Nodeset nodeset)
            {
                foreach (var kvp in Variables)
                    if (kvp.Value is Network network && network.Nodeset == nodeset)
                        return CommandResult.Fail("ConstraintNodesetInUse", $"Can not delete nodeset '{name}': used by network '{kvp.Key}'.");
            }
            Variables.Remove(name);
            return CommandResult.Ok($"Structure '{name}' removed.");
            //return OperationResult.Ok($"Structure '{name}' removed.");
        }


        /// <summary>
        /// Removes all variables and structures from the console variable memory.
        /// </summary>
        internal CommandResult DeleteAllStructures()
        {
            Variables.Clear();
            return CommandResult.Ok("All structures removed.");
        }

        /// <summary>
        /// Tries to get an object of type T from the console variable memory. Returns true if successful, false otherwise.
        /// The actual object is returned via the out parameter <paramref name="value"/>. Note that if the method returns false, the value of the out parameter is null.
        /// </summary>
        /// <typeparam name="T">The type of object to get.</typeparam>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The return object of type T, null if no such object exists.</param>
        /// <returns>True if successful, otherwise false.</returns>
        internal bool TryGetVariable<T>(string name, out T value) where T : class
        {
            if (Variables.TryGetValue(name, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = null!;
            return false;
        }

        /// <summary>
        /// Returns the next available variable name, either based on a provided <paramref name="baseName"/> or following the Untitled-
        /// pattern. If there is no stored variable with the 'basename', that is returned immediately. If there is already a stored
        /// variable with that name, the postfix '-1' is added to the end of the basename and it is tried again, possibly increasing
        /// the number if that is also occupied. This continues until the first free 'basename-N' variable is found.
        /// </summary>
        /// <param name="baseName">The basename to start with (optional; defaults to 'Untitled-').</param>
        /// <returns>Returns an available variable name based on the current basename.</returns>
        internal string GetNextIncrementalName(string baseName = "Untitled-")
        {
            if (!Variables.ContainsKey(baseName))
                return baseName;
            int i = 0;
            while (true)
            {
                if (!Variables.ContainsKey(baseName + i))
                    return "Untitled-" + i;
                i++;
            }
        }

        /// <summary>
        /// Returns a collection of all <see cref="Network"/> objects that refers to the provided <see cref="Nodeset"/> object.
        /// Used by <see cref="Commands.RemoveNode"/> to make sure that the removal of a node also removes related edges in all
        /// networks that use this particular <see cref="Nodeset"/>.
        /// </summary>
        /// <param name="nodeset">The <see cref="Nodeset"/> object.</param>
        /// <returns>A collection of all <see cref="Network"/> objects that refers to the provided <see cref="Nodeset"/>.</returns>
        internal IEnumerable<Network> GetNetworksUsingNodeset(Nodeset nodeset)
        {
            return Variables.Values.OfType<Network>().Where(net => ReferenceEquals(net.Nodeset, nodeset));
        }
        #endregion
    }
}
