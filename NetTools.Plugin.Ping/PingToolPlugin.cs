using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.Ping
{
    public class PingToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        public string Id => "Ping";
        public string DisplayName => "Ping Tool";
        public string Description => "Send ICMP echo requests to a host.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new PingToolControl(context);
        }

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter host or IP: ");
            var host = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(host))
            {
                Console.WriteLine("Host is required.");
                return;
            }

            Console.Write("Count (default 4): ");
            var countText = Console.ReadLine();
            int count = 4;
            if (!int.TryParse(countText, out count) || count <= 0)
            {
                count = 4;
            }

            Console.Write("Timeout ms (default 4000): ");
            var timeoutText = Console.ReadLine();
            int timeout = 4000;
            if (!int.TryParse(timeoutText, out timeout) || timeout <= 0)
            {
                timeout = 4000;
            }

            Console.WriteLine();
            Console.WriteLine($"Pinging {host} with {count} request(s), timeout {timeout} ms:");
            Console.WriteLine();

            int sent = 0;
            int received = 0;
            int failed = 0;
            var roundtripTimes = new List<long>();
            string resolvedAddress = null;
            var stopwatch = Stopwatch.StartNew();

            using (var ping = new System.Net.NetworkInformation.Ping())
            {
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sent++;

                    try
                    {
                        var reply = await ping.SendPingAsync(host, timeout);
                        if (reply.Status == IPStatus.Success)
                        {
                            received++;
                            roundtripTimes.Add(reply.RoundtripTime);
                            resolvedAddress = reply.Address.ToString();

                            Console.WriteLine(
                                $"Reply from {reply.Address} " +
                                $"bytes={reply.Buffer?.Length ?? 0} " +
                                $"time={reply.RoundtripTime}ms " +
                                $"ttl={reply.Options?.Ttl ?? 0} " +
                                $"seq={i + 1}");
                        }
                        else

                        {
                            failed++;
                            Console.WriteLine($"Request {i + 1} failed: {reply.Status}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        context.Logger.Error(ex.Message);
                        Console.WriteLine($"Request {i + 1} error: {ex.Message}");
                    }

                    if (i < count - 1)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }

            stopwatch.Stop();
            PrintStatistics(host, resolvedAddress, sent, received, failed, roundtripTimes, stopwatch.Elapsed);
        }

        private static void PrintStatistics(
            string host,
            string resolvedAddress,
            int sent,
            int received,
            int failed,
            List<long> roundtripTimes,
            TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.WriteLine($"Ping statistics for {host}" +
                              (string.IsNullOrEmpty(resolvedAddress) ? string.Empty : $" [{resolvedAddress}]") +
                              ":");

            var lossPercent = sent == 0 ? 0 : (failed * 100.0) / sent;
            Console.WriteLine(
                $"    Packets: Sent = {sent}, Received = {received}, Lost = {failed} ({lossPercent:0.#}% loss),");

            if (roundtripTimes.Count > 0)
            {
                long min = roundtripTimes[0];
                long max = roundtripTimes[0];
                long total = 0;

                foreach (var time in roundtripTimes)
                {
                    if (time < min)
                    {
                        min = time;
                    }

                    if (time > max)
                    {
                        max = time;
                    }

                    total += time;
                }

                var average = (double)total / roundtripTimes.Count;
                Console.WriteLine("Approximate round trip times in milli-seconds:");
                Console.WriteLine(
                    $"    Minimum = {min}ms, Maximum = {max}ms, Average = {average:0.#}ms");
            }
            else
            {
                Console.WriteLine("Approximate round trip times in milli-seconds:");
                Console.WriteLine("    No successful replies.");
            }

            Console.WriteLine($"Total elapsed time: {elapsed.TotalSeconds:0.##} second(s).");
        }
    }
}
