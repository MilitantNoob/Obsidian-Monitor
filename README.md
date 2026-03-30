# 💎 Obsidian Monitor

A sleek, modern, and highly customizable performance HUD for Windows. Monitor your CPU, GPU, RAM, FPS, and more with a premium, glass-like desktop widget.

![Banner](logo.ico)

## ✨ Features

- **Real-time Hardware Monitoring**: Track CPU & GPU usage/temps, RAM consumption, and Fan speeds.
- **Game Metrics**: Live FPS tracking, 1% Lows, and Latency monitoring.
- **Premium Design**: Modern Capsule UI with smooth animations and customizable themes (Glass, Obsidian, White).
- **Interactive HUD**: Drag and reorder monitor elements directly on your desktop.
- **Global Hotkeys**: Quickly toggle the HUD visibility (Default: `Alt + S`).
- **Auto-Start**: Seamlessly start with Windows via Task Scheduler (pre-configured with high privileges).

## 🚀 Getting Started

### Option 1: Direct Run (For Users)

1. Download the latest release.
2. Locate `Obsidian Monitor.exe` in the `bin/Release` folder (or your installation dir).
3. Run the executable. It requires **Administrator privileges** to monitor hardware and FPS correctly.

### Option 2: Build from Source (For Developers)

**Prerequisites**:
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10/11

**Clone & Run**:
```powershell
# Clone the repository
git clone https://github.com/YourUsername/Obsidian-Monitor.git
cd Obsidian-Monitor

# Build and Run
dotnet build
dotnet run --project WindowApp.csproj
```

## 🛠️ Usage Tips

- **Reorder Capsules**: Click and hold a capsule to drag it left or right. To move the entire window, drag it vertically.
- **Settings**: Right-click the Tray Icon (Diamond logo) and select **Settings** to customize scale, themes, and monitor toggles.
- **Visibility**: Toggle the HUD anytime using `Alt + S`.

## 📦 Dependencies

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) - Hardware sensing.
- [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks) - Visual icons.
- [TraceEvent](https://github.com/microsoft/perfview) - FPS statistics.

## 🤝 Contributing

Feel free to fork this project and submit pull requests. For major changes, please open an issue first to discuss what you would like to change.

## 📄 License

[MIT](LICENSE)
