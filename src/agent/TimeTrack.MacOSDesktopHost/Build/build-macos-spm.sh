#!/bin/bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BUILD_DIR="${PROJECT_ROOT}/build/macos"
SWIFT_DIR="${PROJECT_ROOT}/src/agent/TimeTrack.MacOSDesktopHost/Swift"
APP_NAME="TimeTrack"
APP_BUNDLE="${BUILD_DIR}/${APP_NAME}.app"

echo "=== Building macOS App (VS Code + SPM) ==="
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

echo "[1/6] Building React UI..."
cd "${PROJECT_ROOT}/src/ui/timetrack-ui"
if [ -d "node_modules" ]; then
    npm install
fi
if [ ! -d "node_modules" ]; then
    npm install
fi
npm run build
mkdir -p "${APP_BUNDLE}/Contents/Resources"
cp -r dist "${APP_BUNDLE}/Contents/Resources/"
echo "  React UI built"

echo "[2/6] Building Swift binary..."
cd "${SWIFT_DIR}"
swift build -c release --product TimeTrack
cp .build/release/TimeTrack "${APP_BUNDLE}/Contents/MacOS/"
echo "  Swift binary built"

echo "[3/6] Building .NET Agent..."
cd "${PROJECT_ROOT}"
dotnet publish src/agent/TimeTrack.MacOSAgentService -c Release -r osx-arm64 --self-contained true -o "${APP_BUNDLE}/Contents/MacOS/agent" /p:PublishTrimmed=false
echo "  .NET Agent built"

echo "[4/6] Creating Info.plist..."
cp src/agent/TimeTrack.MacOSDesktopHost/Resources/Info.plist "${APP_BUNDLE}/Contents/Info.plist"

echo "[5/6] Creating LaunchAgent..."
mkdir -p "${BUILD_DIR}/launchagent"
cp src/agent/TimeTrack.MacOSAgentService/Resources/com.cyriusx.timetrack.agent.plist "${BUILD_DIR}/launchagent/com.cyriusx.timetrack.agent.plist"

echo "[6/6] Copying Sparkle Framework..."
mkdir -p "${APP_BUNDLE}/Contents/Frameworks"
cp -R .build/checkouts/Sparkle/XCFrameworks/Sparkle.xcframework "${APP_BUNDLE}/Contents/Frameworks/" 2>/dev/null || echo "  Sparkle not built yet (will be fetched on first SPM resolve)"

echo ""
echo "=== Build Complete ==="
echo "App Bundle: ${APP_BUNDLE}"
echo ""
echo "To run: open ${APP_BUNDLE}"
