using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.Traceroute
{
    public class TracerouteToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        public string Id => "Traceroute";
        public string DisplayName => "Traceroute Tool";
        public string Description => "Trace the route to a destination host using ICMP with increasing TTL.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new TracerouteToolControl(context);
        }

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

            int hopsAttempted = 0;
            int hopsResponded = 0;
            int hopsTimedOut = 0;
            int hopsErrored = 0;
            bool destinationReached = false;
            string destinationAddress = null;
            var stopwatch = Stopwatch.StartNew();

            using (var ping = new Ping())
            {
                for (int ttl = 1; ttl <= maxHops; ttl++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hopsAttempted++;

                    var options = new PingOptions(ttl, true);
                    var buffer = new byte[32];
                    var hopStart = DateTime.UtcNow;

                    PingReply reply;
                    try
                    {
                        reply = await ping.SendPingAsync(host, timeout, buffer, options);
                    }
                    catch (Exception ex)
                    {
                        hopsErrored++;
                        context.Logger.Error(ex.Message);
                        Console.WriteLine($"{ttl,3}  *  Error: {ex.Message}");
                        continue;
                    }

                    var elapsed = DateTime.UtcNow - hopStart;

                    if (reply.Status == IPStatus.TimedOut)
                    {
                        hopsTimedOut++;
                        Console.WriteLine($"{ttl,3}  *  Request timed out.");
                    }
                    else
                    {
                        hopsResponded++;
                        destinationAddress = reply.Address.ToString();
                        Console.WriteLine(
                            $"{ttl,3}  {reply.Address,-15}  time={elapsed.TotalMilliseconds,6:0.##}ms  status={reply.Status}");

                        if (reply.Status == IPStatus.Success)
                        {
                            destinationReached = true;
                            break;
                        }
                    }
                }
            }

            stopwatch.Stop();
            PrintSummary(host, destinationAddress, hopsAttempted, hopsResponded, hopsTimedOut, hopsErrored, destinationReached, stopwatch.Elapsed);
        }

        private static void PrintSummary(
            string host,
            string destinationAddress,
            int hopsAttempted,
            int hopsResponded,
            int hopsTimedOut,
            int hopsErrored,
            bool destinationReached,
            TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.WriteLine("Trace summary:");
            Console.WriteLine($"    Destination: {host}" +
                              (string.IsNullOrEmpty(destinationAddress) ? string.Empty : $" [{destinationAddress}]"));
            Console.WriteLine($"    Hops attempted: {hopsAttempted}");
            Console.WriteLine($"    Hops responded: {hopsResponded}");
            Console.WriteLine($"    Hops timed out: {hopsTimedOut}");
            Console.WriteLine($"    Hops with errors: {hopsErrored}");
            Console.WriteLine($"    Destination reached: {(destinationReached ? "Yes" : "No")}");
            Console.WriteLine($"    Total elapsed time: {elapsed.TotalSeconds:0.##} second(s).");

            if (destinationReached)
            {
                Console.WriteLine("Trace complete.");
            }
            else
            {
                Console.WriteLine("Trace finished without reaching destination.");
            }
        }
    }
}
