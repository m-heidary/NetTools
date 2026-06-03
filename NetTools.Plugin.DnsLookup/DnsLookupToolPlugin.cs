using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.DnsLookup
{
    public class DnsLookupToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        public string Id => "DnsLookup";
        public string DisplayName => "DNS Lookup Tool";
        public string Description => "Resolve hostnames to IP addresses and reverse lookup IPs.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new DnsLookupToolControl(context);
        }

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
            Console.WriteLine($"Looking up '{input}'...");
            Console.WriteLine();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var hostEntry = await Task.Run(() => Dns.GetHostEntry(input), cancellationToken);
                stopwatch.Stop();

                var ipv4Addresses = new List<string>();
                var ipv6Addresses = new List<string>();

                if (hostEntry.AddressList != null)
                {
                    foreach (var address in hostEntry.AddressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipv4Addresses.Add(address.ToString());
                        }
                        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            ipv6Addresses.Add(address.ToString());
                        }
                    }
                }

                Console.WriteLine("Host Name: " + hostEntry.HostName);

                if (hostEntry.Aliases != null && hostEntry.Aliases.Length > 0)
                {
                    Console.WriteLine("Aliases:");
                    foreach (var alias in hostEntry.Aliases)
                    {
                        Console.WriteLine("  " + alias);
                    }
                }

                if (ipv4Addresses.Count > 0)
                {
                    Console.WriteLine("IPv4 Addresses:");
                    foreach (var address in ipv4Addresses)
                    {
                        Console.WriteLine("  " + address);
                    }
                }

                if (ipv6Addresses.Count > 0)
                {
                    Console.WriteLine("IPv6 Addresses:");
                    foreach (var address in ipv6Addresses)
                    {
                        Console.WriteLine("  " + address);
                    }
                }

                if (ipv4Addresses.Count == 0 && ipv6Addresses.Count == 0)
                {
                    Console.WriteLine("No addresses returned.");
                }

                PrintSummary(
                    input,
                    hostEntry.HostName,
                    hostEntry.Aliases?.Length ?? 0,
                    ipv4Addresses.Count,
                    ipv6Addresses.Count,
                    stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                context.Logger.Error(ex.Message);
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine();
                Console.WriteLine($"Lookup failed after {stopwatch.Elapsed.TotalMilliseconds:0.##} ms.");
            }
        }

        private static void PrintSummary(
            string input,
            string hostName,
            int aliasCount,
            int ipv4Count,
            int ipv6Count,
            TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.WriteLine("Lookup summary:");
            Console.WriteLine($"    Query: {input}");
            Console.WriteLine($"    Resolved host: {hostName}");
            Console.WriteLine($"    Aliases: {aliasCount}");
            Console.WriteLine($"    IPv4 addresses: {ipv4Count}");
            Console.WriteLine($"    IPv6 addresses: {ipv6Count}");
            Console.WriteLine($"    Total addresses: {ipv4Count + ipv6Count}");
            Console.WriteLine($"    Lookup time: {elapsed.TotalMilliseconds:0.##} ms");
        }
    }
}
