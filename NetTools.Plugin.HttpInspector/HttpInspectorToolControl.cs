using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.HttpInspector
{
    public partial class HttpInspectorToolControl : UserControl
    {
        private readonly IPluginHostContext _context;

        private readonly TextBox _urlTextBox;
        private readonly ComboBox _methodComboBox;
        private readonly NumericUpDown _timeoutInput;
        private readonly Button _runButton;
        private readonly Button _cancelButton;
        private readonly RichTextBox _outputBox;

        private CancellationTokenSource _cancellationTokenSource;

        public HttpInspectorToolControl(IPluginHostContext context)
        {
            _context = context;

            InitializeComponent();

            Dock = DockStyle.Fill;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 92,
                Padding = new Padding(8)
            };

            _urlTextBox = new TextBox
            {
                Left = 8,
                Top = 8,
                Width = 360
            };

            _methodComboBox = new ComboBox
            {
                Left = 380,
                Top = 8,
                Width = 90,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _methodComboBox.Items.Add("HEAD");
            _methodComboBox.Items.Add("GET");
            _methodComboBox.SelectedIndex = 0;

            _timeoutInput = new NumericUpDown
            {
                Left = 482,
                Top = 8,
                Width = 100,
                Minimum = 1000,
                Maximum = 120000,
                Increment = 1000,
                Value = 10000
            };

            _runButton = new Button
            {
                Left = 594,
                Top = 6,
                Width = 100,
                Height = 28,
                Text = "Inspect"
            };

            _cancelButton = new Button
            {
                Left = 704,
                Top = 6,
                Width = 90,
                Height = 28,
                Text = "Cancel",
                Enabled = false
            };

            topPanel.Controls.Add(new Label
            {
                Left = 8,
                Top = 36,
                AutoSize = true,
                Text = "URL"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 380,
                Top = 36,
                AutoSize = true,
                Text = "Method"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 482,
                Top = 36,
                AutoSize = true,
                Text = "Timeout (ms)"
            });

            topPanel.Controls.Add(_urlTextBox);
            topPanel.Controls.Add(_methodComboBox);
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

            _runButton.Click += async (s, e) => await RunInspectAsync();
            _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        }

        private async Task RunInspectAsync()
        {
            var input = _urlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show(
                    this,
                    "URL is required.",
                    "HTTP Inspector",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string normalizedUrl;
            if (!TryNormalizeUrl(input, out normalizedUrl))
            {
                MessageBox.Show(
                    this,
                    "Please enter a valid URL.",
                    "HTTP Inspector",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var useGet = string.Equals(_methodComboBox.SelectedItem as string, "GET", StringComparison.OrdinalIgnoreCase);
            var timeoutMilliseconds = (int)_timeoutInput.Value;

            _runButton.Enabled = false;
            _cancelButton.Enabled = true;
            _urlTextBox.Enabled = false;
            _methodComboBox.Enabled = false;
            _timeoutInput.Enabled = false;

            _outputBox.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            AppendLine("Inspecting HTTP endpoint");
            AppendLine("URL: " + normalizedUrl);
            AppendLine("Method: " + (useGet ? "GET" : "HEAD"));
            AppendLine("Timeout: " + timeoutMilliseconds + " ms");
            AppendLine(string.Empty);

            try
            {
                var result = await InspectAsync(
                    normalizedUrl,
                    useGet,
                    timeoutMilliseconds,
                    _cancellationTokenSource.Token);

                AppendResult(result);
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
                _runButton.Enabled = true;
                _cancelButton.Enabled = false;
                _urlTextBox.Enabled = true;
                _methodComboBox.Enabled = true;
                _timeoutInput.Enabled = true;

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        public static bool TryNormalizeUrl(string input, out string normalizedUrl)
        {
            normalizedUrl = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim();

            Uri uri;
            if (Uri.TryCreate(input, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                normalizedUrl = uri.ToString();
                return true;
            }

            if (input.IndexOf("://", StringComparison.OrdinalIgnoreCase) < 0)
            {
                if (Uri.TryCreate("https://" + input, UriKind.Absolute, out uri))
                {
                    normalizedUrl = uri.ToString();
                    return true;
                }
            }

            return false;
        }

        public static async Task<HttpInspectorResult> InspectAsync(
            string url,
            bool useGet,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var redirectSteps = new List<HttpRedirectStep>();
                var currentUrl = url;
                HttpResponseMessage finalResponse = null;
                string methodName = useGet ? "GET" : "HEAD";

                using (var handler = new HttpClientHandler())
                using (var client = new HttpClient(handler))
                {
                    handler.AllowAutoRedirect = false;
                    client.Timeout = Timeout.InfiniteTimeSpan;

                    for (int i = 0; i < 10; i++)
                    {
                        using (var request = new HttpRequestMessage(
                            useGet ? HttpMethod.Get : HttpMethod.Head,
                            currentUrl))
                        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            timeoutCts.CancelAfter(timeoutMilliseconds);

                            var response = await client.SendAsync(
                                request,
                                HttpCompletionOption.ResponseHeadersRead,
                                timeoutCts.Token);

                            if (IsRedirectStatusCode(response.StatusCode))
                            {
                                var location = response.Headers.Location;
                                if (location == null)
                                {
                                    finalResponse = response;
                                    break;
                                }

                                var nextUri = location.IsAbsoluteUri
                                    ? location
                                    : new Uri(new Uri(currentUrl), location);

                                redirectSteps.Add(new HttpRedirectStep
                                {
                                    SourceUrl = currentUrl,
                                    StatusCode = response.StatusCode,
                                    TargetUrl = nextUri.ToString()
                                });

                                currentUrl = nextUri.ToString();
                                response.Dispose();
                                continue;
                            }

                            finalResponse = response;
                            break;
                        }
                    }

                    if (finalResponse == null)
                    {
                        stopwatch.Stop();

                        return new HttpInspectorResult
                        {
                            RequestUrl = url,
                            Method = methodName,
                            Success = false,
                            CheckedAt = DateTime.Now,
                            Elapsed = stopwatch.Elapsed,
                            Message = "Too many redirects or no final response received."
                        };
                    }

                    using (finalResponse)
                    {
                        var headersText = BuildHeadersText(finalResponse);

                        stopwatch.Stop();

                        return new HttpInspectorResult
                        {
                            RequestUrl = url,
                            Method = methodName,
                            Success = true,
                            CheckedAt = DateTime.Now,
                            Elapsed = stopwatch.Elapsed,
                            FinalUrl = currentUrl,
                            StatusCode = finalResponse.StatusCode,
                            HeadersText = headersText,
                            RedirectSteps = redirectSteps,
                            Message = "Inspection completed successfully."
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                stopwatch.Stop();

                return new HttpInspectorResult
                {
                    RequestUrl = url,
                    Method = useGet ? "GET" : "HEAD",
                    Success = false,
                    CheckedAt = DateTime.Now,
                    Elapsed = stopwatch.Elapsed,
                    Message = "Request timed out."
                };
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();

                return new HttpInspectorResult
                {
                    RequestUrl = url,
                    Method = useGet ? "GET" : "HEAD",
                    Success = false,
                    CheckedAt = DateTime.Now,
                    Elapsed = stopwatch.Elapsed,
                    Message = "HTTP error: " + ex.Message
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                return new HttpInspectorResult
                {
                    RequestUrl = url,
                    Method = useGet ? "GET" : "HEAD",
                    Success = false,
                    CheckedAt = DateTime.Now,
                    Elapsed = stopwatch.Elapsed,
                    Message = ex.Message
                };
            }
        }

        private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
        {
            var code = (int)statusCode;
            return code == 301 || code == 302 || code == 303 || code == 307 || code == 308;
        }

        private static string BuildHeadersText(HttpResponseMessage response)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Response Headers:");
            foreach (var header in response.Headers)
            {
                builder.AppendLine(header.Key + ": " + string.Join(", ", header.Value));
            }

            if (response.Content != null)
            {
                builder.AppendLine();
                builder.AppendLine("Content Headers:");
                foreach (var header in response.Content.Headers)
                {
                    builder.AppendLine(header.Key + ": " + string.Join(", ", header.Value));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private void AppendResult(HttpInspectorResult result)
        {
            AppendLine("Request URL: " + result.RequestUrl);
            AppendLine("Status: " + (result.Success ? "SUCCESS" : "FAILED"));
            AppendLine("Checked At: " + result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendLine("Elapsed: " + result.Elapsed.TotalMilliseconds.ToString("0.##") + " ms");

            if (!result.Success)
            {
                AppendLine("Message: " + result.Message);
                AppendLine(string.Empty);
                AppendLine("Inspection complete.");
                return;
            }

            AppendLine("Method: " + result.Method);
            AppendLine("Final URL: " + result.FinalUrl);
            AppendLine("Status Code: " + ((int)result.StatusCode) + " " + result.StatusCode);

            if (result.RedirectSteps.Count > 0)
            {
                AppendLine(string.Empty);
                AppendLine("Redirect Chain:");
                for (int i = 0; i < result.RedirectSteps.Count; i++)
                {
                    var step = result.RedirectSteps[i];
                    AppendLine(
                        (i + 1).ToString() + ". " +
                        step.SourceUrl + " -> " +
                        ((int)step.StatusCode).ToString() + " " + step.StatusCode + " -> " +
                        step.TargetUrl);
                }
            }

            AppendLine(string.Empty);
            AppendLine(result.HeadersText);
            AppendLine(string.Empty);
            AppendLine("Inspection complete.");
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

    public class HttpInspectorResult
    {
        public string RequestUrl { get; set; }

        public string Method { get; set; }

        public bool Success { get; set; }

        public DateTime CheckedAt { get; set; }

        public TimeSpan Elapsed { get; set; }

        public string FinalUrl { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public string HeadersText { get; set; }

        public List<HttpRedirectStep> RedirectSteps { get; set; } = new List<HttpRedirectStep>();

        public string Message { get; set; }
    }

    public class HttpRedirectStep
    {
        public string SourceUrl { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public string TargetUrl { get; set; }
    }
}
