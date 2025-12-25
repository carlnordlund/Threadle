using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Utilities;
using System.Globalization;

namespace Threadle.CLIconsole
{
    /// <summary>
    /// Entry class for the CLIconsole application.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Entry point for the CLIconsole application. Parses startup arguments and sets
        /// suitable properties before passing on execution to <see cref="CommandLoop.Run"/>.
        /// </summary>
        /// <param name="args">Arguments passed to the application.</param>
        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            for (int i=0;i<args.Length;i++)
            {
                switch (args[i])
                {
                    case "-s":
                    case "--silent":
                        ConsoleOutput.Verbose = false;
                        break;
                    //case "-e":
                    //case "--endmarker":
                    //    ConsoleOutput.EndMarker = true;
                    //    break;
                    case "-j":
                    case "--json":
                        // INjection here - should I also inject silent etc?
                        CommandLoop.Run(true);
                        break;
                }
            }
            CommandLoop.Run(false);
        }
    }
}
