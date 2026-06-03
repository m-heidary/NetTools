using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.PortScan
{
    public class PortScanToolControl : UserControl
    {
        private readonly TextBox _hostTextBox;
        private readonly NumericUpDown _startPortInput;
        private readonly NumericUpDown _endPortInput;
        private readonly NumericUpDown _timeoutInput;
        private readonly NumericUpDown _concurrencyInput;
        private readonly Button _runButton;
        private readonly Button _cancelButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _progressLabel;
        private readonly ListBox _openPortsList;
        private readonly Label _summaryLabel;
        private CancellationTokenSource _cancellationTokenSource;

        public PortScanToolControl(IPluginHostContext context)
        {
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(8) };
            _hostTextBox = new TextBox { Left = 8, Top = 8, Width = 220 };
            _startPortInput = new NumericUpDown { Left = 240, Top = 8, Width = 80, Minimum = 1, Maximum = 65535, Value = 1 };
            _endPortInput = new NumericUpDown { Left = 330, Top = 8, Width = 80, Minimum = 1, Maximum = 65535, Value = 1024 };
            _timeoutInput = new NumericUpDown { Left = 420, Top = 8, Width = 80, Minimum = 100, Maximum = 10000, Value = 500, Increment = 100 };
            _concurrencyInput = new NumericUpDown { Left = 510, Top = 8, Width = 80, Minimum = 1, Maximum = 500, Value = 50 };
            _runButton = new Button { Left = 600, Top = 6, Width = 90, Height = 28, Text = "Scan" };
            _cancelButton = new Button { Left = 700, Top = 6, Width = 90, Height = 28, Text = "Cancel", Enabled = false };

            topPanel.Controls.Add(new Label { Left = 8, Top = 38, AutoSize = true, Text = "Host" });
            topPanel.Controls.Add(new Label { Left = 240, Top = 38, AutoSize = true, Text = "Start" });
            topPanel.Controls.Add(new Label { Left = 330, Top = 38, AutoSize = true, Text = "End" });
            topPanel.Controls.Add(new Label { Left = 420, Top = 38, AutoSize = true, Text = "Timeout" });
            topPanel.Controls.Add(new Label { Left = 510, Top = 38, AutoSize = true, Text = "Concurrent" });
            topPanel.Controls.Add(_hostTextBox);
            topPanel.Controls.Add(_startPortInput);
            topPanel.Controls.Add(_endPortInput);
            topPanel.Controls.Add(_timeoutInput);
            topPanel.Controls.Add(_concurrencyInput);
            topPanel.Controls.Add(_runButton);
            topPanel.Controls.Add(_cancelButton);

            _progressLabel = new Label { Dock = DockStyle.Top, Height = 24, Padding = new Padding(8, 4, 0, 0), Text = "Ready" };
            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 24 };
            _summaryLabel = new Label { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8, 4, 0, 0), Text = "Open ports will appear below." };
            _openPortsList = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10F) };

            Controls.Add(_openPortsList);
            Controls.Add(_summaryLabel);
            Controls.Add(_progressBar);
            Controls.Add(_progressLabel);
            Controls.Add(topPanel);

            _runButton.Click += async (s, e) => await RunScanAsync();
            _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        }

        private async Task RunScanAsync()
        {
            var host = _hostTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Target is required.", "Port Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int startPort = (int)_startPortInput.Value;
            int endPort = (int)_endPortInput.Value;
            if (endPort < startPort)
            {
                MessageBox.Show(this, "End port must be greater than or equal to start port.", "Port Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _runButton.Enabled = false;
            _cancelButton.Enabled = true;
            _openPortsList.Items.Clear();
            _progressBar.Value = 0;
            _cancellationTokenSource = new CancellationTokenSource();

            int timeout = (int)_timeoutInput.Value;
            int maxConcurrency = (int)_concurrencyInput.Value;
            var portCount = endPort - startPort + 1;
            if (maxConcurrency > portCount)
            {
                maxConcurrency = portCount;
            }

            var openPorts = new List<int>();
            var completedCount = 0;
            var stopwatch = Stopwatch.StartNew();
            SetProgress(0, portCount, $"Scanning {host} ports {startPort}-{endPort}...");

            try
            {
                var scanTasks = new List<Task>(portCount);
                using (var concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency))
                {
                    for (int port = startPort; port <= endPort; port++)
                    {
                        int currentPort = port;
                        scanTasks.Add(ScanPortAsync(host, currentPort, timeout, portCount, concurrencyLimiter, openPorts, () => Interlocked.Increment(ref completedCount)));
                    }

                    await Task.WhenAll(scanTasks);
                }
            }
            catch (OperationCanceledException)
            {
                _summaryLabel.Text = "Scan canceled.";
            }
            finally
            {
                stopwatch.Stop();
                openPorts.Sort();
                _openPortsList.Items.Clear();
                foreach (var port in openPorts)
                {
                    _openPortsList.Items.Add($"Port {port} is OPEN");
                }

                _summaryLabel.Text = openPorts.Count == 0
                    ? $"No open ports found. Checked {portCount} port(s) in {stopwatch.Elapsed.TotalSeconds:0.##} second(s)."
                    : $"Found {openPorts.Count} open port(s) in {stopwatch.Elapsed.TotalSeconds:0.##} second(s).";

                SetProgress(portCount, portCount, "Scan complete.");
                _runButton.Enabled = true;
                _cancelButton.Enabled = false;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task ScanPortAsync(
            string host,
            int port,
            int timeout,
            int totalPorts,
            SemaphoreSlim concurrencyLimiter,
            List<int> openPorts,
            Func<int> incrementCompleted)
        {
            await concurrencyLimiter.WaitAsync(_cancellationTokenSource.Token);

            try
            {
                if (await IsPortOpenAsync(host, port, timeout, _cancellationTokenSource.Token))
                {
                    lock (openPorts)
                    {
                        openPorts.Add(port);
                    }
                }
            }
            finally
            {
                concurrencyLimiter.Release();
                var completed = incrementCompleted();
                SetProgress(completed, totalPorts, $"Progress: {completed}/{totalPorts}");
            }
        }

        private static async Task<bool> IsPortOpenAsync(string host, int port, int timeout, CancellationToken cancellationToken)
        {
            using (var client = new TcpClient())
            {
                try
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeout, cancellationToken));
                    if (completedTask != connectTask)
                    {
                        return false;
                    }

                    await connectTask;
                    return client.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        private void SetProgress(int completed, int total, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, int, string>(SetProgress), completed, total, message);
                return;
            }

            _progressLabel.Text = message;
            if (total <= 0)
            {
                _progressBar.Value = 0;
                return;
            }

            var percent = (int)((completed * 100L) / total);
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            _progressBar.Value = percent;
        }
    }
}
