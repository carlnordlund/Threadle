namespace Threadle.Core.Processing.Enums
{
    /// <summary>
    /// Enum to handle various comparisons that can be done
    /// Used by the filter command
    /// </summary>
    public enum SymmetrizeMethod
    {
        max,
        min,
        minnonzero,
        average,
        sum,
        product
    }
}
