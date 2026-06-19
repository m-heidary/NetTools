using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.SslCertificate
{
	public class SslCertificateToolPlugin : IConsoleNetworkToolPlugin, IWinFormsNetworkToolPlugin
	{
		public string Id => "SslCertificate";
		public string DisplayName => "SSL/TLS Certificate Checker";
		public string Description => "Inspect the SSL/TLS certificate and protocol details of a remote host.";

		public UserControl CreateToolControl(IPluginHostContext context)
		{
			return new SslCertificateToolControl(context);
		}

		public async Task RunConsoleAsync(IPluginHostContext context, CancellationToken cancellationToken)
		{
			Console.Write("Enter hostname or URL: ");
			var input = Console.ReadLine();

			if (string.IsNullOrWhiteSpace(input))
			{
				Console.WriteLine("Host or URL is required.");
				return;
			}

			string host;
			int port;
			if (!SslCertificateToolControl.TryParseTarget(input, out host, out port))
			{
				Console.WriteLine("Invalid host or URL.");
				return;
			}

			Console.Write("Port [default " + port + "]: ");
			var portText = Console.ReadLine();

			if (!string.IsNullOrWhiteSpace(portText))
			{
				int parsedPort;
				if (int.TryParse(portText, out parsedPort) && parsedPort > 0 && parsedPort <= 65535)
				{
					port = parsedPort;
				}
			}

			Console.Write("Timeout in milliseconds [default 10000]: ");
			var timeoutText = Console.ReadLine();

			int timeoutMilliseconds = 10000;
			if (!string.IsNullOrWhiteSpace(timeoutText))
			{
				int parsedTimeout;
				if (int.TryParse(timeoutText, out parsedTimeout) && parsedTimeout > 0)
				{
					timeoutMilliseconds = parsedTimeout;
				}
			}

			Console.WriteLine();

			var result = await SslCertificateToolControl.CheckCertificateAsync(
				host,
				port,
				timeoutMilliseconds,
				cancellationToken);

			PrintResult(result);
		}

		private static void PrintResult(SslCertificateResult result)
		{
			Console.WriteLine("Target: " + result.Host + ":" + result.Port);
			Console.WriteLine("Status: " + (result.Success ? "SUCCESS" : "FAILED"));
			Console.WriteLine("Checked At: " + result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss"));
			Console.WriteLine("Elapsed: " + result.Elapsed.TotalMilliseconds.ToString("0.##") + " ms");

			if (!result.Success)
			{
				Console.WriteLine("Message: " + result.Message);
				return;
			}

			Console.WriteLine("TLS Protocol: " + result.TlsProtocol);
			Console.WriteLine("Certificate Subject: " + result.Subject);
			Console.WriteLine("Certificate Issuer: " + result.Issuer);
			Console.WriteLine("Valid From: " + result.NotBefore.ToString("yyyy-MM-dd HH:mm:ss"));
			Console.WriteLine("Valid Until: " + result.NotAfter.ToString("yyyy-MM-dd HH:mm:ss"));
			Console.WriteLine("Days Remaining: " + result.DaysRemaining.ToString("0.##"));
			Console.WriteLine("Thumbprint: " + result.Thumbprint);
			Console.WriteLine("Serial Number: " + result.SerialNumber);
			Console.WriteLine("Signature Algorithm: " + result.SignatureAlgorithm);
			Console.WriteLine("Chain Status: " + result.ChainStatus);

			if (!string.IsNullOrWhiteSpace(result.SubjectAlternativeNames))
			{
				Console.WriteLine("Subject Alternative Names:");
				Console.WriteLine(result.SubjectAlternativeNames);
			}
		}
	}
}
