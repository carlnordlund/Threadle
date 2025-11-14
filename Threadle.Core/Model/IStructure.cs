using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    /// <summary>
    /// Interface for a structure in Threadle. A structure is characterized as possible to store
    /// in a variable in Threadle, and also that can be loaded from/saved to file.
    /// Implemented by the Network and Nodeset classes.
    /// </summary>
    public interface IStructure
    {
        #region Properties
        /// <summary>
        /// Returns the name of the structure.
        /// </summary>
        string Name { get; set; }
        
        /// <summary>
        /// The filepath of the structure (null if it has not been saved or loaded from file)
        /// </summary>
        string Filepath { get; set; }

        /// <summary>
        /// Indicates whether the structure has been modified since it was loaded or created.
        /// </summary>
        bool IsModified { get; set; }

        /// <summary>
        /// Returns the content of the structure (e.g. used by the View CLI command)
        /// </summary>
        List<string> Content { get; }

        /// <summary>
        /// Returns a dictionary of information about the structure (e.g. used by the Info CLI command)
        /// </summary>
        Dictionary<string,object> Info { get; }
        #endregion
    }
}
