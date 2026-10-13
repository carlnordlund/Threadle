namespace Threadle.CLIconsole.Results
{
    /// <summary>
    /// A column-oriented table payload: a string-object dictionary where each key is a column name and each
    /// value is an array with one value per row (all arrays having the same length). Serialized to JSON as an
    /// ordinary dictionary of arrays, but recognized by the <see cref="TextCommandResultRenderer"/> so that it
    /// can be displayed row by row.
    /// </summary>
    public sealed class ColumnTable : Dictionary<string, object>
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnTable"/> class, with the columns of the provided dictionary.
        /// </summary>
        /// <param name="columns">Dictionary with column names and arrays of column values.</param>
        public ColumnTable(IDictionary<string, object> columns) : base(columns) { }
        #endregion
    }
}
