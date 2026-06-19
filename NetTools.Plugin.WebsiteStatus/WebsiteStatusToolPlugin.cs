using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.WebsiteStatus
{
    public class WebsiteStatusToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        public string Id => "WebsiteStatus";
        public string DisplayName => "Website Status Checker";
        public string Description => "Check if a website is actually up by sending HTTP/HTTPS requests.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new WebsiteStatusToolControl(context);
        }

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter website URL: ");
            var urlText = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(urlText))
            {
                Console.WriteLine("Website URL is required.");
                return;
            }

            Uri uri;
            if (!WebsiteStatusToolControl.TryNormalizeUrl(urlText, out uri))
            {
                Console.WriteLine("Invalid website URL.");
                return;
            }

            Console.Write("Expected text - optional: ");
            var expectedText = Console.ReadLine();

            Console.Write("Mode: 1 = Once, 2 = Repeat every N seconds [default 1]: ");
            var modeText = Console.ReadLine();

            bool repeat = modeText == "2";

            int intervalSeconds = 10;

            if (repeat)
            {
                Console.Write("Interval seconds [default 10]: ");
                var intervalText = Console.ReadLine();

                if (!int.TryParse(intervalText, out intervalSeconds) || intervalSeconds <= 0)
                {
                    intervalSeconds = 10;
                }
            }

            Console.WriteLine();

            if (!repeat)
            {
                var result = await WebsiteStatusToolControl.CheckWebsiteAsync(
                    uri,
                    expectedText,
                    15000,
                    cancellationToken);

                PrintResult(result);
                return;
            }

            Console.WriteLine("Checking website every " + intervalSeconds + " second(s).");
            Console.WriteLine("Press Ctrl+C or stop the tool to cancel.");
            Console.WriteLine();

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await WebsiteStatusToolControl.CheckWebsiteAsync(
                    uri,
                    expectedText,
                    15000,
                    cancellationToken);

                PrintResult(result);

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
        }

        private static void PrintResult(WebsiteStatusResult result)
        {
            Console.WriteLine(
                "[" + result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss") + "] " +
                (result.IsUp ? "UP" : "DOWN") +
                " | " + result.Url +
                " | HTTP: " + result.StatusCodeText +
                " | Time: " + result.ResponseTime.TotalMilliseconds.ToString("0.##") + "ms" +
                (string.IsNullOrWhiteSpace(result.Message) ? string.Empty : " | " + result.Message));
        }
    }
}
