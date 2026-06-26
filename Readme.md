# NetTools Terminal Emulator Plugin

A terminal emulator plugin for **NetTools**, built with **C#**, **WinForms**, and **.NET Framework 4.8**.

This plugin adds interactive terminal access to NetTools through SSH and Telnet sessions. It is designed around a clean separation between UI rendering, terminal session management, and transport protocols, making it extensible for future protocols such as Serial, Raw TCP, or custom network shells.

---

## Features

- SSH terminal sessions
- Telnet terminal sessions
- WinForms-based terminal UI
- Multiple terminal sessions using tabs
- Session lifecycle management
  - Connect
  - Disconnect
  - Reconnect
  - Close session
- Keyboard input forwarding
- Mouse selection support
- Copy and paste support
- Terminal clear operation
- Resize-aware terminal sessions
- Thread-safe UI updates from background transport events
- Extensible transport layer
- Plugin-based integration with NetTools

---

## Project Target

The plugin ecosystem targets **.NET Framework 4.8**.

The plugin contracts project defines:

- `TargetFrameworkVersion`: `v4.8`
- Root namespace: `NetTools.PluginContracts`
- Assembly name: `NetTools.PluginContracts`

Source: `NetTools.PluginContracts.csproj`, lines 7-9 and 17-22.

---

## Architecture Overview

