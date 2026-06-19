using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.WebsiteStatus
{
    public partial class WebsiteStatusToolControl : UserControl
    {
        private readonly IPluginHostContext _context;

        private readonly TextBox _urlTextBox;
        private readonly TextBox _expectedTextBox;
        private readonly NumericUpDown _intervalInput;
        private readonly NumericUpDown _timeoutInput;
        private readonly RadioButton _onceRadioButton;
        private readonly RadioButton _repeatRadioButton;
        private readonly Button _runButton;
        private readonly Button _cancelButton;
        private readonly RichTextBox _outputBox;

        private CancellationTokenSource _cancellationTokenSource;

        private static readonly HttpClient _httpClient = CreateHttpClient();

        public WebsiteStatusToolControl(IPluginHostContext context)
        {
            _context = context;

            Dock = DockStyle.Fill;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 125,
                Padding = new Padding(8)
            };

            _urlTextBox = new TextBox
            {
                Left = 8,
                Top = 8,
                Width = 360
            };

            _expectedTextBox = new TextBox
            {
                Left = 380,
                Top = 8,
                Width = 220
            };

            _timeoutInput = new NumericUpDown
            {
                Left = 610,
                Top = 8,
                Width = 90,
                Minimum = 1000,
                Maximum = 120000,
                Value = 15000,
                Increment = 1000
            };

            _onceRadioButton = new RadioButton
            {
                Left = 8,
                Top = 62,
                Width = 100,
                Text = "Check once",
                Checked = true
            };

            _repeatRadioButton = new RadioButton
            {
                Left = 115,
                Top = 62,
                Width = 110,
                Text = "Check every"
            };

            _intervalInput = new NumericUpDown
            {
                Left = 230,
                Top = 60,
                Width = 80,
                Minimum = 1,
                Maximum = 86400,
                Value = 10
            };

            _runButton = new Button
            {
                Left = 380,
                Top = 58,
                Width = 100,
                Height = 28,
                Text = "Run Check"
            };

            _cancelButton = new Button
            {
                Left = 490,
                Top = 58,
                Width = 90,
                Height = 28,
                Text = "Cancel",
                Enabled = false
            };

            topPanel.Controls.Add(new Label
            {
                Left = 8,
                Top = 35,
                AutoSize = true,
                Text = "Website URL"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 380,
                Top = 35,
                AutoSize = true,
                Text = "Expected Text - Optional"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 610,
                Top = 35,
                AutoSize = true,
                Text = "Timeout (ms)"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 315,
                Top = 64,
                AutoSize = true,
                Text = "second(s)"
            });

            topPanel.Controls.Add(_urlTextBox);
            topPanel.Controls.Add(_expectedTextBox);
            topPanel.Controls.Add(_timeoutInput);
            topPanel.Controls.Add(_onceRadioButton);
            topPanel.Controls.Add(_repeatRadioButton);
            topPanel.Controls.Add(_intervalInput);
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

            _runButton.Click += async (s, e) => await RunWebsiteCheckAsync();
            _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        }

        private async Task RunWebsiteCheckAsync()
        {
            var urlText = _urlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(urlText))
            {
                MessageBox.Show(
                    this,
                    "Website URL is required.",
                    "Website Status Checker",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            Uri uri;
            if (!TryNormalizeUrl(urlText, out uri))
            {
                MessageBox.Show(
                    this,
                    "Please enter a valid HTTP or HTTPS URL.",
                    "Website Status Checker",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            _runButton.Enabled = false;
            _cancelButton.Enabled = true;
            _urlTextBox.Enabled = false;
            _expectedTextBox.Enabled = false;
            _timeoutInput.Enabled = false;
            _intervalInput.Enabled = false;
            _onceRadioButton.Enabled = false;
            _repeatRadioButton.Enabled = false;

            _outputBox.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            var expectedText = _expectedTextBox.Text;
            int timeout = (int)_timeoutInput.Value;
            int intervalSeconds = (int)_intervalInput.Value;

            int checksAttempted = 0;
            int checksUp = 0;
            int checksDown = 0;

            var stopwatch = Stopwatch.StartNew();

            AppendLine("Checking website: " + uri);
            AppendLine("Mode: " + (_onceRadioButton.Checked ? "Once" : "Repeat every " + intervalSeconds + " second(s)"));
            AppendLine("Timeout: " + timeout + " ms");

            if (!string.IsNullOrWhiteSpace(expectedText))
            {
                AppendLine("Expected text: " + expectedText);
            }

            AppendLine(string.Empty);

            try
            {
                if (_onceRadioButton.Checked)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    checksAttempted++;

                    var result = await CheckWebsiteAsync(
                        uri,
                        expectedText,
                        timeout,
                        _cancellationTokenSource.Token);

                    if (result.IsUp)
                    {
                        checksUp++;
                    }
                    else
                    {
                        checksDown++;
                    }

                    AppendResult(result);
                }
                else
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        checksAttempted++;

                        var result = await CheckWebsiteAsync(
                            uri,
                            expectedText,
                            timeout,
                            _cancellationTokenSource.Token);

                        if (result.IsUp)
                        {
                            checksUp++;
                        }
                        else
                        {
                            checksDown++;
                        }

                        AppendResult(result);

                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AppendLine("Operation canceled.");
            }
            catch (Exception ex)
            {
                _context.Logger.Error(ex.Message);
                AppendLine("Error: " + ex.Message);
            }
            finally
            {
                stopwatch.Stop();

                AppendSummary(
                    uri.ToString(),
                    checksAttempted,
                    checksUp,
                    checksDown,
                    stopwatch.Elapsed);

                _runButton.Enabled = true;
                _cancelButton.Enabled = false;
                _urlTextBox.Enabled = true;
                _expectedTextBox.Enabled = true;
                _timeoutInput.Enabled = true;
                _intervalInput.Enabled = true;
                _onceRadioButton.Enabled = true;
                _repeatRadioButton.Enabled = true;

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        public static async Task<WebsiteStatusResult> CheckWebsiteAsync(
            Uri uri,
            string expectedText,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using (var timeoutCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCancellationTokenSource.CancelAfter(timeoutMilliseconds);

                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    {
                        request.Headers.UserAgent.ParseAdd("NetTools-WebsiteStatusChecker/1.0");
                        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                        using (var response = await _httpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            timeoutCancellationTokenSource.Token))
                        {
                            stopwatch.Stop();

                            int statusCode = (int)response.StatusCode;
                            string statusCodeText = statusCode + " " + response.StatusCode;

                            bool statusCodeOk = statusCode >= 200 && statusCode <= 399;

                            if (!statusCodeOk)
                            {
                                return new WebsiteStatusResult
                                {
                                    Url = uri.ToString(),
                                    IsUp = false,
                                    StatusCode = response.StatusCode,
                                    StatusCodeText = statusCodeText,
                                    ResponseTime = stopwatch.Elapsed,
                                    CheckedAt = DateTime.Now,
                                    Message = response.ReasonPhrase
                                };
                            }

                            if (!string.IsNullOrWhiteSpace(expectedText))
                            {
                                string body = await response.Content.ReadAsStringAsync();

                                bool expectedTextFound =
                                    body != null &&
                                    body.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0;

                                if (!expectedTextFound)
                                {
                                    return new WebsiteStatusResult
                                    {
                                        Url = uri.ToString(),
                                        IsUp = false,
                                        StatusCode = response.StatusCode,
                                        StatusCodeText = statusCodeText,
                                        ResponseTime = stopwatch.Elapsed,
                                        CheckedAt = DateTime.Now,
                                        Message = "Expected text was not found."
                                    };
                                }
                            }

                            return new WebsiteStatusResult
                            {
                                Url = uri.ToString(),
                                IsUp = true,
                                StatusCode = response.StatusCode,
                                StatusCodeText = statusCodeText,
                                ResponseTime = stopwatch.Elapsed,
                                CheckedAt = DateTime.Now,
                                Message = "Website responded successfully."
                            };
                        }
                    }
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();

                return new WebsiteStatusResult
                {
                    Url = uri.ToString(),
                    IsUp = false,
                    StatusCodeText = "-",
                    ResponseTime = stopwatch.Elapsed,
                    CheckedAt = DateTime.Now,
                    Message = "Request timed out."
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                return new WebsiteStatusResult
                {
                    Url = uri.ToString(),
                    IsUp = false,
                    StatusCodeText = "-",
                    ResponseTime = stopwatch.Elapsed,
                    CheckedAt = DateTime.Now,
                    Message = ex.Message
                };
            }
        }

        public static bool TryNormalizeUrl(string input, out Uri uri)
        {
            uri = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim();

            if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                input = "https://" + input;
            }

            return Uri.TryCreate(input, UriKind.Absolute, out uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            return new HttpClient(handler);
        }

        private void AppendResult(WebsiteStatusResult result)
        {
            string statusText = result.IsUp ? "UP" : "DOWN";

            string line =
                "[" + result.CheckedAt.ToString("HH:mm:ss") + "] " +
                statusText.PadRight(4) +
                "  " +
                "HTTP=" + result.StatusCodeText.PadRight(18) +
                "  " +
                "time=" + result.ResponseTime.TotalMilliseconds.ToString("0.##").PadLeft(8) + "ms" +
                "  " +
                result.Url;

            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                line += "  " + result.Message;
            }

            AppendLine(line);
        }

        private void AppendSummary(
            string url,
            int checksAttempted,
            int checksUp,
            int checksDown,
            TimeSpan elapsed)
        {
            AppendLine(string.Empty);
            AppendLine("Website check summary:");
            AppendLine("    URL: " + url);
            AppendLine("    Checks attempted: " + checksAttempted);
            AppendLine("    Checks UP: " + checksUp);
            AppendLine("    Checks DOWN: " + checksDown);

            double downPercent = checksAttempted == 0 ? 0 : checksDown * 100.0 / checksAttempted;

            AppendLine("    Down percentage: " + downPercent.ToString("0.##") + "%");
            AppendLine("    Total elapsed time: " + elapsed.TotalSeconds.ToString("0.##") + " second(s).");
            AppendLine("Check complete.");
        }

        private void AppendLine(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLine), text);
                return;
            }

            _outputBox.AppendText(text + Environment.NewLine);
            _outputBox.SelectionStart = _outputBox.TextLength;
            _outputBox.ScrollToCaret();
        }


    }

    public class WebsiteStatusResult
    {
        public string Url { get; set; }

        public bool IsUp { get; set; }

        public HttpStatusCode? StatusCode { get; set; }

        public string StatusCodeText { get; set; }

        public TimeSpan ResponseTime { get; set; }

        public DateTime CheckedAt { get; set; }

        public string Message { get; set; }
    }
}
