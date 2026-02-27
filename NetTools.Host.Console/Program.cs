using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetTools.Host.Core;
using NetTools.PluginContracts;

namespace NetTools.Host.ConsoleApp
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            IPluginHostContext hostContext = new ConsoleHostContext();
            var manager = new PluginManager();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string pluginsRoot = Path.Combine(baseDir, "Plugins");

            var plugins = manager.LoadConsolePlugins(pluginsRoot, hostContext);

            if (plugins.Count == 0)
            {
                Console.WriteLine("No console plugins found in 'Plugins' directory.");
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
                return 1;
            }

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== NetTools Console Host ===");
                Console.WriteLine();

                for (int i = 0; i < plugins.Count; i++)
                {
                    var p = plugins[i];
                    Console.WriteLine($"{i + 1}. {p.DisplayName} - {p.Description}");
                }

                Console.WriteLine("0. Exit");
                Console.WriteLine();
                Console.Write("Select tool: ");

                var input = Console.ReadLine();
                if (!int.TryParse(input, out var index) || index < 0 || index > plugins.Count)
                {
                    continue;
                }

                if (index == 0)
                {
                    break;
                }

                var plugin = plugins[index - 1];

                Console.Clear();
                Console.WriteLine($"*** {plugin.DisplayName} ***");
                Console.WriteLine(plugin.Description);
                Console.WriteLine("Press Ctrl+C to cancel.\n");

                try
                {
                    await plugin.RunConsoleAsync(hostContext, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Operation canceled.");
                }
                catch (Exception ex)
                {
                    hostContext.Logger.Error(ex.ToString());
                    Console.WriteLine("An error occurred. See log for details.");
                }

                Console.WriteLine();
                Console.WriteLine("Press Enter to return to menu...");
                Console.ReadLine();
            }

            return 0;
        }
    }
}

