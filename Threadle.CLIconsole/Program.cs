using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Utilities;
using System.Globalization;

namespace Threadle.CLIconsole
{
    class Program
    {
        static void Main(string[] args)
        {
            // Override any localized number and UI formats to the generic
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            //bool verbose = true, endmarker = false;
            for (int i=0;i<args.Length;i++)
            {
                switch (args[i])
                {
                    case "-s":
                    case "--silent":
                        ConsoleOutput.Verbose = false;
                        break;
                    case "-e":
                    case "--endmarker":
                        ConsoleOutput.EndMarker = true;
                        break;
                }
            }
            CommandLoop.Run();
        }
    }
}
