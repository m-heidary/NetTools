using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.DnsLookup
{
    public class DnsLookupToolControl : UserControl
    {
        private readonly TextBox _queryTextBox;
        private readonly Button _lookupButton;
        private readonly RichTextBox _outputBox;

        public DnsLookupToolControl(IPluginHostContext context)
        {
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            _queryTextBox = new TextBox { Left = 8, Top = 10, Width = 320 };
            _lookupButton = new Button { Left = 340, Top = 8, Width = 100, Height = 28, Text = "Lookup" };

            topPanel.Controls.Add(new Label { Left = 8, Top = 32, AutoSize = true, Text = "Hostname or IP" });
            topPanel.Controls.Add(_queryTextBox);
            topPanel.Controls.Add(_lookupButton);

            _outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 10F)
            };

            Controls.Add(_outputBox);
            Controls.Add(topPanel);

            _lookupButton.Click += async (s, e) => await RunLookupAsync();
            _queryTextBox.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await RunLookupAsync();
                }
            };
        }

        private async Task RunLookupAsync()
        {
            var input = _queryTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show(this, "Input is required.", "DNS Lookup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _lookupButton.Enabled = false;
            _outputBox.Clear();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var hostEntry = await Task.Run(() => Dns.GetHostEntry(input));
                stopwatch.Stop();

                var ipv4Addresses = new List<string>();
                var ipv6Addresses = new List<string>();

                foreach (var address in hostEntry.AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4Addresses.Add(address.ToString());
                    }
                    else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        ipv6Addresses.Add(address.ToString());
                    }
                }

                AppendLine($"Looking up '{input}'...");
                AppendLine(string.Empty);
                AppendLine("Host Name: " + hostEntry.HostName);

                if (hostEntry.Aliases != null && hostEntry.Aliases.Length > 0)
                {
                    AppendLine("Aliases:");
                    foreach (var alias in hostEntry.Aliases)
                    {
                        AppendLine("  " + alias);
                    }
                }

                if (ipv4Addresses.Count > 0)
                {
                    AppendLine("IPv4 Addresses:");
                    foreach (var address in ipv4Addresses)
                    {
                        AppendLine("  " + address);
                    }
                }

                if (ipv6Addresses.Count > 0)
                {
                    AppendLine("IPv6 Addresses:");
                    foreach (var address in ipv6Addresses)
                    {
                        AppendLine("  " + address);
                    }
                }

                AppendLine(string.Empty);
                AppendLine("Lookup summary:");
                AppendLine($"    Query: {input}");
                AppendLine($"    Resolved host: {hostEntry.HostName}");
                AppendLine($"    Aliases: {hostEntry.Aliases?.Length ?? 0}");
                AppendLine($"    IPv4 addresses: {ipv4Addresses.Count}");
                AppendLine($"    IPv6 addresses: {ipv6Addresses.Count}");
                AppendLine($"    Total addresses: {ipv4Addresses.Count + ipv6Addresses.Count}");
                AppendLine($"    Lookup time: {stopwatch.Elapsed.TotalMilliseconds:0.##} ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppendLine("Error: " + ex.Message);
                AppendLine($"Lookup failed after {stopwatch.Elapsed.TotalMilliseconds:0.##} ms.");
            }
            finally
            {
                _lookupButton.Enabled = true;
            }
        }

        private void AppendLine(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLine), text);
                return;
            }

            _outputBox.AppendText(text + Environment.NewLine);
        }
    }
}
