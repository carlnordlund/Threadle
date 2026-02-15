using Threadle.Core.Model;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class for packaging an IStructure with potentially additional IStructure objects.
    /// Used when loading files that return both a Network object and a Nodeset object.
    /// </summary>
    /// <param name="mainStructure">The main IStructure object.</param>
    /// <param name="additionalStructures">A name-keyed dictionary of potentially additional IStructure objects.</param>
    public class StructureResult(IStructure mainStructure, Dictionary<string, IStructure>? additionalStructures = null)
    {
        #region Properties
        /// <summary>
        /// Returns the main IStructure object.
        /// </summary>
        public IStructure MainStructure { get; } = mainStructure;

        /// <summary>
        /// Returns a dictionary of name-indexed IStructure objects.
        /// </summary>
        public Dictionary<string, IStructure> AdditionalStructures { get; } = additionalStructures ?? [];
        #endregion
    }
}
