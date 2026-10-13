using System.Collections;

namespace Threadle.CLIconsole.Results
{
    /// <summary>
    /// A column-oriented table payload: column name to an array of values, all of the same length.
    /// Serialized to JSON as one array per column (so that e.g. R can turn it into a data frame directly),
    /// and rendered by the text console as rows of tab-separated values under a header.
    /// </summary>
    public sealed class TablePayload : Dictionary<string, object>
    {
        /// <summary>
        /// Gets the number of rows in the table.
        /// </summary>
        public int NbrRows => Count == 0 ? 0 : ((IList)Values.First()).Count;

        /// <summary>
        /// Adds a column to the table. Columns are kept in the order they are added.
        /// </summary>
        /// <param name="name">The name of the column.</param>
        /// <param name="values">The values of the column.</param>
        public TablePayload AddColumn(string name, IList values)
        {
            Add(name, values);
            return this;
        }
    }
}
