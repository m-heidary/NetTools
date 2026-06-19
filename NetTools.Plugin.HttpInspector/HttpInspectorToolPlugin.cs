using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.HttpInspector
{
    public class HttpInspectorToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
    {
        public string Id => "HttpInspector";
        public string DisplayName => "HTTP Inspector";
        public string Description => "Inspect HTTP status, headers, redirects, and final URL.";

        public UserControl CreateToolControl(IPluginHostContext context)
        {
            return new HttpInspectorToolControl(context);
        }

        public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
        {
            Console.Write("Enter URL: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("URL is required.");
                return;
            }

            string normalizedUrl;
            if (!HttpInspectorToolControl.TryNormalizeUrl(input, out normalizedUrl))
            {
                Console.WriteLine("Invalid URL.");
                return;
            }

            Console.Write("Method [HEAD/GET, default HEAD]: ");
            var methodText = Console.ReadLine();
            var useGet = string.Equals(methodText, "GET", StringComparison.OrdinalIgnoreCase);

            Console.Write("Timeout in milliseconds [default 10000]: ");
            var timeoutText = Console.ReadLine();

            int timeoutMilliseconds = 10000;
            if (!string.IsNullOrWhiteSpace(timeoutText))
            {
                int parsedTimeout;
                if (int.TryParse(timeoutText, out parsedTimeout) && parsedTimeout > 0)
                {
                    timeoutMilliseconds = parsedTimeout;
                }
            }

            Console.WriteLine();

            var result = await HttpInspectorToolControl.InspectAsync(
                normalizedUrl,
                useGet,
                timeoutMilliseconds,
                cancellationToken);

            PrintResult(result);
        }

        private static void PrintResult(HttpInspectorResult result)
        {
            Console.WriteLine("Request URL: " + result.RequestUrl);
            Console.WriteLine("Status: " + (result.Success ? "SUCCESS" : "FAILED"));
            Console.WriteLine("Checked At: " + result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("Elapsed: " + result.Elapsed.TotalMilliseconds.ToString("0.##") + " ms");

            if (!result.Success)
            {
                Console.WriteLine("Message: " + result.Message);
                return;
            }

            Console.WriteLine("Method: " + result.Method);
            Console.WriteLine("Final URL: " + result.FinalUrl);
            Console.WriteLine("Status Code: " + ((int)result.StatusCode) + " " + result.StatusCode);

            if (result.RedirectSteps.Count > 0)
            {
                Console.WriteLine("Redirect Chain:");
                for (int i = 0; i < result.RedirectSteps.Count; i++)
                {
                    var step = result.RedirectSteps[i];
                    Console.WriteLine(
                        (i + 1).ToString() + ". " +
                        step.SourceUrl + " -> " +
                        ((int)step.StatusCode).ToString() + " " + step.StatusCode + " -> " +
                        step.TargetUrl);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Headers:");
            Console.WriteLine(result.HeadersText);
        }
    }
}
