using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace NetTools.Plugin.NtpClient
{
    public sealed class NtpQueryResult
    {
        public string Server { get; set; }
        public string ResolvedAddress { get; set; }
        public DateTime LocalTimeUtc { get; set; }
        public DateTime ServerTimeUtc { get; set; }
        public double OffsetMilliseconds { get; set; }
        public double RoundTripMilliseconds { get; set; }

        public DateTime CorrectedUtc => LocalTimeUtc.AddMilliseconds(OffsetMilliseconds);
        public DateTime CorrectedLocal => CorrectedUtc.ToLocalTime();
    }

    public static class NtpClientHelper
    {
        private static readonly DateTime NtpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const int NtpPort = 123;
        private const int PacketSize = 48;

        public static bool IsRunningAsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static async Task<NtpQueryResult> QueryAsync(
            string server,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                throw new ArgumentException("NTP server is required.", nameof(server));
            }

            var addresses = await Dns.GetHostAddressesAsync(server.Trim());
            if (addresses.Length == 0)
            {
                throw new InvalidOperationException("Could not resolve NTP server.");
            }

            var address = addresses[0];
            foreach (var candidate in addresses)
            {
                if (candidate.AddressFamily == AddressFamily.InterNetwork)
                {
                    address = candidate;
                    break;
                }
            }

            var packet = new byte[PacketSize];
            packet[0] = 0x1B;

            using (var client = new UdpClient(address.AddressFamily))
            {
                client.Client.ReceiveTimeout = timeoutMs;
                client.Client.SendTimeout = timeoutMs;

                var remote = new IPEndPoint(address, NtpPort);
                var t1 = DateTime.UtcNow;

                await client.SendAsync(packet, packet.Length, remote);

                var receiveTask = client.ReceiveAsync();
                var completed = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs, cancellationToken));
                if (completed != receiveTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("NTP request timed out.");
                }

                var response = await receiveTask;
                var t4 = DateTime.UtcNow;

                if (response.Buffer == null || response.Buffer.Length < PacketSize)
                {
                    throw new InvalidOperationException("Invalid NTP response.");
                }

                var t2 = ReadTimestamp(response.Buffer, 32);
                var t3 = ReadTimestamp(response.Buffer, 40);
                var offset = ((t2 - t1).TotalMilliseconds + (t3 - t4).TotalMilliseconds) / 2.0;
                var delay = (t4 - t1).TotalMilliseconds - (t3 - t2).TotalMilliseconds;

                return new NtpQueryResult
                {
                    Server = server.Trim(),
                    ResolvedAddress = address.ToString(),
                    LocalTimeUtc = t4,
                    ServerTimeUtc = t3,
                    OffsetMilliseconds = offset,
                    RoundTripMilliseconds = delay
                };
            }
        }

        public static bool TrySetSystemTimeUtc(DateTime utc, out string error)
        {
            var systemTime = new SystemTime
            {
                Year = (ushort)utc.Year,
                Month = (ushort)utc.Month,
                Day = (ushort)utc.Day,
                Hour = (ushort)utc.Hour,
                Minute = (ushort)utc.Minute,
                Second = (ushort)utc.Second,
                Milliseconds = (ushort)utc.Millisecond
            };

            if (!NativeMethods.SetSystemTime(ref systemTime))
            {
                error = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            error = null;
            return true;
        }

        public static string FormatResult(NtpQueryResult result)
        {
            return
                $"Server: {result.Server} [{result.ResolvedAddress}]" + Environment.NewLine +
                $"Local time (UTC):   {result.LocalTimeUtc:yyyy-MM-dd HH:mm:ss.fff}" + Environment.NewLine +
                $"Server time (UTC):  {result.ServerTimeUtc:yyyy-MM-dd HH:mm:ss.fff}" + Environment.NewLine +
                $"Corrected time (UTC):   {result.CorrectedUtc:yyyy-MM-dd HH:mm:ss.fff}" + Environment.NewLine +
                $"Corrected time (Local): {result.CorrectedLocal:yyyy-MM-dd HH:mm:ss.fff}" + Environment.NewLine +
                $"Offset: {result.OffsetMilliseconds:0.###} ms" + Environment.NewLine +
                $"Round-trip delay: {result.RoundTripMilliseconds:0.###} ms";
        }

        private static DateTime ReadTimestamp(byte[] buffer, int offset)
        {
            var seconds =
                ((uint)buffer[offset] << 24) |
                ((uint)buffer[offset + 1] << 16) |
                ((uint)buffer[offset + 2] << 8) |
                buffer[offset + 3];

            var fraction =
                ((uint)buffer[offset + 4] << 24) |
                ((uint)buffer[offset + 5] << 16) |
                ((uint)buffer[offset + 6] << 8) |
                buffer[offset + 7];

            var fractionTicks = (long)(fraction * (double)TimeSpan.TicksPerSecond / uint.MaxValue);
            return NtpEpoch.AddSeconds(seconds).AddTicks(fractionTicks);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemTime
        {
            public ushort Year;
            public ushort Month;
            public ushort DayOfWeek;
            public ushort Day;
            public ushort Hour;
            public ushort Minute;
            public ushort Second;
            public ushort Milliseconds;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetSystemTime(ref SystemTime lpSystemTime);
        }
    }
}
