using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NetTools.PluginContracts;

namespace NetTools.Plugin.Ping
{
    public class PingToolPlugin : IConsoleNetworkToolPlugin
    {
        public string Id => "Ping";
        public string DisplayName => "Ping Tool";
        public string Description => "Send ICMP echo requests to a host.";

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

            Console.WriteLine();

            using (var ping = new System.Net.NetworkInformation.Ping())
            {
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var reply = await ping.SendPingAsync(host, 4000);
                        if (reply.Status == IPStatus.Success)
                        {
                            Console.WriteLine(
                                $"Reply from {reply.Address} " +
                                $"time={reply.RoundtripTime}ms " +
                                $"ttl={reply.Options?.Ttl}");
                        }
                        else
                        {
                            Console.WriteLine($"Request failed: {reply.Status}");
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Logger.Error(ex.Message);
                        Console.WriteLine("Error: " + ex.Message);
                    }

                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
    }
}

