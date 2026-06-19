:::writing block

Network Toolkit (Plugin‑Based)
A modular network diagnostics and analysis toolkit built with a plugin-based architecture.

The project provides both Console and Windows Forms GUI interfaces, allowing users to run various networking tools through dynamically loaded plugins.

The goal of this project is to create an extensible network utility platform where new tools can easily be added without modifying the core application.

Screenshots
Windows Forms Interface


Console Interface


Running a Plugin


Features
Plugin-based architecture
Supports both Console and Windows Forms UI
Easy to extend with new plugins
Modular design
Lightweight and fast
Ideal for network diagnostics, troubleshooting, and testing
Available Plugins
Plugin	Description
DNSLookup	Query DNS records for a domain
HttpInspector	Inspect HTTP responses and headers
IPScan	Scan a range of IP addresses
NtpClient	Query time from NTP servers
Ping	Test connectivity to a host
PortScan	Scan open ports on a target
SSLCertificate	Retrieve SSL certificate information
SubnetCalculator	Perform subnet calculations
Traceroute	Trace route to a destination
WebsiteStatusChecker	Check website availability
Architecture
The project follows a plugin-based architecture.

Core components:

Core Application

Plugin Loader
Plugin Interface
Execution Engine
Plugins

Implement the shared plugin interface
Loaded dynamically at runtime
Extend the functionality of the toolkit
Simplified architecture:

text
Core Application
│
├── Plugin Loader
│
├── Plugin Interface
│
└── Plugins
    ├── Ping
    ├── PortScan
    ├── DNSLookup
    ├── Traceroute
    └── ...
Project Structure
text
NetworkToolkit
│
├── Core
│   └── Plugin interfaces and base classes
│
├── ConsoleApp
│   └── Command-line interface
│
├── WinFormsApp
│   └── Graphical interface
│
├── Plugins
│   ├── Ping
│   ├── PortScan
│   ├── DNSLookup
│   ├── Traceroute
│   └── ...
│
└── docs
    └── images
Installation
Clone the repository:

bash
git clone https://github.com/m-heidary/NetTools.git
Open the solution in Visual Studio and build the project.

Running the Application
Console Version
bash
dotnet run --project ConsoleApp
Windows Forms Version
Run the WinForms project from Visual Studio.

Creating a New Plugin
Create a new class library project.
Reference the Core project.
Implement the plugin interface.
Example:

csharp
public class MyPlugin : INetworkToolPlugin
{
    public string Name => "My Plugin";

    public void Execute()
    {
        Console.WriteLine("My plugin is running");
    }
}
Place the compiled plugin inside the Plugins directory.

The application will automatically load it at runtime.

Roadmap
Future improvements may include:

Plugin marketplace
Packet capture plugin
Whois lookup
Network speed test
Export results (JSON / CSV)
Logging system
Cross-platform GUI
Contributing
Contributions are welcome.

If you’d like to add a new plugin or improve the core system:

Fork the repository
Create a new branch
Commit your changes
Open a Pull Request
License
This project is licensed under the MIT License.

Author
Developed as a modular network utility toolkit focused on extensibility and plugin-based design.

:::