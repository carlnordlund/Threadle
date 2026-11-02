namespace Threadle.Core.Model.Enums
{
    /// <summary>
    /// Declares how the values of a node attribute are stored in a Nodeset.
    /// Sparse: values are stored per node, only for nodes that have the attribute set.
    /// Suitable for attributes that only a subset of nodes have (e.g. register data).
    /// Dense: values are stored in a flat column with one entry per node, plus a presence bitmap.
    /// Suitable for attributes that (almost) every node has (e.g. results of centrality analyses).
    /// </summary>
    public enum NodeAttributeStorage : byte
    {
        Sparse,
        Dense
    }
}
