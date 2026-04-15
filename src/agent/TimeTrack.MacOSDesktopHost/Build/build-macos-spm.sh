#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../../../.." && pwd)"
BUILD_DIR="${PROJECT_ROOT}/build/macos"
SPM_DIR="${SCRIPT_DIR}/.."
APP_NAME="TimeTrack"
APP_BUNDLE="${BUILD_DIR}/${APP_NAME}.app"

echo "=== Building macOS App (VS Code + SPM) ==="
echo "Project root: ${PROJECT_ROOT}"
echo ""

rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"
mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"
mkdir -p "${APP_BUNDLE}/Contents/Frameworks"

echo "[1/6] Building React UI..."
UI_DIR="${PROJECT_ROOT}/src/ui/timetrack-ui"
if [ -d "${UI_DIR}" ]; then
    cd "${UI_DIR}"
    if [ ! -d "node_modules" ]; then
        npm install
    fi
    npm run build
    cp -r dist "${APP_BUNDLE}/Contents/Resources/"
    echo "  React UI built"
else
    echo "  WARNING: UI not found at ${UI_DIR}, creating placeholder"
    mkdir -p "${APP_BUNDLE}/Contents/Resources/dist"
    echo '<html><body><h2>TimeTrack</h2><p>UI not built.</p></body></html>' \
        > "${APP_BUNDLE}/Contents/Resources/dist/index.html"
fi

echo "[2/6] Building Swift binary..."
cd "${SPM_DIR}"
swift build -c release --product TimeTrack
cp ".build/release/TimeTrack" "${APP_BUNDLE}/Contents/MacOS/"
echo "  Swift binary built"

echo "[3/6] Building .NET Agent Service..."
cd "${PROJECT_ROOT}"
if command -v dotnet &>/dev/null; then
    dotnet publish src/agent/TimeTrack.MacOSAgentService \
        -c Release -r osx-arm64 --self-contained true \
        -o "${APP_BUNDLE}/Contents/MacOS/agent" \
        /p:PublishTrimmed=false
    echo "  .NET Agent built"
else
    echo "  WARNING: dotnet not found, skipping .NET Agent"
fi

echo "[4/6] Creating Info.plist..."
cp "${SPM_DIR}/Resources/Info.plist" "${APP_BUNDLE}/Contents/Info.plist"

echo "[4b] Copying AppIcon.icns..."
cp "${SPM_DIR}/Resources/AppIcon.icns" "${APP_BUNDLE}/Contents/Resources/AppIcon.icns"

echo "[5/6] Creating LaunchAgent..."
mkdir -p "${BUILD_DIR}/launchagent"
cp "${PROJECT_ROOT}/src/agent/TimeTrack.MacOSAgentService/Resources/com.cyriusx.timetrack.agent.plist" \
    "${BUILD_DIR}/launchagent/com.cyriusx.timetrack.agent.plist"

echo "[6/6] Installing Sparkle Framework..."
SPARKLE_XCFW="${SPM_DIR}/.build/artifacts/sparkle/Sparkle/Sparkle.xcframework"
if [ -d "${SPARKLE_XCFW}/macos-arm64_x86_64/Sparkle.framework" ]; then
    cp -R "${SPARKLE_XCFW}/macos-arm64_x86_64/Sparkle.framework" "${APP_BUNDLE}/Contents/Frameworks/"
    echo "  Sparkle framework installed"
else
    echo "  Sparkle framework not found at ${SPARKLE_XCFW}"
fi

echo "[7/7] Setting up dynamic linker rpath..."
install_name_tool -add_rpath "@executable_path/../Frameworks" "${APP_BUNDLE}/Contents/MacOS/TimeTrack" 2>/dev/null \
    && echo "  rpath set" \
    || echo "  (rpath already set)"

echo ""
echo "=== Build Complete ==="
echo "App Bundle: ${APP_BUNDLE}"
echo ""
echo "To run: open ${APP_BUNDLE}"
