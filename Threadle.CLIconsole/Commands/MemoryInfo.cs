using System.Diagnostics;
using System.Runtime;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'memoryinfo' CLI command.
    /// </summary>
    public class MemoryInfo : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "memoryinfo(*forcegc=['false'(default),'true'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Reports current memory usage: ManagedMemoryBytes (GC.GetTotalMemory - bytes currently allocated by live managed objects), GCHeapSizeBytes (GC.GetGCMemoryInfo().HeapSizeBytes - total committed managed heap, including generational/fragmentation overhead), and WorkingSetBytes (the process's OS-reported resident memory, i.e. what an external process inspector sees). Passive by default (forcegc=false), same as systeminfo(). When forcegc=true, first runs a blocking, compacting Gen2 garbage collection (GC.Collect(MaxGeneration, Aggressive, blocking: true, compacting: true) plus GC.WaitForPendingFinalizers()) before measuring, to get an accurate snapshot - the .NET GC doesn't reliably return freed pages to the OS otherwise, so WorkingSetBytes can otherwise sit well above what's actually live. This is not cheap: a full compacting collection can pause the process noticeably on a large heap, so forcegc=true should be used deliberately (e.g. for benchmarking), not in a tight loop.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            bool forceGc = command.GetArgumentParseBool("forcegc", false);
            if (forceGc)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }

            var info = new Dictionary<string, object>
            {
                ["ForcedGc"] = forceGc,
                ["GCSettings"] = System.Runtime.GCSettings.IsServerGC,
                ["ManagedMemoryBytes"] = GC.GetTotalMemory(false),
                ["GCHeapSizeBytes"] = GC.GetGCMemoryInfo().HeapSizeBytes,
                ["WorkingSetBytes"] = Process.GetCurrentProcess().WorkingSet64
            };

            return CommandResult.Ok(
                forceGc
                    ? "Forced a full garbage collection before measuring memory usage."
                    : "Reporting current memory usage (no garbage collection forced - pass forcegc=true for an accurate snapshot).",
                payload: info
            );
        }
    }
}