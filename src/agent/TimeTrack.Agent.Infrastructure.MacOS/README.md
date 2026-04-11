# TimeTrack.Agent.Infrastructure.MacOS

macOS-specific implementations for the TimeTrack Agent infrastructure layer.

## Overview

This project provides macOS implementations of the platform-specific interfaces defined in `TimeTrack.Agent.Contracts`. It allows the TimeTrack agent to run on macOS while maintaining the same clean architecture and code sharing with the Windows version.

## Components

### Providers

- **MacOSActiveWindowProvider**: Tracks the active window using `NSWorkspace` and `AXUIElement` API
  - Uses Accessibility APIs for window title extraction
  - Supports browser URL extraction for Safari, Chrome, Firefox, Brave, Edge
  - Implements caching for performance

- **MacOSIdleDetector**: Detects user idle time using `CoreGraphics` `CGEventSourceSecondsSinceLastEventType`
  - Sleep/wake detection
  - Configurable idle threshold

- **MacOSMachineMetricsProvider**: Collects system metrics (CPU, Memory, Disk)
  - CPU usage via `sysctl` and `hw.cpustats`
  - Memory usage via `vm.*` sysctl
  - Disk usage via filesystem APIs

- **MacOSFilePathExtractor**: Extracts file paths from active windows
  - Supports Finder, VS Code, JetBrains IDEs, and other common macOS apps
  - Parses window titles to extract file/project paths

### Security

- **KeychainTokenStore**: Secure token storage using macOS Keychain
  - Replaces Windows DPAPI
  - Stores JWT and refresh tokens securely
  - Implements automatic token refresh

### Notifications

- **MacOSNotificationService**: Displays notifications using `UserNotifications` framework
  - Supports notification actions (primary/secondary)
  - Action callbacks via IPC
  - Category-based notification grouping

## Usage

### Registering Services

```csharp
// Add macOS-specific providers
services.AddMacOSProviders();

// Add sync services with Keychain token storage
services.AddMacOSSyncServices(settings);

// Or use null implementations for local-only mode
services.AddNullSyncTransport();
```

### Building

```bash
dotnet build TimeTrack.Agent.Infrastructure.MacOS.csproj

# For specific platform
dotnet build -r osx-arm64   # Apple Silicon
dotnet build -r osx-x64     # Intel
```

## Requirements

- macOS 12.0 or later
- .NET 8.0 or later
- Accessibility permissions (required for window tracking)
- Keychain access (for token storage)

## Permissions

The application requires the following macOS permissions:

### Accessibility
- Required for: Active window tracking, window title extraction
- Location: System Settings → Privacy & Security → Accessibility

### File System
- Required for: Database storage, log files
- Location: App bundle sandbox or user home directory

### Network
- Required for: Backend API communication
- Location: Network entitlements in `.entitlements` file

## Architecture Notes

This project follows the same clean architecture principles as the Windows version:

1. **Portable Contracts**: All interfaces are in `TimeTrack.Agent.Contracts` (shared across platforms)
2. **Platform-Specific Implementations**: This project provides macOS implementations
3. **Shared Domain**: Business logic remains in `TimeTrack.Agent.Domain`
4. **Shared Application Layer**: Application services in `TimeTrack.Agent.Application`

## Testing

Unit tests for macOS-specific implementations should be created in `TimeTrack.Agent.Tests.MacOS` (to be created).

## Migration from Windows

To migrate an existing Windows installation to macOS:

1. Install the macOS version of the agent
2. Re-activate the device (device IDs are platform-specific)
3. The backend will treat it as a new device
4. Historical data remains on the Windows installation

## Known Limitations

- Browser URL extraction may not work for all browsers due to macOS security restrictions
- Some Electron apps may not expose their window titles via Accessibility
- LaunchAgent/launchd integration required for background service mode

## See Also

- [Windows Implementation](../TimeTrack.Agent.Infrastructure/)
- [Contracts](../TimeTrack.Agent.Contracts/)
- [Domain](../TimeTrack.Agent.Domain/)
- [Application](../TimeTrack.Agent.Application/)
