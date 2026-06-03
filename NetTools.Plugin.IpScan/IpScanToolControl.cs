using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.IpScan
{
    public class IpScanToolControl : UserControl
    {
        private readonly IPluginHostContext _context;
        private readonly TextBox _rangeTextBox;
        private readonly NumericUpDown _timeoutInput;
        private readonly NumericUpDown _concurrencyInput;
        private readonly CheckBox _detailsCheckBox;
        private readonly Button _scanButton;
        private readonly Button _cancelButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _statusLabel;
        private readonly DataGridView _resultsGrid;
        private CancellationTokenSource _cancellationTokenSource;

        public IpScanToolControl(IPluginHostContext context)
        {
            _context = context;
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(8) };
            _rangeTextBox = new TextBox { Left = 8, Top = 8, Width = 260, Text = "192.168.1.1-192.168.1.254" };
            _timeoutInput = new NumericUpDown { Left = 280, Top = 8, Width = 80, Minimum = 100, Maximum = 10000, Value = 1000, Increment = 100 };
            _concurrencyInput = new NumericUpDown { Left = 370, Top = 8, Width = 80, Minimum = 1, Maximum = 500, Value = 50 };
            _detailsCheckBox = new CheckBox { Left = 460, Top = 10, Width = 150, Checked = true, Text = "Resolve details" };
            _scanButton = new Button { Left = 620, Top = 6, Width = 90, Height = 28, Text = "Scan" };
            _cancelButton = new Button { Left = 720, Top = 6, Width = 90, Height = 28, Text = "Cancel", Enabled = false };

            topPanel.Controls.Add(new Label { Left = 8, Top = 38, AutoSize = true, Text = "Range or CIDR" });
            topPanel.Controls.Add(new Label { Left = 280, Top = 38, AutoSize = true, Text = "Timeout" });
            topPanel.Controls.Add(new Label { Left = 370, Top = 38, AutoSize = true, Text = "Concurrent" });
            topPanel.Controls.Add(_rangeTextBox);
            topPanel.Controls.Add(_timeoutInput);
            topPanel.Controls.Add(_concurrencyInput);
            topPanel.Controls.Add(_detailsCheckBox);
            topPanel.Controls.Add(_scanButton);
            topPanel.Controls.Add(_cancelButton);

            _statusLabel = new Label { Dock = DockStyle.Top, Height = 24, Padding = new Padding(8, 4, 0, 0), Text = "Ready" };
            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 22 };

            _resultsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White
            };

            _resultsGrid.Columns.Add("IpAddress", "IP Address");
            _resultsGrid.Columns.Add("RoundTripMs", "RTT (ms)");
            _resultsGrid.Columns.Add("Ttl", "TTL");
            _resultsGrid.Columns.Add("MacAddress", "MAC");
            _resultsGrid.Columns.Add("Hostname", "Hostname");
            _resultsGrid.Columns.Add("NetBiosName", "NetBIOS");
            _resultsGrid.Columns.Add("Workgroup", "Workgroup");
            _resultsGrid.Columns.Add("OsGuess", "OS Guess");

            Controls.Add(_resultsGrid);
            Controls.Add(_progressBar);
            Controls.Add(_statusLabel);
            Controls.Add(topPanel);

            _scanButton.Click += async (s, e) => await RunScanAsync();
            _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        }

        private async Task RunScanAsync()
        {
            IReadOnlyList<IPAddress> addresses;
            try
            {
                addresses = IpScanHelper.ParseIpRange(_rangeTextBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "IP Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (addresses.Count == 0)
            {
                MessageBox.Show(this, "No addresses to scan.", "IP Scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var maxConcurrency = (int)_concurrencyInput.Value;
            if (maxConcurrency > addresses.Count)
            {
                maxConcurrency = addresses.Count;
            }

            _scanButton.Enabled = false;
            _cancelButton.Enabled = true;
            _resultsGrid.Rows.Clear();
            _progressBar.Value = 0;
            _cancellationTokenSource = new CancellationTokenSource();

            var aliveHosts = new List<IpScanHostInfo>();
            var completed = 0;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using (var limiter = new SemaphoreSlim(maxConcurrency, maxConcurrency))
                {
                    var tasks = addresses.Select(address => ProbeAsync(
                        address,
                        (int)_timeoutInput.Value,
                        _detailsCheckBox.Checked,
                        limiter,
                        aliveHosts,
                        () => Interlocked.Increment(ref completed),
                        addresses.Count,
                        _cancellationTokenSource.Token)).ToList();

                    await Task.WhenAll(tasks).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Scan canceled.");
            }
            finally
            {
                stopwatch.Stop();
                aliveHosts.Sort((left, right) => IpScanHelper.CompareIpAddresses(left.IpAddress, right.IpAddress));
                PopulateGrid(aliveHosts);
                SetProgress(addresses.Count, addresses.Count, IpScanHelper.FormatSummary(addresses.Count, aliveHosts.Count, stopwatch.Elapsed));
                _scanButton.Enabled = true;
                _cancelButton.Enabled = false;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task ProbeAsync(
            IPAddress address,
            int timeout,
            bool resolveDetails,
            SemaphoreSlim limiter,
            List<IpScanHostInfo> aliveHosts,
            Func<int> incrementCompleted,
            int total,
            CancellationToken cancellationToken)
        {
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var info = await IpScanHelper.ProbeHostAsync(address, timeout, resolveDetails, cancellationToken)
                    .ConfigureAwait(false);

                if (info.IsAlive)
                {
                    lock (aliveHosts)
                    {
                        aliveHosts.Add(info);
                    }
                }
            }
            finally
            {
                limiter.Release();
                var done = incrementCompleted();
                SetProgress(done, total, $"Progress: {done}/{total}");
            }
        }

        private void PopulateGrid(IEnumerable<IpScanHostInfo> hosts)
        {
            _resultsGrid.Rows.Clear();
            foreach (var host in hosts)
            {
                _resultsGrid.Rows.Add(
                    host.IpAddress,
                    host.RoundTripMs,
                    host.Ttl,
                    host.MacAddress,
                    host.Hostname,
                    host.NetBiosName,
                    host.Workgroup,
                    host.OsGuess);
            }
        }

        private void SetProgress(int completed, int total, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, int, string>(SetProgress), completed, total, message);
                return;
            }

            _statusLabel.Text = message;
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

        private void SetStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetStatus), message);
                return;
            }

            _statusLabel.Text = message;
        }
    }
}
