using NetTools.PluginContracts;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetTools.Plugin.SslCertificate
{
    public partial class SslCertificateToolControl : UserControl
    {
        private readonly IPluginHostContext _context;

        private readonly TextBox _targetTextBox;
        private readonly NumericUpDown _portInput;
        private readonly NumericUpDown _timeoutInput;
        private readonly Button _runButton;
        private readonly Button _cancelButton;
        private readonly RichTextBox _outputBox;

        private CancellationTokenSource _cancellationTokenSource;

        public SslCertificateToolControl(IPluginHostContext context)
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

            _targetTextBox = new TextBox
            {
                Left = 8,
                Top = 8,
                Width = 340
            };

            _portInput = new NumericUpDown
            {
                Left = 360,
                Top = 8,
                Width = 80,
                Minimum = 1,
                Maximum = 65535,
                Value = 443
            };

            _timeoutInput = new NumericUpDown
            {
                Left = 452,
                Top = 8,
                Width = 100,
                Minimum = 1000,
                Maximum = 120000,
                Value = 10000,
                Increment = 1000
            };

            _runButton = new Button
            {
                Left = 564,
                Top = 6,
                Width = 100,
                Height = 28,
                Text = "Check SSL"
            };

            _cancelButton = new Button
            {
                Left = 674,
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
                Text = "Hostname or URL"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 360,
                Top = 36,
                AutoSize = true,
                Text = "Port"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 452,
                Top = 36,
                AutoSize = true,
                Text = "Timeout (ms)"
            });

            topPanel.Controls.Add(_targetTextBox);
            topPanel.Controls.Add(_portInput);
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

            _runButton.Click += async (s, e) => await RunCheckAsync();
            _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        }

        private async Task RunCheckAsync()
        {
            var input = _targetTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show(
                    this,
                    "Hostname or URL is required.",
                    "SSL/TLS Certificate Checker",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string host;
            int detectedPort;
            if (!TryParseTarget(input, out host, out detectedPort))
            {
                MessageBox.Show(
                    this,
                    "Please enter a valid hostname or URL.",
                    "SSL/TLS Certificate Checker",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int port = (int)_portInput.Value;
            if (string.IsNullOrWhiteSpace(_targetTextBox.Text) == false && _portInput.Value == 443 && detectedPort != 443)
            {
                port = detectedPort;
            }

            int timeoutMilliseconds = (int)_timeoutInput.Value;

            _runButton.Enabled = false;
            _cancelButton.Enabled = true;
            _targetTextBox.Enabled = false;
            _portInput.Enabled = false;
            _timeoutInput.Enabled = false;

            _outputBox.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            AppendLine("Checking SSL/TLS certificate");
            AppendLine("Target: " + host + ":" + port);
            AppendLine("Timeout: " + timeoutMilliseconds + " ms");
            AppendLine(string.Empty);

            try
            {
                var result = await CheckCertificateAsync(
                    host,
                    port,
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
                _targetTextBox.Enabled = true;
                _portInput.Enabled = true;
                _timeoutInput.Enabled = true;

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        public static bool TryParseTarget(string input, out string host, out int port)
        {
            host = null;
            port = 443;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim();

            Uri uri;
            if (Uri.TryCreate(input, UriKind.Absolute, out uri))
            {
                host = uri.Host;
                port = uri.IsDefaultPort ? GetDefaultPort(uri.Scheme) : uri.Port;
                return !string.IsNullOrWhiteSpace(host);
            }

            if (input.IndexOf("://", StringComparison.OrdinalIgnoreCase) < 0)
            {
                if (Uri.TryCreate("https://" + input, UriKind.Absolute, out uri))
                {
                    host = uri.Host;
                    port = uri.IsDefaultPort ? 443 : uri.Port;
                    return !string.IsNullOrWhiteSpace(host);
                }
            }

            return false;
        }

        public static async Task<SslCertificateResult> CheckCertificateAsync(
            string host,
            int port,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using (var tcpClient = new TcpClient())
                {
                    await ConnectWithTimeoutAsync(tcpClient, host, port, timeoutMilliseconds, cancellationToken);

                    using (var sslStream = new SslStream(
                        tcpClient.GetStream(),
                        false,
                        new RemoteCertificateValidationCallback(ValidateRemoteCertificate)))
                    {
                        await AuthenticateWithTimeoutAsync(sslStream, host, timeoutMilliseconds, cancellationToken);

                        if (sslStream.RemoteCertificate == null)
                        {
                            stopwatch.Stop();

                            return new SslCertificateResult
                            {
                                Host = host,
                                Port = port,
                                Success = false,
                                CheckedAt = DateTime.Now,
                                Elapsed = stopwatch.Elapsed,
                                Message = "The remote host did not provide a certificate."
                            };
                        }

                        var certificate = new X509Certificate2(sslStream.RemoteCertificate);
                        string chainStatus = BuildChainStatus(certificate);
                        string subjectAlternativeNames = GetSubjectAlternativeNames(certificate);

                        stopwatch.Stop();

                        return new SslCertificateResult
                        {
                            Host = host,
                            Port = port,
                            Success = true,
                            CheckedAt = DateTime.Now,
                            Elapsed = stopwatch.Elapsed,
                            TlsProtocol = sslStream.SslProtocol.ToString(),
                            Subject = certificate.Subject,
                            Issuer = certificate.Issuer,
                            NotBefore = certificate.NotBefore,
                            NotAfter = certificate.NotAfter,
                            DaysRemaining = (certificate.NotAfter - DateTime.Now).TotalDays,
                            Thumbprint = certificate.Thumbprint,
                            SerialNumber = certificate.SerialNumber,
                            SignatureAlgorithm = certificate.SignatureAlgorithm != null
                                ? certificate.SignatureAlgorithm.FriendlyName
                                : string.Empty,
                            SubjectAlternativeNames = subjectAlternativeNames,
                            ChainStatus = chainStatus,
                            Message = "Certificate retrieved successfully."
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException ex)
            {
                stopwatch.Stop();

                return new SslCertificateResult
                {
                    Host = host,
                    Port = port,
                    Success = false,
                    CheckedAt = DateTime.Now,
                    Elapsed = stopwatch.Elapsed,
                    Message = ex.Message
                };
            }
            catch (AuthenticationException ex)
            {
                stopwatch.Stop();

                return new SslCertificateResult
                {
                    Host = host,
                    Port = port,
                    Success = false,
                    CheckedAt = DateTime.Now,
                    Elapsed = stopwatch.Elapsed,
                    Message = "TLS authentication failed: " + ex.Message
                };
            }
            catch (SocketException ex)
            {
                stopwatch.Stop();

                return new SslCertificateResult
                {
                    Host = host,
                    Port = port,
                    Success = false,
                    CheckedAt = DateTime.Now,
                    Elapsed = stopwatch.Elapsed,
                    Message = "Socket error: " + ex.Message
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                return new SslCertificateResult
                {
                    Host = host,
                    Port = port,
                    Success = false,
                    CheckedAt = DateTime.Now,
                    Elapsed = stopwatch.Elapsed,
                    Message = ex.Message
                };
            }
        }

        private static async Task ConnectWithTimeoutAsync(
            TcpClient tcpClient,
            string host,
            int port,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var connectTask = tcpClient.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMilliseconds, cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, delayTask);

            if (completedTask != connectTask)
            {
                tcpClient.Close();

                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                throw new TimeoutException("Connection timed out.");
            }

            await connectTask;
        }

        private static async Task AuthenticateWithTimeoutAsync(
            SslStream sslStream,
            string host,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var authenticateTask = sslStream.AuthenticateAsClientAsync(host);
            var delayTask = Task.Delay(timeoutMilliseconds, cancellationToken);

            var completedTask = await Task.WhenAny(authenticateTask, delayTask);

            if (completedTask != authenticateTask)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                throw new TimeoutException("TLS handshake timed out.");
            }

            await authenticateTask;
        }

        private static bool ValidateRemoteCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private static int GetDefaultPort(string scheme)
        {
            if (string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return 80;
            }

            if (string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return 443;
            }

            return 443;
        }

        private static string BuildChainStatus(X509Certificate2 certificate)
        {
            using (var chain = new X509Chain())
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                chain.Build(certificate);

                if (chain.ChainStatus == null || chain.ChainStatus.Length == 0)
                {
                    return "Valid";
                }

                var parts = new System.Collections.Generic.List<string>();

                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    var status = chain.ChainStatus[i].Status;
                    var text = chain.ChainStatus[i].StatusInformation;

                    string formatted = status.ToString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        formatted += " - " + text.Trim();
                    }

                    if (!parts.Contains(formatted))
                    {
                        parts.Add(formatted);
                    }
                }

                return parts.Count == 0 ? "Valid" : string.Join("; ", parts.ToArray());
            }
        }

        private static string GetSubjectAlternativeNames(X509Certificate2 certificate)
        {
            for (int i = 0; i < certificate.Extensions.Count; i++)
            {
                var extension = certificate.Extensions[i];

                if (extension.Oid != null && extension.Oid.Value == "2.5.29.17")
                {
                    var formatted = new AsnEncodedData(extension.Oid, extension.RawData).Format(true);
                    return string.IsNullOrWhiteSpace(formatted) ? string.Empty : formatted.Trim();
                }
            }

            return string.Empty;
        }

        private void AppendResult(SslCertificateResult result)
        {
            AppendLine("Target: " + result.Host + ":" + result.Port);
            AppendLine("Status: " + (result.Success ? "SUCCESS" : "FAILED"));
            AppendLine("Checked At: " + result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendLine("Elapsed: " + result.Elapsed.TotalMilliseconds.ToString("0.##") + " ms");

            if (!result.Success)
            {
                AppendLine("Message: " + result.Message);
                AppendLine(string.Empty);
                AppendLine("Check complete.");
                return;
            }

            AppendLine("TLS Protocol: " + result.TlsProtocol);
            AppendLine("Subject: " + result.Subject);
            AppendLine("Issuer: " + result.Issuer);
            AppendLine("Valid From: " + result.NotBefore.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendLine("Valid Until: " + result.NotAfter.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendLine("Days Remaining: " + result.DaysRemaining.ToString("0.##"));
            AppendLine("Thumbprint: " + result.Thumbprint);
            AppendLine("Serial Number: " + result.SerialNumber);
            AppendLine("Signature Algorithm: " + result.SignatureAlgorithm);
            AppendLine("Chain Status: " + result.ChainStatus);

            if (!string.IsNullOrWhiteSpace(result.SubjectAlternativeNames))
            {
                AppendLine("Subject Alternative Names:");
                AppendLine(result.SubjectAlternativeNames);
            }

            AppendLine(string.Empty);
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

    public class SslCertificateResult
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public bool Success { get; set; }

        public DateTime CheckedAt { get; set; }

        public TimeSpan Elapsed { get; set; }

        public string TlsProtocol { get; set; }

        public string Subject { get; set; }

        public string Issuer { get; set; }

        public DateTime NotBefore { get; set; }

        public DateTime NotAfter { get; set; }

        public double DaysRemaining { get; set; }

        public string Thumbprint { get; set; }

        public string SerialNumber { get; set; }

        public string SignatureAlgorithm { get; set; }

        public string SubjectAlternativeNames { get; set; }

        public string ChainStatus { get; set; }

        public string Message { get; set; }
    }
}
