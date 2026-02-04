using Threadle.Core.Utilities;
using System.Globalization;
using Threadle.CLIconsole.Runtime;

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
            bool json = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-s":
                    case "--silent":
                        CLISettings.Verbose = false;
                        break;
                    case "-j":
                    case "--json":
                        json = true;
                        break;
                }
            }
            CommandLoop.Run(json);
        }
    }
}
