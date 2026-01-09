# Guitar Tuner

A Windows desktop guitar tuner application built with C# and WPF.

## Features

- Real-time pitch detection using McLeod Pitch Method (MPM)
- Support for multiple tuning profiles (Standard, Drop D, Half Step Down, Open G)
- Visual tuning indicator with sharp/flat feedback
- Low-latency audio capture via WASAPI

## Requirements

- Windows 10/11
- .NET 8.0 SDK
- A microphone or audio input device

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run --project src/Tuner.UI.Win
```

## Testing

```bash
dotnet test
```

## Architecture

The solution follows clean architecture principles:

- **Tuner.Core** - Platform-independent pitch detection and signal processing
- **Tuner.AppContracts** - Shared interfaces and data types
- **Tuner.Audio.Abstractions** - Audio input interface
- **Tuner.Audio.Windows** - Windows WASAPI audio implementation
- **Tuner.UI.Win** - WPF user interface

See [ARCHITECTURE.md](ARCHITECTURE.md) for details.

## License

MIT
