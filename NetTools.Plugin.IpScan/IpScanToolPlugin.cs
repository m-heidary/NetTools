using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.IpScan
{
    public class IpScanToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        private static readonly object ConsoleLock = new object();

        public string Id => "IpScan";
        public string DisplayName => "IP Scan";
        public string Description => "Scan an IP range and show host/system details for alive devices.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new IpScanToolControl(context);
        }

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.WriteLine("Examples: 192.168.1.1-192.168.1.254  |  192.168.1.0/24");
            Console.Write("Enter IP range: ");
            var rangeText = Console.ReadLine();

            Console.Write("Timeout ms per host (default 1000): ");
            var timeoutText = Console.ReadLine();
            int timeout = 1000;
            if (!int.TryParse(timeoutText, out timeout) || timeout <= 0)
            {
                timeout = 1000;
            }

            Console.Write("Max concurrent probes (default 50): ");
            var concurrencyText = Console.ReadLine();
            int maxConcurrency = 50;
            if (!int.TryParse(concurrencyText, out maxConcurrency) || maxConcurrency <= 0)
            {
                maxConcurrency = 50;
            }

            Console.Write("Resolve host details? (Y/n): ");
            var detailsText = Console.ReadLine();
            var resolveDetails = !string.Equals(detailsText, "n", StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<IPAddress> addresses;
            try
            {
                addresses = IpScanHelper.ParseIpRange(rangeText);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Invalid range: " + ex.Message);
                return;
            }

            if (addresses.Count == 0)
            {
                Console.WriteLine("No addresses to scan.");
                return;
            }

            if (maxConcurrency > addresses.Count)
            {
                maxConcurrency = addresses.Count;
            }

            Console.WriteLine();
            Console.WriteLine($"Scanning {addresses.Count} address(es) with up to {maxConcurrency} concurrent probes...");
            Console.WriteLine("IP              RTT   TTL  MAC               Host                 NetBIOS          Group            OS");
            Console.WriteLine(new string('-', 120));
            Console.WriteLine();

            var aliveHosts = new List<IpScanHostInfo>();
            var completed = 0;
            var stopwatch = Stopwatch.StartNew();

            using (var limiter = new SemaphoreSlim(maxConcurrency, maxConcurrency))
            {
                var tasks = addresses.Select(address => ProbeAsync(
                    address,
                    timeout,
                    resolveDetails,
                    limiter,
                    aliveHosts,
                    () => Interlocked.Increment(ref completed),
                    addresses.Count,
                    cancellationToken)).ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            stopwatch.Stop();
            Console.WriteLine();
            Console.WriteLine(IpScanHelper.FormatSummary(addresses.Count, aliveHosts.Count, stopwatch.Elapsed));
        }

        private static async Task ProbeAsync(
            IPAddress address,
            int timeout,
            bool resolveDetails,
            SemaphoreSlim limiter,
            List<IpScanHostInfo> aliveHosts,
            Func<int> incrementCompleted,
            int total,
            CancellationToken cancellationToken)
        {
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var info = await IpScanHelper.ProbeHostAsync(address, timeout, resolveDetails, cancellationToken)
                    .ConfigureAwait(false);

                if (info.IsAlive)
                {
                    lock (aliveHosts)
                    {
                        aliveHosts.Add(info);
                    }

                    var line = IpScanHelper.FormatHostLine(info);
                    if (line != null)
                    {
                        lock (ConsoleLock)
                        {
                            Console.WriteLine(line);
                        }
                    }
                }
            }
            finally
            {
                limiter.Release();
                var done = incrementCompleted();
                lock (ConsoleLock)
                {
                    Console.Write($"\rProgress: {done}/{total} ({(done * 100) / total}%)   ");
                }
            }
        }
    }
}
