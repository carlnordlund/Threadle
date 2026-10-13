namespace Threadle.Core.Model
{
    /// <summary>
    /// Column-oriented table returned by <see cref="Network.GetAlterDetails"/>, with one row per ego, alter,
    /// layer and shared affiliation. All columns have the same length.
    /// </summary>
    public class AlterDetails
    {
        #region Properties
        /// <summary>
        /// The queried (ego) node of each row.
        /// </summary>
        public List<uint> Ego { get; } = [];

        /// <summary>
        /// The alter node of each row.
        /// </summary>
        public List<uint> Alter { get; } = [];

        /// <summary>
        /// The name of the layer where ego and alter are connected.
        /// </summary>
        public List<string> Layer { get; } = [];

        /// <summary>
        /// The name of the affiliation (hyperedge) that ego and alter share in a 2-mode layer; null for 1-mode layers.
        /// </summary>
        public List<string?> Hyperedge { get; } = [];

        /// <summary>
        /// The number of nodes affiliated to that hyperedge (including ego and alter); 0 for 1-mode layers.
        /// </summary>
        public List<int> Hypersize { get; } = [];

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int NbrRows => Ego.Count;
        #endregion


        #region Methods
        /// <summary>
        /// Adds a row to the table.
        /// </summary>
        internal void AddRow(uint ego, uint alter, string layer, string? hyperedge, int hypersize)
        {
            Ego.Add(ego);
            Alter.Add(alter);
            Layer.Add(layer);
            Hyperedge.Add(hyperedge);
            Hypersize.Add(hypersize);
        }
        #endregion
    }
}
