using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NetTools.PluginContracts;

namespace NetTools.Plugin.Traceroute
{
    public class TracerouteToolPlugin : IConsoleNetworkToolPlugin
    {
        public string Id => "Traceroute";
        public string DisplayName => "Traceroute Tool";
        public string Description => "Trace the route to a destination host using ICMP with increasing TTL.";

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter destination host or IP: ");
            var host = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(host))
            {
                Console.WriteLine("Destination is required.");
                return;
            }

            Console.Write("Max hops (default 30): ");
            var maxHopsText = Console.ReadLine();
            int maxHops = 30;
            if (!int.TryParse(maxHopsText, out maxHops) || maxHops <= 0)
            {
                maxHops = 30;
            }

            Console.Write("Timeout per hop ms (default 4000): ");
            var timeoutText = Console.ReadLine();
            int timeout = 4000;
            if (!int.TryParse(timeoutText, out timeout) || timeout <= 0)
            {
                timeout = 4000;
            }

            Console.WriteLine();
            Console.WriteLine($"Tracing route to {host} over a maximum of {maxHops} hops:");
            Console.WriteLine();

            using (var ping = new Ping())
            {
                for (int ttl = 1; ttl <= maxHops; ttl++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var options = new PingOptions(ttl, true);
                    var buffer = new byte[32];
                    var startTime = DateTime.UtcNow;

                    PingReply reply;
                    try
                    {
                        reply = await ping.SendPingAsync(host, timeout, buffer, options);
                    }
                    catch (Exception ex)
                    {
                        context.Logger.Error(ex.Message);
                        Console.WriteLine($"{ttl,3}  *  Error: {ex.Message}");
                        continue;
                    }

                    var elapsed = DateTime.UtcNow - startTime;

                    if (reply.Status == IPStatus.TimedOut)
                    {
                        Console.WriteLine($"{ttl,3}  *  Request timed out.");
                    }
                    else
                    {
                        var address = reply.Address;
                        Console.WriteLine(
                            $"{ttl,3}  {address}  time={elapsed.TotalMilliseconds:0.##}ms  status={reply.Status}");

                        if (reply.Status == IPStatus.Success)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Trace complete.");
                            break;
                        }
                    }
                }
            }
        }
    }
}

