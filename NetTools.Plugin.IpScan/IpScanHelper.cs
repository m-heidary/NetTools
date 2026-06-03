using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetTools.Plugin.IpScan
{
    public sealed class IpScanHostInfo
    {
        public string IpAddress { get; set; }
        public bool IsAlive { get; set; }
        public long RoundTripMs { get; set; }
        public int Ttl { get; set; }
        public string Hostname { get; set; }
        public string MacAddress { get; set; }
        public string NetBiosName { get; set; }
        public string Workgroup { get; set; }
        public string OsGuess { get; set; }
    }

    public static class IpScanHelper
    {
        public static IReadOnlyList<IPAddress> ParseIpRange(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("IP range is required.", nameof(input));
            }

            input = input.Trim();

            if (input.Contains("/"))
            {
                var parts = input.Split('/');
                if (parts.Length != 2)
                {
                    throw new FormatException("Invalid CIDR notation.");
                }

                var network = IPAddress.Parse(parts[0].Trim());
                if (!int.TryParse(parts[1].Trim(), out var prefix))
                {
                    throw new FormatException("Invalid CIDR prefix.");
                }

                return GenerateCidr(network, prefix).ToList();
            }

            if (input.Contains("-"))
            {
                var parts = input.Split('-');
                if (parts.Length != 2)
                {
                    throw new FormatException("Invalid IP range.");
                }

                var start = IPAddress.Parse(parts[0].Trim());
                var end = IPAddress.Parse(parts[1].Trim());
                return GenerateRange(start, end).ToList();
            }

            return new List<IPAddress> { IPAddress.Parse(input) };
        }

        public static async Task<IpScanHostInfo> ProbeHostAsync(
            IPAddress address,
            int timeoutMs,
            bool resolveDetails,
            CancellationToken cancellationToken)
        {
            var info = new IpScanHostInfo
            {
                IpAddress = address.ToString(),
                IsAlive = false,
                Hostname = "-",
                MacAddress = "-",
                NetBiosName = "-",
                Workgroup = "-",
                OsGuess = "-"
            };

            using (var ping = new Ping())
            {
                PingReply reply;
                try
                {
                    reply = await ping.SendPingAsync(address, timeoutMs);
                }
                catch
                {
                    return info;
                }

                if (reply.Status != IPStatus.Success)
                {
                    return info;
                }

                info.IsAlive = true;
                info.RoundTripMs = reply.RoundtripTime;
                info.Ttl = reply.Options?.Ttl ?? 0;
                info.OsGuess = GuessOsFromTtl(info.Ttl);
            }

            if (!resolveDetails)
            {
                return info;
            }

            await Task.WhenAll(
                ResolveHostnameAsync(info, cancellationToken),
                ResolveMacAddressAsync(info, cancellationToken),
                ResolveNetBiosAsync(info, timeoutMs, cancellationToken));

            return info;
        }

        public static string FormatHostLine(IpScanHostInfo host)
        {
            if (!host.IsAlive)
            {
                return null;
            }

            return string.Format(
                "{0,-15}  RTT={1,4}ms  TTL={2,3}  MAC={3,-17}  Host={4,-20}  NetBIOS={5,-16}  Group={6,-16}  OS={7}",
                host.IpAddress,
                host.RoundTripMs,
                host.Ttl,
                host.MacAddress,
                Truncate(host.Hostname, 20),
                Truncate(host.NetBiosName, 16),
                Truncate(host.Workgroup, 16),
                Truncate(host.OsGuess, 20));
        }

        public static string FormatSummary(int scanned, int alive, TimeSpan elapsed)
        {
            return $"Scan complete. Scanned {scanned} address(es), found {alive} alive host(s) in {elapsed.TotalSeconds:0.##} second(s).";
        }

        public static int CompareIpAddresses(string left, string right)
        {
            return ToUInt32(IPAddress.Parse(left)).CompareTo(ToUInt32(IPAddress.Parse(right)));
        }

        private static IEnumerable<IPAddress> GenerateRange(IPAddress start, IPAddress end)
        {
            var startValue = ToUInt32(start);
            var endValue = ToUInt32(end);

            if (endValue < startValue)
            {
                throw new ArgumentException("End IP must be greater than or equal to start IP.");
            }

            for (var value = startValue; value <= endValue; value++)
            {
                yield return FromUInt32(value);
            }
        }

        private static IEnumerable<IPAddress> GenerateCidr(IPAddress network, int prefix)
        {
            if (network.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException("Only IPv4 CIDR is supported.");
            }

            if (prefix < 8 || prefix > 30)
            {
                throw new NotSupportedException("Supported CIDR prefix range is /8 to /30.");
            }

            var networkValue = ToUInt32(network);
            var hostBits = 32 - prefix;
            var hostCount = (1u << hostBits) - 2u;
            var networkMask = prefix == 0 ? 0u : uint.MaxValue << hostBits;
            var networkBase = networkValue & networkMask;

            yield return FromUInt32(networkBase + 1);
            for (uint i = 1; i < hostCount; i++)
            {
                yield return FromUInt32(networkBase + 1 + i);
            }
        }

        private static async Task ResolveHostnameAsync(IpScanHostInfo info, CancellationToken cancellationToken)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(info.IpAddress).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(entry.HostName))
                {
                    info.Hostname = entry.HostName;
                }
            }
            catch
            {
            }
        }

        private static Task ResolveMacAddressAsync(IpScanHostInfo info, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    var ip = IPAddress.Parse(info.IpAddress);
                    if (ip.AddressFamily != AddressFamily.InterNetwork)
                    {
                        return;
                    }

                    var mac = new byte[6];
                    var length = mac.Length;
                    var ipBytes = ip.GetAddressBytes();
                    var dest = ipBytes[0] | (ipBytes[1] << 8) | (ipBytes[2] << 16) | (ipBytes[3] << 24);

                    if (NativeMethods.SendARP(dest, 0, mac, ref length) == 0 && length >= 6)
                    {
                        info.MacAddress = string.Format(
                            "{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                            mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
                    }
                }
                catch
                {
                }
            }, cancellationToken);
        }

        private static async Task ResolveNetBiosAsync(IpScanHostInfo info, int timeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                var address = IPAddress.Parse(info.IpAddress);
                var packet = BuildNetBiosNodeStatusRequest();

                using (var udp = new UdpClient())
                {
                    udp.Client.ReceiveTimeout = timeoutMs;
                    await udp.SendAsync(packet, packet.Length, new IPEndPoint(address, 137)).ConfigureAwait(false);

                    var receiveTask = udp.ReceiveAsync();
                    var completed = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs, cancellationToken)).ConfigureAwait(false);
                    if (completed != receiveTask)
                    {
                        return;
                    }

                    var response = await receiveTask.ConfigureAwait(false);
                    ParseNetBiosNodeStatus(response.Buffer, info);
                }
            }
            catch
            {
            }
        }

        private static byte[] BuildNetBiosNodeStatusRequest()
        {
            var packet = new byte[50];
            packet[0] = 0x00;
            packet[1] = 0x00;
            packet[2] = 0x00;
            packet[3] = 0x01;
            packet[4] = 0x00;
            packet[5] = 0x00;
            packet[6] = 0x00;
            packet[7] = 0x01;
            packet[8] = 0x00;
            packet[9] = 0x00;
            packet[10] = 0x00;
            packet[11] = 0x00;
            packet[12] = 0x20;
            packet[13] = 0x43;
            packet[14] = 0x4B;

            for (var i = 15; i < 45; i++)
            {
                packet[i] = 0x41;
            }

            packet[45] = 0x00;
            packet[46] = 0x00;
            packet[47] = 0x21;
            packet[48] = 0x00;
            packet[49] = 0x01;
            return packet;
        }

        private static void ParseNetBiosNodeStatus(byte[] response, IpScanHostInfo info)
        {
            if (response == null || response.Length < 57)
            {
                return;
            }

            var nameCount = response[56];
            var offset = 57;

            for (var i = 0; i < nameCount && offset + 18 <= response.Length; i++)
            {
                var name = Encoding.ASCII.GetString(response, offset, 15).Trim();
                var suffix = response[offset + 15];
                var flags = (ushort)(response[offset + 16] | (response[offset + 17] << 8));
                var isGroup = (flags & 0x8000) != 0;

                if (suffix == 0x00 && info.NetBiosName == "-" && !string.IsNullOrWhiteSpace(name))
                {
                    info.NetBiosName = name;
                }

                if (isGroup && info.Workgroup == "-" && !string.IsNullOrWhiteSpace(name))
                {
                    info.Workgroup = name;
                }

                offset += 18;
            }

            var macOffset = 57 + (nameCount * 18);
            if (macOffset + 6 <= response.Length && info.MacAddress == "-")
            {
                info.MacAddress = string.Format(
                    "{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                    response[macOffset],
                    response[macOffset + 1],
                    response[macOffset + 2],
                    response[macOffset + 3],
                    response[macOffset + 4],
                    response[macOffset + 5]);
            }
        }

        private static string GuessOsFromTtl(int ttl)
        {
            if (ttl <= 0)
            {
                return "Unknown";
            }

            if (ttl >= 240)
            {
                return "Network device";
            }

            if (ttl >= 120)
            {
                return "Windows (likely)";
            }

            if (ttl >= 60 && ttl <= 70)
            {
                return "Linux/Unix (likely)";
            }

            if (ttl >= 250)
            {
                return "Cisco/IOS (likely)";
            }

            return "Unknown";
        }

        private static uint ToUInt32(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }

        private static IPAddress FromUInt32(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return new IPAddress(bytes);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength - 3) + "...";
        }

        private static class NativeMethods
        {
            [DllImport("iphlpapi.dll", ExactSpelling = true)]
            public static extern int SendARP(int destIp, int srcIp, byte[] pMacAddr, ref int phyAddrLen);
        }
    }
}
