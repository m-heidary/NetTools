using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.Ping
{
    public class PingToolControl : UserControl
    {
        private readonly IPluginHostContext _context;
        private readonly TextBox _hostTextBox;
        private readonly NumericUpDown _countInput;
        private readonly NumericUpDown _timeoutInput;
        private readonly Button _runButton;
        private readonly Button _cancelButton;
        private readonly RichTextBox _outputBox;
        private CancellationTokenSource _cancellationTokenSource;

        public PingToolControl(IPluginHostContext context)
        {
            _context = context;
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8) };
            _hostTextBox = new TextBox { Left = 8, Top = 8, Width = 260 };
            _countInput = new NumericUpDown { Left = 280, Top = 8, Width = 80, Minimum = 1, Maximum = 100, Value = 4 };
            _timeoutInput = new NumericUpDown { Left = 370, Top = 8, Width = 90, Minimum = 100, Maximum = 60000, Value = 4000, Increment = 100 };
            _runButton = new Button { Left = 470, Top = 6, Width = 90, Height = 28, Text = "Run Ping" };
            _cancelButton = new Button { Left = 570, Top = 6, Width = 90, Height = 28, Text = "Cancel", Enabled = false };

            topPanel.Controls.Add(new Label { Left = 8, Top = 38, AutoSize = true, Text = "Host" });
            topPanel.Controls.Add(new Label { Left = 280, Top = 38, AutoSize = true, Text = "Count" });
            topPanel.Controls.Add(new Label { Left = 370, Top = 38, AutoSize = true, Text = "Timeout (ms)" });
            topPanel.Controls.Add(_hostTextBox);
            topPanel.Controls.Add(_countInput);
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

            _runButton.Click += async (s, e) => await RunPingAsync();
            _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        }

        private async Task RunPingAsync()
        {
            var host = _hostTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Host is required.", "Ping", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _runButton.Enabled = false;
            _cancelButton.Enabled = true;
            _outputBox.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            int count = (int)_countInput.Value;
            int timeout = (int)_timeoutInput.Value;
            int sent = 0;
            int received = 0;
            int failed = 0;
            var roundtripTimes = new List<long>();
            string resolvedAddress = null;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            AppendLine($"Pinging {host} with {count} request(s), timeout {timeout} ms:");
            AppendLine(string.Empty);

            try
            {
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    for (int i = 0; i < count; i++)
                    {
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                        sent++;

                        try
                        {
                            var reply = await ping.SendPingAsync(host, timeout);
                            if (reply.Status == IPStatus.Success)
                            {
                                received++;
                                roundtripTimes.Add(reply.RoundtripTime);
                                resolvedAddress = reply.Address.ToString();
                                AppendLine(
                                    $"Reply from {reply.Address} bytes={reply.Buffer?.Length ?? 0} " +
                                    $"time={reply.RoundtripTime}ms ttl={reply.Options?.Ttl ?? 0} seq={i + 1}");
                            }
                            else
                            {
                                failed++;
                                AppendLine($"Request {i + 1} failed: {reply.Status}");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            AppendLine($"Request {i + 1} error: {ex.Message}");
                        }

                        if (i < count - 1)
                        {
                            await Task.Delay(1000, _cancellationTokenSource.Token);
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
                AppendStatistics(host, resolvedAddress, sent, received, failed, roundtripTimes, stopwatch.Elapsed);
                _runButton.Enabled = true;
                _cancelButton.Enabled = false;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void AppendStatistics(
            string host,
            string resolvedAddress,
            int sent,
            int received,
            int failed,
            List<long> roundtripTimes,
            TimeSpan elapsed)
        {
            AppendLine(string.Empty);
            AppendLine($"Ping statistics for {host}" +
                       (string.IsNullOrEmpty(resolvedAddress) ? string.Empty : $" [{resolvedAddress}]") + ":");

            var lossPercent = sent == 0 ? 0 : (failed * 100.0) / sent;
            AppendLine($"    Packets: Sent = {sent}, Received = {received}, Lost = {failed} ({lossPercent:0.#}% loss),");

            if (roundtripTimes.Count > 0)
            {
                long min = roundtripTimes[0];
                long max = roundtripTimes[0];
                long total = 0;

                foreach (var time in roundtripTimes)
                {
                    if (time < min) min = time;
                    if (time > max) max = time;
                    total += time;
                }

                var average = (double)total / roundtripTimes.Count;
                AppendLine("Approximate round trip times in milli-seconds:");
                AppendLine($"    Minimum = {min}ms, Maximum = {max}ms, Average = {average:0.#}ms");
            }
            else
            {
                AppendLine("Approximate round trip times in milli-seconds:");
                AppendLine("    No successful replies.");
            }

            AppendLine($"Total elapsed time: {elapsed.TotalSeconds:0.##} second(s).");
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
