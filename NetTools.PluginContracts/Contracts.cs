using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetTools.PluginContracts
{
    public interface IPluginHostContext
    {
        ILogSink Logger { get; }
    }

    public interface ILogSink
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public interface INetworkToolMetadata
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
    }

    public interface IConsoleNetworkToolPlugin : INetworkToolMetadata
    {
        Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken);
    }

    public interface IWinFormsNetworkToolPlugin : INetworkToolMetadata
    {
        UserControl CreateToolControl(IPluginHostContext context);
    }
}

