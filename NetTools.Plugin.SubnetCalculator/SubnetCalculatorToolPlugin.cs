using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.SubnetCalculator
{
    public class SubnetCalculatorToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        public string Id => "SubnetCalculator";
        public string DisplayName => "Subnet Calculator";
        public string Description => "Calculate subnet details for an IPv4 address and CIDR prefix.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new SubnetCalculatorToolControl(context);
        }

        public Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter IPv4 address: ");
            var ipText = Console.ReadLine();

            Console.Write("Enter CIDR prefix [0-32]: ");
            var prefixText = Console.ReadLine();

            int prefixLength;
            if (!int.TryParse(prefixText, out prefixLength))
            {
                Console.WriteLine("Invalid prefix length.");
                return Task.CompletedTask;
            }

            SubnetCalculationResult result;
            if (!SubnetCalculatorToolControl.TryCalculate(ipText, prefixLength, out result))
            {
                Console.WriteLine("Invalid IPv4 address or prefix.");
                return Task.CompletedTask;
            }

            PrintResult(result);
            return Task.CompletedTask;
        }

        private static void PrintResult(SubnetCalculationResult result)
        {
            Console.WriteLine("IP Address: " + result.InputAddress);
            Console.WriteLine("CIDR: /" + result.PrefixLength);
            Console.WriteLine("Subnet Mask: " + result.SubnetMask);
            Console.WriteLine("Wildcard Mask: " + result.WildcardMask);
            Console.WriteLine("Network Address: " + result.NetworkAddress);
            Console.WriteLine("Broadcast Address: " + result.BroadcastAddress);
            Console.WriteLine("First Usable IP: " + result.FirstUsableAddress);
            Console.WriteLine("Last Usable IP: " + result.LastUsableAddress);
            Console.WriteLine("Total Addresses: " + result.TotalAddresses);
            Console.WriteLine("Usable Hosts: " + result.UsableHostCount);
            Console.WriteLine("Address Class: " + result.AddressClass);
            Console.WriteLine("Private Range: " + (result.IsPrivateRange ? "Yes" : "No"));
        }
    }
}
