using System;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.Traceroute
{
    public class TracerouteToolControl : UserControl
    {
        private readonly TextBox _hostTextBox;
        private readonly NumericUpDown _maxHopsInput;
        private readonly NumericUpDown _timeoutInput;
        private readonly Button _runButton;
        private readonly Button _cancelButton;
        private readonly RichTextBox _outputBox;
        private CancellationTokenSource _cancellationTokenSource;

        public TracerouteToolControl(IPluginHostContext context)
        {
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8) };
            _hostTextBox = new TextBox { Left = 8, Top = 8, Width = 260 };
            _maxHopsInput = new NumericUpDown { Left = 280, Top = 8, Width = 80, Minimum = 1, Maximum = 64, Value = 30 };
            _timeoutInput = new NumericUpDown { Left = 370, Top = 8, Width = 90, Minimum = 500, Maximum = 30000, Value = 4000, Increment = 500 };
            _runButton = new Button { Left = 470, Top = 6, Width = 100, Height = 28, Text = "Run Trace" };
            _cancelButton = new Button { Left = 580, Top = 6, Width = 90, Height = 28, Text = "Cancel", Enabled = false };

            topPanel.Controls.Add(new Label { Left = 8, Top = 38, AutoSize = true, Text = "Destination" });
            topPanel.Controls.Add(new Label { Left = 280, Top = 38, AutoSize = true, Text = "Max Hops" });
            topPanel.Controls.Add(new Label { Left = 370, Top = 38, AutoSize = true, Text = "Timeout (ms)" });
            topPanel.Controls.Add(_hostTextBox);
            topPanel.Controls.Add(_maxHopsInput);
            topPanel.Controls.Add(_timeoutInput);
            topPanel.Controls.Add(_runButton);
            topPanel.Controls.Add(_cancelButton);

            _outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 10F)
            };

            Controls.Add(_outputBox);
            Controls.Add(topPanel);

            _runButton.Click += async (s, e) => await RunTracerouteAsync();
            _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        }

        private async Task RunTracerouteAsync()
        {
            var host = _hostTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Destination is required.", "Traceroute", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _runButton.Enabled = false;
            _cancelButton.Enabled = true;
            _outputBox.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            int maxHops = (int)_maxHopsInput.Value;
            int timeout = (int)_timeoutInput.Value;
            int hopsAttempted = 0;
            int hopsResponded = 0;
            int hopsTimedOut = 0;
            int hopsErrored = 0;
            bool destinationReached = false;
            string destinationAddress = null;
            var stopwatch = Stopwatch.StartNew();

            AppendLine($"Tracing route to {host} over a maximum of {maxHops} hops:");
            AppendLine(string.Empty);

            try
            {
                using (var ping = new Ping())
                {
                    for (int ttl = 1; ttl <= maxHops; ttl++)
                    {
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                        hopsAttempted++;

                        var options = new PingOptions(ttl, true);
                        var buffer = new byte[32];
                        var hopStart = DateTime.UtcNow;

                        try
                        {
                            var reply = await ping.SendPingAsync(host, timeout, buffer, options);
                            var elapsed = DateTime.UtcNow - hopStart;

                            if (reply.Status == IPStatus.TimedOut)
                            {
                                hopsTimedOut++;
                                AppendLine($"{ttl,3}  *  Request timed out.");
                            }
                            else
                            {
                                hopsResponded++;
                                destinationAddress = reply.Address.ToString();
                                AppendLine($"{ttl,3}  {reply.Address,-15}  time={elapsed.TotalMilliseconds,6:0.##}ms  status={reply.Status}");

                                if (reply.Status == IPStatus.Success)
                                {
                                    destinationReached = true;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            hopsErrored++;
                            AppendLine($"{ttl,3}  *  Error: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AppendLine("Operation canceled.");
            }
            finally
            {
                stopwatch.Stop();
                AppendSummary(host, destinationAddress, hopsAttempted, hopsResponded, hopsTimedOut, hopsErrored, destinationReached, stopwatch.Elapsed);
                _runButton.Enabled = true;
                _cancelButton.Enabled = false;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void AppendSummary(
            string host,
            string destinationAddress,
            int hopsAttempted,
            int hopsResponded,
            int hopsTimedOut,
            int hopsErrored,
            bool destinationReached,
            TimeSpan elapsed)
        {
            AppendLine(string.Empty);
            AppendLine("Trace summary:");
            AppendLine($"    Destination: {host}" +
                       (string.IsNullOrEmpty(destinationAddress) ? string.Empty : $" [{destinationAddress}]"));
            AppendLine($"    Hops attempted: {hopsAttempted}");
            AppendLine($"    Hops responded: {hopsResponded}");
            AppendLine($"    Hops timed out: {hopsTimedOut}");
            AppendLine($"    Hops with errors: {hopsErrored}");
            AppendLine($"    Destination reached: {(destinationReached ? "Yes" : "No")}");
            AppendLine($"    Total elapsed time: {elapsed.TotalSeconds:0.##} second(s).");
            AppendLine(destinationReached ? "Trace complete." : "Trace finished without reaching destination.");
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
