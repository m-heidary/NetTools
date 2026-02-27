using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetTools.PluginContracts;

namespace NetTools.Plugin.PortScan
{
    public class PortScanToolPlugin : IConsoleNetworkToolPlugin
    {
        public string Id => "PortScan";
        public string DisplayName => "Port Scan Tool";
        public string Description => "Scan a range of TCP ports on a host.";

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

            Console.WriteLine();
            Console.WriteLine($"Scanning {host} ports {startPort}-{endPort} (TCP)...");
            Console.WriteLine("This may take some time.\n");

            for (int port = startPort; port <= endPort; port++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool isOpen = await IsPortOpenAsync(host, port, timeout, cancellationToken);

                if (isOpen)
                {
                    Console.WriteLine($"Port {port} is OPEN");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Scan complete.");
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

                    // Propagate exceptions if any
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