The terminal emulator is organized into separate layers:
```text
Transport
  -> TerminalSession
-> TerminalEmulatorToolControl
-> SessionTabPage
-> TerminalControl
-> AnsiTerminalParser
-> TerminalBuffer
-> WinForms Paint

### 1. Plugin Layer

The plugin entry point exposes the terminal emulator to the NetTools host application.

Typical responsibilities:

- Provide plugin metadata
- Create the WinForms tool control
- Receive host context
- Use host logging services where available

The contracts project references `System.Windows.Forms`, which indicates support for WinForms-based plugin UI components.

Source: `NetTools.PluginContracts.csproj`, lines 25-33.

---

### 2. UI Layer

The UI layer is responsible for the user-facing terminal experience.

Main components:

- `TerminalEmulatorToolControl`
- `SessionTabPage`
- `TerminalControl`
- `ConnectDialog`

Responsibilities:

- Manage terminal tabs
- Open and close sessions
- Reconnect sessions
- Forward toolbar commands to the active terminal
- Render terminal output
- Handle copy, paste, selection, and keyboard input

---

### 3. Session Layer

The session layer acts as an abstraction between the UI and the underlying protocol transport.

Main components:

- `TerminalSession`
- `TerminalSessionOptions`
- `TerminalTransportFactory`

Responsibilities:

- Store connection options
- Create the correct transport implementation
- Expose session events
- Forward terminal input to the transport
- Handle resize events
- Provide helper methods for control keys, arrow keys, and function keys

---

### 4. Transport Layer

The transport layer handles protocol-specific communication.

Implemented transports:

- SSH transport
- Telnet transport

#### SSH Transport

The SSH transport uses an interactive shell stream and supports:

- Password authentication
- Private key authentication
- Terminal resize notifications
- Bidirectional terminal I/O

#### Telnet Transport

The Telnet transport includes a state-machine-based parser for Telnet negotiation.

Supported Telnet behavior includes:

- IAC command processing
- Option negotiation
- Subnegotiation handling
- NAWS terminal size negotiation
- Terminal type negotiation

---

### 5. Terminal Rendering Layer

The terminal rendering layer is responsible for converting incoming terminal data into visual output.

Core components:

- `TerminalControl`
- `AnsiTerminalParser`
- `TerminalBuffer`
- `TerminalCell`
- `TerminalStyle`

Planned or expected ANSI/VT support:

- Normal text output
- Carriage return
- Line feed
- Backspace
- Tab
- Cursor movement
- Screen clearing
- Line clearing
- ANSI color sequences
- Basic text styles
- Scroll behavior
- Terminal title escape sequence support

---

## Plugin Contracts

The shared plugin contract project is named:

text
NetTools.PluginContracts

It includes the following compile items:

text
Contracts.cs
Properties\AssemblyInfo.cs

Source: `NetTools.PluginContracts.csproj`, lines 35-40.

The contract assembly is expected to define the interfaces required by NetTools plugins, such as plugin metadata, host context access, logging, and WinForms tool creation.

---

## Dependencies

The plugin contracts project references standard .NET Framework assemblies, including:

- `System`
- `System.Core`
- `System.Windows.Forms`
- `System.Xml.Linq`
- `System.Data.DataSetExtensions`
- `Microsoft.CSharp`
- `System.Data`
- `System.Net.Http`
- `System.Xml`

Source: `NetTools.PluginContracts.csproj`, lines 25-33.

The terminal emulator plugin may additionally require an SSH client library, such as SSH.NET, for SSH session support.

---

## Suggested Solution Structure

text
NetTools.PluginContracts/
  Contracts.cs
  NetTools.PluginContracts.csproj

NetTools.Plugin.TerminalEmulator/
  TerminalEmulatorToolPlugin.cs
  TerminalEmulatorToolControl.cs

  UI/
ConnectDialog.cs
SessionTabPage.cs

  Sessions/
TerminalSession.cs
TerminalSessionOptions.cs

  Transports/
ITerminalTransport.cs
TerminalTransportFactory.cs
SshTransport.cs
TelnetTransport.cs

  Terminal/
TerminalControl.cs
TerminalBuffer.cs
TerminalCell.cs
TerminalStyle.cs
AnsiTerminalParser.cs

---

## Building

Requirements:

- Windows
- Visual Studio 2019 or newer
- .NET Framework 4.8 Developer Pack

Build the solution in Visual Studio or using MSBuild:

powershell
msbuild NetTools.sln /p:Configuration=Release

If building only the plugin project:

powershell
msbuild NetTools.Plugin.TerminalEmulator.csproj /p:Configuration=Release

---

## Usage

1. Build the plugin project.
2. Copy the compiled plugin assembly to the NetTools plugin directory.
3. Start NetTools.
4. Open the Terminal Emulator tool from the plugin/tool list.
5. Create a new session.
6. Select SSH or Telnet.
7. Enter host, port, and authentication details.
8. Connect.

---

## Session Flow

text
User creates a session
  -> TerminalEmulatorToolControl creates TerminalSessionOptions
  -> TerminalTransportFactory creates SSH or Telnet transport
  -> TerminalSession connects transport
  -> Transport emits DataReceived events
  -> UI marshals events to WinForms UI thread
  -> TerminalControl parses and renders output

---

## Thread Safety

Transport events may be raised from background threads. For this reason, all UI updates must be marshaled to the WinForms UI thread before touching controls.

The recommended pattern is:

csharp
if (InvokeRequired)
{
BeginInvoke(action);
return;
}

action();

This prevents cross-thread operation exceptions and keeps terminal sessions stable during asynchronous network activity.

---

## Extending Transports

New protocols can be added by implementing the terminal transport abstraction.

Example future transports:

- Serial
- Raw TCP
- Local shell
- PowerShell remoting
- Custom device console

A new transport should handle:

- Connect
- Disconnect
- Send data
- Receive data
- Resize notification, if supported
- Error reporting

Then register it in the transport factory.

---

## Roadmap

- Complete ANSI/VT100 parser
- Add richer terminal color support
- Add scrollback buffer
- Add configurable terminal themes
- Add saved connection profiles
- Add SSH key passphrase support
- Add session logging
- Add search in terminal output
- Add copy-on-select option
- Add configurable font and cursor style

---

## License

Add the project license here.

Example:

text
MIT License

---

## Status

This plugin is under active development.

The current architecture focuses on:

- Clean plugin integration
- SSH/Telnet transport separation
- Thread-safe WinForms UI updates
- A foundation for ANSI terminal rendering

