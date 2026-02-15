namespace Threadle.Core.Utilities
{
    internal static class TextFileReader
    {
        internal static string[] LoadFile(string filepath)
        {
            return File.ReadAllLines(filepath);
        }
    }
}
