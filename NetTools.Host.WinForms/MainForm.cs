using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NetTools.Host.Core;
using NetTools.PluginContracts;

namespace NetTools.Host.WinForms
{
    public partial class MainForm : Form
    {
        private readonly IPluginHostContext _hostContext;
        private readonly PluginManager _pluginManager;
        private readonly IReadOnlyList<IWinFormsNetworkToolPlugin> _plugins;

        public MainForm()
        {
            InitializeComponent();

            _hostContext = new ConsoleHostContext();
            _pluginManager = new PluginManager();

            string pluginsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            _plugins = _pluginManager.LoadWinFormsPlugins(pluginsRoot, _hostContext);

            LoadPluginList();
        }

        private void LoadPluginList()
        {
            listPlugins.Items.Clear();

            if (_plugins.Count == 0)
            {
                statusLabel.Text = "No WinForms plugins found in Plugins folder.";
                return;
            }

            foreach (var plugin in _plugins)
            {
                listPlugins.Items.Add(plugin);
            }

            listPlugins.DisplayMember = "DisplayName";
            statusLabel.Text = $"{_plugins.Count} plugin(s) loaded.";
        }

        private void listPlugins_SelectedIndexChanged(object sender, EventArgs e)
        {
            panelToolHost.Controls.Clear();

            var plugin = listPlugins.SelectedItem as IWinFormsNetworkToolPlugin;
            if (plugin == null)
            {
                return;
            }

            try
            {
                var control = plugin.CreateToolControl(_hostContext);
                control.Dock = DockStyle.Fill;
                panelToolHost.Controls.Add(control);
                statusLabel.Text = plugin.Description;
            }
            catch (Exception ex)
            {
                _hostContext.Logger.Error(ex.ToString());
                statusLabel.Text = "Failed to load plugin UI.";
                panelToolHost.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.DarkRed,
                    Text = "Failed to load plugin UI.\n" + ex.Message
                });
            }
        }
    }
}
