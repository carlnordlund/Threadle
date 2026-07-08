namespace Threadle.Core.Processing.Enums
{
    /// <summary>
    /// Enum specifying how two 1-mode layers' edge values are combined when merged into a new layer.
    /// Used by the mergelayers command. The method fully determines the resulting layer's edge type:
    /// And/Or/Xor always produce a binary layer; Sum/Average always produce a valued layer;
    /// Max/Min/Product produce a binary layer only if both input layers are binary, valued otherwise.
    /// </summary>
    public enum MergeMethod
    {
        And,
        Or,
        Xor,
        Sum,
        Average,
        Max,
        Min,
        Product
    }
}