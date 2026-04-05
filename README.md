# 💎 Obsidian Monitor

A sleek, modern, and highly customizable performance HUD for Windows. Monitor your CPU, GPU, RAM, FPS, and more with a premium, glass-like desktop widget.

![Banner](logo.ico)

## 🚀 Quick Start (Recommended)

The easiest way to get started is to use the pre-built installer. It handles permissions, shortcuts, and background startup automatically.

1. **Download and Run**: [**`ObsidianMonitor_Setup.exe`**](ObsidianMonitor_Setup.exe)
2. **Follow the Setup**: The application will install to your system and can be launched from the Start menu.
3. **Enjoy**: The HUD will appear immediately, providing real-time hardware metrics.

### 🔄 Upgrading (v2.2.0)
If you already have an older version installed, simply download and run the new `ObsidianMonitor_Setup.exe`. The installer will automatically overwrite the previous version and preserve your settings. No manual uninstallation is required.

---

## 🛠️ For Developers (Build from Source)

If you'd like to build the project yourself, follow these steps.

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10/11 (with Administrator privileges for hardware monitoring)

### Building and Running
1. **Clone the repository**:
   ```powershell
   git clone https://github.com/YourUsername/Obsidian-Monitor.git
   cd Obsidian-Monitor
   ```

2. **Run the application**:
   ```powershell
   # Restore and run directly
   dotnet run --project WindowApp.csproj
   ```

3. **Build executable**:
   ```powershell
   # Create a standalone release build
   dotnet build -c Release
   ```
   The executable will be located in `bin/Release/net9.0-windows/Obsidian Monitor.exe`.

---

## ✨ Features

- **Real-time Hardware Monitoring**: Track CPU & GPU usage/temps, RAM consumption, Fan speeds, and **Network Throughput**.
- **Game Metrics**: Live FPS tracking, 1% Lows, and Latency monitoring.
- **Premium Design**: Modern Capsule UI with smooth animations and customizable themes (Glass, Obsidian, White).
- **Interactive HUD**: Drag and reorder monitor elements directly on your desktop.
- **Global Hotkeys**: Quickly toggle the HUD visibility (Default: `Alt + S`).
- **Auto-Start**: Seamlessly start with Windows via Task Scheduler (pre-configured with high privileges via installer).

## 💡 Usage Tips

- **Reorder Capsules**: Click and hold a capsule to drag it left or right. To move the entire window, drag it vertically.
- **Settings**: Right-click the Tray Icon (Diamond logo) and select **Settings** to customize scale, themes, and monitor toggles.
- **Visibility**: Toggle the HUD anytime using `Alt + S`.

## 📦 Dependencies

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) - Hardware sensing.
- [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks) - Visual icons.
- [TraceEvent](https://github.com/microsoft/perfview) - FPS statistics.

## 🤝 Contributing

Contributions are welcome! Feel free to fork and submit pull requests.

## 📄 License

[MIT](LICENSE)
