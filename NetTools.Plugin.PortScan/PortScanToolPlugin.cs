using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.PortScan
{
    public class PortScanToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        private static readonly object ConsoleLock = new object();

        public string Id => "PortScan";
        public string DisplayName => "Port Scan Tool";
        public string Description => "Scan a range of TCP ports on a host concurrently.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new PortScanToolControl(context);
        }

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter target host or IP: ");
            var host = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(host))
            {
                Console.WriteLine("Target is required.");
                return;
            }

            Console.Write("Start port (default 1): ");
            var startText = Console.ReadLine();
            int startPort = 1;
            if (!int.TryParse(startText, out startPort) || startPort < 1 || startPort > 65535)
            {
                startPort = 1;
            }

            Console.Write("End port (default 1024): ");
            var endText = Console.ReadLine();
            int endPort = 1024;
            if (!int.TryParse(endText, out endPort) || endPort < startPort || endPort > 65535)
            {
                endPort = 1024;
            }

            Console.Write("Timeout per port ms (default 500): ");
            var timeoutText = Console.ReadLine();
            int timeout = 500;
            if (!int.TryParse(timeoutText, out timeout) || timeout <= 0)
            {
                timeout = 500;
            }

            Console.Write("Max concurrent scans (default 50): ");
            var concurrencyText = Console.ReadLine();
            int maxConcurrency = 50;
            if (!int.TryParse(concurrencyText, out maxConcurrency) || maxConcurrency <= 0)
            {
                maxConcurrency = 50;
            }

            var portCount = endPort - startPort + 1;
            if (maxConcurrency > portCount)
            {
                maxConcurrency = portCount;
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Scanning {host} ports {startPort}-{endPort} (TCP) with up to {maxConcurrency} concurrent connections...");
            Console.WriteLine();

            var openPorts = new List<int>();
            var completedCount = 0;
            var stopwatch = Stopwatch.StartNew();
            var scanTasks = new List<Task>(portCount);

            using (var concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency))
            {
                for (int port = startPort; port <= endPort; port++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int currentPort = port;
                    scanTasks.Add(ScanPortAsync(
                        host,
                        currentPort,
                        timeout,
                        portCount,
                        concurrencyLimiter,
                        openPorts,
                        () => Interlocked.Increment(ref completedCount),
                        cancellationToken));
                }

                await Task.WhenAll(scanTasks);
            }

            stopwatch.Stop();
            Console.WriteLine();

            openPorts.Sort();

            if (openPorts.Count == 0)
            {
                Console.WriteLine("No open ports found.");
            }
            else
            {
                Console.WriteLine($"Found {openPorts.Count} open port(s):");
                foreach (var openPort in openPorts)
                {
                    Console.WriteLine($"  Port {openPort} is OPEN");
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Scan complete. Checked {portCount} port(s) in {stopwatch.Elapsed.TotalSeconds:0.##} second(s).");
        }

        private static async Task ScanPortAsync(
            string host,
            int port,
            int timeout,
            int totalPorts,
            SemaphoreSlim concurrencyLimiter,
            List<int> openPorts,
            Func<int> incrementCompleted,
            CancellationToken cancellationToken)
        {
            await concurrencyLimiter.WaitAsync(cancellationToken);

            try
            {
                if (await IsPortOpenAsync(host, port, timeout, cancellationToken))
                {
                    lock (ConsoleLock)
                    {
                        openPorts.Add(port);
                    }
                }
            }
            finally
            {
                concurrencyLimiter.Release();

                var completed = incrementCompleted();
                lock (ConsoleLock)
                {
                    var percent = (completed * 100) / totalPorts;
                    Console.Write($"\rProgress: {completed}/{totalPorts} ({percent}%)   ");
                }
            }
        }

        private static async Task<bool> IsPortOpenAsync(
            string host,
            int port,
            int timeout,
            CancellationToken cancellationToken)
        {
            using (var client = new TcpClient())
            {
                try
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeout, cancellationToken));

                    if (completedTask != connectTask)
                    {
                        return false;
                    }

                    await connectTask;
                    return client.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}

