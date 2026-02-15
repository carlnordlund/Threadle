namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Enum with the available file formats, currently only Tsv with optional Gzip
    /// </summary>
    public enum FileFormat
    {
        None,
        Tsv,
        TsvGzip,
        Bin,
        BinGzip
    }
}
