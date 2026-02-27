using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetTools.PluginContracts;

namespace NetTools.Plugin.DnsLookup
{
    public class DnsLookupToolPlugin : IConsoleNetworkToolPlugin
    {
        public string Id => "DnsLookup";
        public string DisplayName => "DNS Lookup Tool";
        public string Description => "Resolve hostnames to IP addresses and reverse lookup IPs.";

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter hostname or IP: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Input is required.");
                return;
            }

            Console.WriteLine();

            try
            {
                // برای ساده‌سازی از متدهای سنکرون استفاده و در Task.Run می‌پیچیم
                var hostEntry = await Task.Run(() => Dns.GetHostEntry(input), cancellationToken);

                Console.WriteLine("Host Name: " + hostEntry.HostName);

                if (hostEntry.Aliases != null && hostEntry.Aliases.Length > 0)
                {
                    Console.WriteLine("Aliases:");
                    foreach (var alias in hostEntry.Aliases)
                    {
                        Console.WriteLine("  " + alias);
                    }
                }

                if (hostEntry.AddressList != null && hostEntry.AddressList.Length > 0)
                {
                    Console.WriteLine("Addresses:");
                    foreach (var address in hostEntry.AddressList)
                    {
                        Console.WriteLine("  " + address);
                    }
                }
            }
            catch (Exception ex)
            {
                context.Logger.Error(ex.Message);
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}

