using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.NtpClient
{
    public class NtpClientToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        public string Id => "NtpClient";
        public string DisplayName => "NTP Client";
        public string Description => "Query NTP servers and synchronize system clock.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new NtpClientToolControl(context);
        }

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter NTP server (default time.windows.com): ");
            var server = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(server))
            {
                server = "time.windows.com";
            }

            Console.Write("Timeout ms (default 5000): ");
            var timeoutText = Console.ReadLine();
            int timeout = 5000;
            if (!int.TryParse(timeoutText, out timeout) || timeout <= 0)
            {
                timeout = 5000;
            }

            Console.WriteLine();
            Console.WriteLine($"Querying NTP server: {server}");
            Console.WriteLine();

            NtpQueryResult result;
            try
            {
                result = await NtpClientHelper.QueryAsync(server, timeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation canceled.");
                return;
            }
            catch (Exception ex)
            {
                context.Logger.Error(ex.Message);
                Console.WriteLine("Error: " + ex.Message);
                return;
            }

            Console.WriteLine(NtpClientHelper.FormatResult(result));
            Console.WriteLine();
            Console.WriteLine($"Running as administrator: {(NtpClientHelper.IsRunningAsAdministrator() ? "Yes" : "No")}");

            Console.Write("Sync system time to corrected UTC? (y/n): ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!NtpClientHelper.IsRunningAsAdministrator())
            {
                Console.WriteLine("Administrator privileges are required to change system time.");
                Console.WriteLine("Run the host as administrator and try again.");
                return;
            }

            string error;
            if (NtpClientHelper.TrySetSystemTimeUtc(result.CorrectedUtc, out error))
            {
                Console.WriteLine("System time updated successfully.");
                Console.WriteLine($"New local time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            }
            else
            {
                context.Logger.Error(error);
                Console.WriteLine("Failed to update system time: " + error);
            }
        }
    }
}
