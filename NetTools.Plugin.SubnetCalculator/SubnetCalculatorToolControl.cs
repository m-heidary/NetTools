using System;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools.PluginContracts;

namespace NetTools.Plugin.SubnetCalculator
{
    public partial class SubnetCalculatorToolControl : UserControl
    {
        private readonly IPluginHostContext _context;

        private readonly TextBox _ipTextBox;
        private readonly NumericUpDown _prefixInput;
        private readonly Button _calculateButton;
        private readonly RichTextBox _outputBox;

        public SubnetCalculatorToolControl(IPluginHostContext context)
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

            _ipTextBox = new TextBox
            {
                Left = 8,
                Top = 8,
                Width = 220
            };

            _prefixInput = new NumericUpDown
            {
                Left = 240,
                Top = 8,
                Width = 80,
                Minimum = 0,
                Maximum = 32,
                Value = 24
            };

            _calculateButton = new Button
            {
                Left = 332,
                Top = 6,
                Width = 100,
                Height = 28,
                Text = "Calculate"
            };

            topPanel.Controls.Add(new Label
            {
                Left = 8,
                Top = 36,
                AutoSize = true,
                Text = "IPv4 Address"
            });

            topPanel.Controls.Add(new Label
            {
                Left = 240,
                Top = 36,
                AutoSize = true,
                Text = "CIDR Prefix"
            });

            topPanel.Controls.Add(_ipTextBox);
            topPanel.Controls.Add(_prefixInput);
            topPanel.Controls.Add(_calculateButton);

            _outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 10F)
            };

            Controls.Add(_outputBox);
            Controls.Add(topPanel);

            _calculateButton.Click += (s, e) => RunCalculation();
        }

        private void RunCalculation()
        {
            var ipText = _ipTextBox.Text.Trim();
            var prefixLength = (int)_prefixInput.Value;

            if (string.IsNullOrWhiteSpace(ipText))
            {
                MessageBox.Show(
                    this,
                    "IPv4 address is required.",
                    "Subnet Calculator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SubnetCalculationResult result;
            if (!TryCalculate(ipText, prefixLength, out result))
            {
                MessageBox.Show(
                    this,
                    "Please enter a valid IPv4 address and prefix.",
                    "Subnet Calculator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            AppendResult(result);
        }

        public static bool TryCalculate(string ipText, int prefixLength, out SubnetCalculationResult result)
        {
            result = null;

            if (prefixLength < 0 || prefixLength > 32)
            {
                return false;
            }

            IPAddress ipAddress;
            if (!IPAddress.TryParse(ipText, out ipAddress))
            {
                return false;
            }

            var bytes = ipAddress.GetAddressBytes();
            if (bytes.Length != 4)
            {
                return false;
            }

            uint ip = ToUInt32(bytes);
            uint mask = PrefixToMask(prefixLength);
            uint wildcard = ~mask;
            uint network = ip & mask;
            uint broadcast = network | wildcard;

            ulong totalAddresses = prefixLength == 32 ? 1UL : (1UL << (32 - prefixLength));
            ulong usableHosts = CalculateUsableHosts(prefixLength);

            uint firstUsable = network;
            uint lastUsable = broadcast;

            if (prefixLength <= 30)
            {
                firstUsable = network + 1;
                lastUsable = broadcast - 1;
            }

            result = new SubnetCalculationResult
            {
                InputAddress = ToIpString(ip),
                PrefixLength = prefixLength,
                SubnetMask = ToIpString(mask),
                WildcardMask = ToIpString(wildcard),
                NetworkAddress = ToIpString(network),
                BroadcastAddress = ToIpString(broadcast),
                FirstUsableAddress = ToIpString(firstUsable),
                LastUsableAddress = ToIpString(lastUsable),
                TotalAddresses = totalAddresses,
                UsableHostCount = usableHosts,
                AddressClass = GetAddressClass(bytes[0]),
                IsPrivateRange = IsPrivateRange(ip)
            };

            return true;
        }

        private static uint PrefixToMask(int prefixLength)
        {
            if (prefixLength == 0)
            {
                return 0U;
            }

            return 0xFFFFFFFFU << (32 - prefixLength);
        }

        private static ulong CalculateUsableHosts(int prefixLength)
        {
            if (prefixLength == 32)
            {
                return 1;
            }

            if (prefixLength == 31)
            {
                return 2;
            }

            return (1UL << (32 - prefixLength)) - 2;
        }

        private static uint ToUInt32(byte[] bytes)
        {
            return ((uint)bytes[0] << 24)
                | ((uint)bytes[1] << 16)
                | ((uint)bytes[2] << 8)
                | bytes[3];
        }

        private static string ToIpString(uint value)
        {
            return string.Format(
                "{0}.{1}.{2}.{3}",
                (value >> 24) & 255,
                (value >> 16) & 255,
                (value >> 8) & 255,
                value & 255);
        }

        private static string GetAddressClass(byte firstOctet)
        {
            if (firstOctet >= 1 && firstOctet <= 126)
            {
                return "A";
            }

            if (firstOctet >= 128 && firstOctet <= 191)
            {
                return "B";
            }

            if (firstOctet >= 192 && firstOctet <= 223)
            {
                return "C";
            }

            if (firstOctet >= 224 && firstOctet <= 239)
            {
                return "D (Multicast)";
            }

            if (firstOctet >= 240 && firstOctet <= 255)
            {
                return "E (Experimental)";
            }

            return "Unknown";
        }

        private static bool IsPrivateRange(uint ip)
        {
            var first = (byte)((ip >> 24) & 255);
            var second = (byte)((ip >> 16) & 255);

            if (first == 10)
            {
                return true;
            }

            if (first == 172 && second >= 16 && second <= 31)
            {
                return true;
            }

            if (first == 192 && second == 168)
            {
                return true;
            }

            return false;
        }

        private void AppendResult(SubnetCalculationResult result)
        {
            _outputBox.Clear();
            AppendLine("Subnet calculation result");
            AppendLine(string.Empty);
            AppendLine("IP Address: " + result.InputAddress);
            AppendLine("CIDR: /" + result.PrefixLength);
            AppendLine("Subnet Mask: " + result.SubnetMask);
            AppendLine("Wildcard Mask: " + result.WildcardMask);
            AppendLine("Network Address: " + result.NetworkAddress);
            AppendLine("Broadcast Address: " + result.BroadcastAddress);
            AppendLine("First Usable IP: " + result.FirstUsableAddress);
            AppendLine("Last Usable IP: " + result.LastUsableAddress);
            AppendLine("Total Addresses: " + result.TotalAddresses);
            AppendLine("Usable Hosts: " + result.UsableHostCount);
            AppendLine("Address Class: " + result.AddressClass);
            AppendLine("Private Range: " + (result.IsPrivateRange ? "Yes" : "No"));
        }

        private void AppendLine(string text)
        {
            _outputBox.AppendText(text + Environment.NewLine);
        }
    }

    public class SubnetCalculationResult
    {
        public string InputAddress { get; set; }

        public int PrefixLength { get; set; }

        public string SubnetMask { get; set; }

        public string WildcardMask { get; set; }

        public string NetworkAddress { get; set; }

        public string BroadcastAddress { get; set; }

        public string FirstUsableAddress { get; set; }

        public string LastUsableAddress { get; set; }

        public ulong TotalAddresses { get; set; }

        public ulong UsableHostCount { get; set; }

        public string AddressClass { get; set; }

        public bool IsPrivateRange { get; set; }
    }
}
