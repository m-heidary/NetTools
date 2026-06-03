using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.NtpClient
{
    public class NtpClientToolControl : UserControl
    {
        private readonly IPluginHostContext _context;
        private readonly TextBox _serverTextBox;
        private readonly NumericUpDown _timeoutInput;
        private readonly Button _queryButton;
        private readonly Button _syncButton;
        private readonly RichTextBox _outputBox;
        private NtpQueryResult _lastResult;

        public NtpClientToolControl(IPluginHostContext context)
        {
            _context = context;
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8) };
            _serverTextBox = new TextBox { Left = 8, Top = 8, Width = 280, Text = "time.windows.com" };
            _timeoutInput = new NumericUpDown { Left = 300, Top = 8, Width = 90, Minimum = 1000, Maximum = 30000, Value = 5000, Increment = 500 };
            _queryButton = new Button { Left = 400, Top = 6, Width = 90, Height = 28, Text = "Query" };
            _syncButton = new Button { Left = 500, Top = 6, Width = 120, Height = 28, Text = "Sync Time", Enabled = false };

            topPanel.Controls.Add(new Label { Left = 8, Top = 38, AutoSize = true, Text = "NTP Server" });
            topPanel.Controls.Add(new Label { Left = 300, Top = 38, AutoSize = true, Text = "Timeout (ms)" });
            topPanel.Controls.Add(_serverTextBox);
            topPanel.Controls.Add(_timeoutInput);
            topPanel.Controls.Add(_queryButton);
            topPanel.Controls.Add(_syncButton);

            _outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 10F)
            };

            Controls.Add(_outputBox);
            Controls.Add(topPanel);

            _queryButton.Click += async (s, e) => await QueryAsync();
            _syncButton.Click += (s, e) => SyncSystemTime();
        }

        private async Task QueryAsync()
        {
            _queryButton.Enabled = false;
            _syncButton.Enabled = false;
            _outputBox.Clear();
            _lastResult = null;

            var server = _serverTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(server))
            {
                MessageBox.Show(this, "NTP server is required.", "NTP Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _queryButton.Enabled = true;
                return;
            }

            AppendLine($"Querying NTP server: {server}");
            AppendLine($"Running as administrator: {(NtpClientHelper.IsRunningAsAdministrator() ? "Yes" : "No")}");
            AppendLine(string.Empty);

            try
            {
                _lastResult = await NtpClientHelper.QueryAsync(server, (int)_timeoutInput.Value, CancellationToken.None);
                AppendLine(NtpClientHelper.FormatResult(_lastResult));
                _syncButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _context.Logger.Error(ex.Message);
                AppendLine("Error: " + ex.Message);
            }
            finally
            {
                _queryButton.Enabled = true;
            }
        }

        private void SyncSystemTime()
        {
            if (_lastResult == null)
            {
                MessageBox.Show(this, "Query an NTP server first.", "NTP Client", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!NtpClientHelper.IsRunningAsAdministrator())
            {
                MessageBox.Show(
                    this,
                    "Administrator privileges are required to change system time.\nRun the host as administrator and try again.",
                    "NTP Client",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                $"Set system time to corrected UTC?\n\n{_lastResult.CorrectedUtc:yyyy-MM-dd HH:mm:ss.fff} UTC\n{_lastResult.CorrectedLocal:yyyy-MM-dd HH:mm:ss.fff} Local",
                "Confirm Time Sync",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            string error;
            if (NtpClientHelper.TrySetSystemTimeUtc(_lastResult.CorrectedUtc, out error))
            {
                AppendLine(string.Empty);
                AppendLine("System time updated successfully.");
                AppendLine($"New local time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                MessageBox.Show(this, "System time updated successfully.", "NTP Client", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _context.Logger.Error(error);
                AppendLine(string.Empty);
                AppendLine("Failed to update system time: " + error);
                MessageBox.Show(this, "Failed to update system time:\n" + error, "NTP Client", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
