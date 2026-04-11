#!/bin/bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BUILD_DIR="${PROJECT_ROOT}/build/macos"
APP_NAME="TimeTrack"
APP_BUNDLE="${BUILD_DIR}/${APP_NAME}.app"
BUNDLE_ID="com.cyriusx.timetrack"

echo "=== Building macOS App Bundle ==="

rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"
mkdir -p "${APP_BUNDLE}/Contents/Frameworks"

echo "[1/6] Building React UI..."
UI_DIR="${PROJECT_ROOT}/src/ui/timetrack-ui"
if [ -d "${UI_DIR}" ]; then
    (cd "${UI_DIR}" && npm install && npm run build)
    cp -r "${UI_DIR}/dist" "${APP_BUNDLE}/Contents/Resources/dist"
    echo "  React UI copied to bundle"
else
    echo "  WARNING: UI directory not found, creating placeholder"
    mkdir -p "${APP_BUNDLE}/Contents/Resources/dist"
    echo '<html><body><h2>TimeTrack</h2><p>UI not built. Run npm run build in src/ui/timetrack-ui/</p></body></html>' \
        > "${APP_BUNDLE}/Contents/Resources/dist/index.html"
fi

echo "[2/6] Building macOS Agent Service..."
AGENT_SERVICE_DIR="${PROJECT_ROOT}/src/agent/TimeTrack.MacOSAgentService"
dotnet publish "${AGENT_SERVICE_DIR}" \
    -c Release \
    -r osx-arm64 \
    --self-contained true \
    -o "${APP_BUNDLE}/Contents/MacOS/agent" \
    /p:PublishTrimmed=false

echo "[3/6] Copying Swift sources for Xcode build..."
SWIFT_DIR="${PROJECT_ROOT}/src/agent/TimeTrack.MacOSDesktopHost/Swift"
if [ -d "${SWIFT_DIR}" ]; then
    cp -r "${SWIFT_DIR}/"* "${APP_BUNDLE}/Contents/Resources/"
    echo "  Swift sources available for Xcode project"
fi

echo "[4/6] Creating Info.plist..."
cat > "${APP_BUNDLE}/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>TimeTrack</string>
    <key>CFBundleIdentifier</key>
    <string>com.cyriusx.timetrack</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>ChronosX TimeTrack</string>
    <key>CFBundleDisplayName</key>
    <string>TimeTrack</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2026 CyriusX. All rights reserved.</string>
    <key>NSAppleEventsUsageDescription</key>
    <string>TimeTrack needs to monitor active applications for time tracking.</string>
    <key>SUPublicEDKey</key>
    <string></string>
    <key>SUFeedURL</key>
    <string>https://updates.cyrius.com/timetrack/appcast.xml</string>
</dict>
</plist>
PLIST

echo "[5/6] Creating LaunchAgent plist..."
mkdir -p "${BUILD_DIR}/launchagent"
cat > "${BUILD_DIR}/launchagent/com.cyriusx.timetrack.agent.plist" << 'LAPLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.cyriusx.timetrack.agent</string>
    <key>ProgramArguments</key>
    <array>
        <string>/Applications/TimeTrack.app/Contents/MacOS/agent/TimeTrack.MacOSAgentService</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>ProcessType</key>
    <string>Background</string>
    <key>ThrottleInterval</key>
    <integer>5</integer>
    <key>StandardOutPath</key>
    <string>/var/log/timetrack-agent.log</string>
    <key>StandardErrorPath</key>
    <string>/var/log/timetrack-agent.log</string>
</dict>
</plist>
LAPLIST

echo "[6/6] Creating app launch script..."
cat > "${APP_BUNDLE}/Contents/MacOS/TimeTrack" << 'LAUNCH'
#!/bin/bash
DIR="$(cd "$(dirname "$0")" && pwd)"

# Start agent service in background
if [ -f "${DIR}/agent/TimeTrack.MacOSAgentService" ]; then
    "${DIR}/agent/TimeTrack.MacOSAgentService" &
    AGENT_PID=$!
fi

# Forward signals
cleanup() {
    kill ${AGENT_PID:-} 2>/dev/null
    exit 0
}
trap cleanup SIGINT SIGTERM

# Wait
wait
LAUNCH
chmod +x "${APP_BUNDLE}/Contents/MacOS/TimeTrack"

echo ""
echo "=== Build Complete ==="
echo "App Bundle: ${APP_BUNDLE}"
echo ""
echo "Next steps:"
echo "  1. Create Xcode project with Swift sources from src/agent/TimeTrack.MacOSDesktopHost/Swift/"
echo "  2. Add Sparkle framework via SPM: https://github.com/sparkle-project/Sparkle"
echo "  3. Build Swift app and replace Contents/MacOS/TimeTrack with the native binary"
echo "  4. Code sign: codesign --deep --force --sign 'Developer ID Application: ...' '${APP_BUNDLE}'"
echo "  5. Notarize: xcrun notarytool submit '${APP_BUNDLE}.zip' --apple-id ... --password ... --team-id ..."
echo "  6. Create DMG: hdiutil create -volname TimeTrack -srcfolder '${APP_BUNDLE}' TimeTrack.dmg"
