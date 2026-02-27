using System;
using NetTools.PluginContracts;

namespace NetTools.Host.Core
{
    public sealed class ConsoleLogSink : ILogSink
    {
        public void Info(string message)
        {
            WriteColored("[INFO] ", ConsoleColor.Cyan, message);
        }

        public void Warn(string message)
        {
            WriteColored("[WARN] ", ConsoleColor.Yellow, message);
        }

        public void Error(string message)
        {
            WriteColored("[ERROR]", ConsoleColor.Red, message);
        }

        private static void WriteColored(string prefix, ConsoleColor color, string message)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(prefix + " ");
            Console.ForegroundColor = oldColor;
            Console.WriteLine(message);
        }
    }

    public sealed class ConsoleHostContext : IPluginHostContext
    {
        public ConsoleHostContext()
        {
            Logger = new ConsoleLogSink();
        }

        public ILogSink Logger { get; }
    }
}

