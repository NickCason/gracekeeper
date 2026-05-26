using System;
using System.Threading;
using System.Threading.Tasks;
using GraceKeeper.Core;

namespace GraceKeeper.Cleaner;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var mode = ParseMode(args);
        if (mode == null)
        {
            Console.Error.WriteLine("Usage: GraceKeeper.Cleaner.exe --mode {boot|safety-net}");
            return 2;
        }

        var clock = new SystemClock();
        var logWriter = new CleanerLogWriter(PathResolver.CleanerLogPath, clock);
        var disabledSentinel = new DisabledSentinel(PathResolver.DisabledSentinelPath);

        if (disabledSentinel.IsDisabled)
        {
            logWriter.WriteSkipped("disabled");
            return 0;
        }

        if (mode == CleanupMode.SafetyNet)
        {
            var gate = new SafetyNetGate(PathResolver.CleanerLogPath, clock, TimeSpan.FromHours(11));
            if (gate.ShouldSkip())
            {
                logWriter.WriteSkipped("safety-net: recent run < 11h ago");
                return 0;
            }
        }

        try
        {
            var probe = new EchoControllerProbe(new WmiProcessTreeReader());
            var bouncer = new ServiceBouncer(
                new Win32ServiceController(),
                new[]
                {
                    "FactoryTalk Logix Echo Message Broker",
                    "FactoryTalk Logix Echo Service",
                    "FactoryTalk Activation Service",
                    "FTActivationBoost"
                },
                TimeSpan.FromSeconds(10));
            var cleaner = new RnlCleaner(PathResolver.RnlTargetDir, probe, bouncer);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(14));
            var result = await cleaner.RunAsync(mode.Value, cts.Token);
            logWriter.WriteResult(result);
            return 0;
        }
        catch (Exception ex)
        {
            logWriter.WriteFailed(ex.GetType().Name, ex.Message);
            return 1;
        }
    }

    private static CleanupMode? ParseMode(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "--mode") continue;
            return args[i + 1].ToLowerInvariant() switch
            {
                "boot" => CleanupMode.Boot,
                "safety-net" => CleanupMode.SafetyNet,
                _ => null
            };
        }
        return null;
    }
}
