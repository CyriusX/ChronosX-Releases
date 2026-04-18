#!/bin/bash
# =============================================================================
# TimeTrack macOS Build & Installer Script
# Builds a fully self-contained .app bundle and .dmg installer.
# No additional downloads or runtimes required for end users.
#
# Usage:
#   ./build-macos.sh [--version <x.y.z>] [--arch <arm64|x64|all>] [--sign <identity>] [--notarize]
#
# Options:
#   --version  App version string (default: 1.0.0)
#   --arch     Target CPU architecture: arm64, x64, or all (default: host arch)
#   --sign     Apple Developer ID for signing, e.g.
#              "Developer ID Application: Company Name (TEAMID)"
#              Omit to use ad-hoc signing (app runs locally, no notarization)
#   --notarize Submit to Apple for notarization (requires --sign, APPLE_ID,
#              APPLE_APP_PASSWORD, APPLE_TEAM_ID env vars)
#
# Environment variables for notarization:
#   APPLE_ID           Your Apple ID email
#   APPLE_APP_PASSWORD App-specific password from appleid.apple.com
#   APPLE_TEAM_ID      Your 10-char Team ID
# =============================================================================
set -euo pipefail

# --------------- Argument parsing -------------------------------------------
VERSION="1.0.0"
ARCH=""
SIGNING_IDENTITY=""
DO_NOTARIZE=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="$2"; shift 2 ;;
        --arch)    ARCH="$2"; shift 2 ;;
        --sign)    SIGNING_IDENTITY="$2"; shift 2 ;;
        --notarize) DO_NOTARIZE=true; shift ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Default arch to host arch (or build both via --arch all)
HOST_ARCH="$(uname -m)"
if [[ -z "${ARCH}" ]]; then
    if [[ "${HOST_ARCH}" == "arm64" ]]; then ARCH="arm64"; else ARCH="x64"; fi
fi
if [[ "${ARCH}" != "arm64" && "${ARCH}" != "x64" && "${ARCH}" != "all" ]]; then
    echo "ERROR: --arch must be 'arm64', 'x64', or 'all' (got '${ARCH}')"
    exit 1
fi

# --------------- Paths -------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SPM_DIR="${PROJECT_ROOT}/src/agent/TimeTrack.MacOSDesktopHost"
AGENT_DIR="${PROJECT_ROOT}/src/agent/TimeTrack.MacOSAgentService"
UI_DIR="${PROJECT_ROOT}/src/ui/timetrack-ui"
RESOURCES_DIR="${SPM_DIR}/Resources"

APP_DISPLAY_NAME="Chronos TimeTrack"
BUNDLE_ID="com.cyriusx.timetrack"
ICNS_SRC="${RESOURCES_DIR}/AppIcon.icns"
ENTITLEMENTS="${RESOURCES_DIR}/TimeTrack.entitlements"

# --------------- Helper ------------------------------------------------------
step() { echo ""; echo "━━━ $1 ━━━"; }
ok()   { echo "  ✓ $1"; }
warn() { echo "  ⚠ $1"; }

# Shared UI build cache (used when --arch all)
UI_DIST_CACHE=""

ensure_rosetta_for_x64() {
    if [[ "${HOST_ARCH}" != "arm64" ]]; then
        return 0
    fi
    if ! command -v arch >/dev/null 2>&1; then
        echo "ERROR: 'arch' command not available; cannot build x64 on Apple Silicon."
        exit 1
    fi
    if ! arch -x86_64 /usr/bin/true >/dev/null 2>&1; then
        echo "ERROR: Rosetta is required to build x64 on Apple Silicon."
        echo "       Install it with: softwareupdate --install-rosetta --agree-to-license"
        exit 1
    fi
}

build_ui_once() {
    if [[ -n "${UI_DIST_CACHE}" && -d "${UI_DIST_CACHE}" ]]; then
        ok "Reusing cached React UI build at ${UI_DIST_CACHE}"
        return 0
    fi

    step "Building React UI (once)"
    if [[ -d "${UI_DIR}" ]]; then
        (
            cd "${UI_DIR}"
            if [[ ! -d "node_modules" ]]; then
                npm install --silent
            fi
            npm run build
        )
        UI_DIST_CACHE="${UI_DIR}/dist"
        ok "React UI built at ${UI_DIST_CACHE}"
    else
        UI_DIST_CACHE=""
        warn "UI directory not found – will create placeholder per-arch"
    fi
}

build_for_arch() {
    local target_arch="$1"

    local DOTNET_RUNTIME="osx-arm64"
    local SWIFT_ARCH="arm64"
    if [[ "${target_arch}" == "x64" ]]; then
        DOTNET_RUNTIME="osx-x64"
        SWIFT_ARCH="x86_64"
    fi

    local BUILD_DIR="${PROJECT_ROOT}/build/macos/${target_arch}"
    local APP_BUNDLE="${BUILD_DIR}/${APP_DISPLAY_NAME}.app"
    local CONTENTS="${APP_BUNDLE}/Contents"
    local VOLUME_NAME="${APP_DISPLAY_NAME}"
    local DMG_NAME="Chronos-TimeTrack-${VERSION}-macos-${target_arch}"
    local DMG_STAGING="${BUILD_DIR}/dmg-staging"
    local DMG_TMP="${BUILD_DIR}/${DMG_NAME}-tmp.dmg"
    local DMG_FINAL="${BUILD_DIR}/${DMG_NAME}.dmg"
    local SPM_SCRATCH="${BUILD_DIR}/spm-build"

if [[ ! -f "${ICNS_SRC}" ]]; then
    echo "ERROR: AppIcon.icns not found at ${ICNS_SRC}"
    exit 1
fi

command -v swift  >/dev/null || { echo "ERROR: swift not found"; exit 1; }
command -v dotnet >/dev/null || { echo "ERROR: dotnet not found"; exit 1; }
command -v npm    >/dev/null || { echo "ERROR: npm not found"; exit 1; }
command -v hdiutil >/dev/null || { echo "ERROR: hdiutil not found"; exit 1; }

    step "[0/8] Pre-flight checks (${target_arch})"
    ok "All required tools present"
    echo "  Version:     ${VERSION}"
    echo "  Arch:        ${target_arch}"
    echo "  Project:     ${PROJECT_ROOT}"
    echo "  Output:      ${BUILD_DIR}"
    if [[ -n "${SIGNING_IDENTITY}" ]]; then
        echo "  Signing:     ${SIGNING_IDENTITY}"
    else
        echo "  Signing:     ad-hoc (local use only)"
    fi

# --------------- Clean & scaffold --------------------------------------------
    step "[1/8] Cleaning previous build (${target_arch})"
    rm -rf "${BUILD_DIR}"
    mkdir -p "${CONTENTS}/MacOS"
    mkdir -p "${CONTENTS}/Resources"
    mkdir -p "${CONTENTS}/Frameworks"
    ok "Build directories created"

# --------------- Build React UI ---------------------------------------------
    step "[2/8] Installing React UI (${target_arch})"
    if [[ -n "${UI_DIST_CACHE}" && -d "${UI_DIST_CACHE}" ]]; then
        cp -R "${UI_DIST_CACHE}" "${CONTENTS}/Resources/dist"
        ok "React UI copied to bundle (cached build)"
    elif [[ -d "${UI_DIR}" ]]; then
        (
            cd "${UI_DIR}"
            if [[ ! -d "node_modules" ]]; then
                npm install --silent
            fi
            npm run build
        )
        cp -R "${UI_DIR}/dist" "${CONTENTS}/Resources/dist"
        ok "React UI built and copied to bundle"
    else
        warn "UI directory not found – creating placeholder"
        mkdir -p "${CONTENTS}/Resources/dist"
        echo '<html><body><h2>TimeTrack</h2><p>UI not built.</p></body></html>' \
            > "${CONTENTS}/Resources/dist/index.html"
    fi

# --------------- Build Swift binary -----------------------------------------
    step "[3/8] Building Swift binary (${target_arch})"
    cd "${SPM_DIR}"
    if [[ "${SWIFT_ARCH}" == "x86_64" && "${HOST_ARCH}" == "arm64" ]]; then
        ensure_rosetta_for_x64
        arch -x86_64 swift build -c release --product TimeTrack --scratch-path "${SPM_SCRATCH}"
    else
        swift build -c release --product TimeTrack --scratch-path "${SPM_SCRATCH}"
    fi
    cp "${SPM_SCRATCH}/release/TimeTrack" "${CONTENTS}/MacOS/"
    if [[ "${target_arch}" == "arm64" ]]; then
        file "${CONTENTS}/MacOS/TimeTrack" | grep -q "arm64" || {
            echo "ERROR: Swift build produced an unexpected architecture for ${target_arch}:"
            file "${CONTENTS}/MacOS/TimeTrack" || true
            echo "       Building an arm64 macOS app typically requires an Apple Silicon host."
            exit 1
        }
    else
        file "${CONTENTS}/MacOS/TimeTrack" | grep -q "x86_64" || {
            echo "ERROR: Swift build produced an unexpected architecture for ${target_arch}:"
            file "${CONTENTS}/MacOS/TimeTrack" || true
            echo "       Building x64 on Apple Silicon requires Rosetta."
            exit 1
        }
    fi
    ok "Swift binary built and copied"

# --------------- Build .NET Agent (self-contained) --------------------------
    step "[4/8] Building .NET Agent Service (self-contained, ${target_arch})"
    cd "${PROJECT_ROOT}"
# Publish as single-file so all .NET DLLs are bundled into one Mach-O
# binary. This keeps Contents/MacOS/ clean and avoids codesign errors
# caused by non-signable .NET assemblies in that directory.
    local AGENT_PUBLISH_TMP="${BUILD_DIR}/agent-publish-tmp"
    rm -rf "${AGENT_PUBLISH_TMP}"
    dotnet publish "${AGENT_DIR}" \
        -c Release \
        -r "${DOTNET_RUNTIME}" \
        --self-contained true \
        -p:PublishTrimmed=false \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:ErrorOnDuplicatePublishOutputFiles=false \
        -o "${AGENT_PUBLISH_TMP}"

# Place the agent in Contents/Resources/agent — NOT Contents/MacOS —
# so codesign never sees non-Mach-O files (.json, etc.) as code objects.
# The LaunchAgent plist will point to this location.
    mkdir -p "${CONTENTS}/Resources/agent"
    cp "${AGENT_PUBLISH_TMP}/TimeTrack.MacOSAgentService" "${CONTENTS}/Resources/agent/"
    chmod +x "${CONTENTS}/Resources/agent/TimeTrack.MacOSAgentService"
    if [[ "${target_arch}" == "arm64" ]]; then
        file "${CONTENTS}/Resources/agent/TimeTrack.MacOSAgentService" | grep -q "arm64" || {
            echo "ERROR: .NET publish produced an unexpected architecture for ${target_arch}:"
            file "${CONTENTS}/Resources/agent/TimeTrack.MacOSAgentService" || true
            exit 1
        }
    else
        file "${CONTENTS}/Resources/agent/TimeTrack.MacOSAgentService" | grep -q "x86_64" || {
            echo "ERROR: .NET publish produced an unexpected architecture for ${target_arch}:"
            file "${CONTENTS}/Resources/agent/TimeTrack.MacOSAgentService" || true
            exit 1
        }
    fi
    # Runtime config is required for some hosting scenarios; include it when present.
    if [[ -f "${AGENT_PUBLISH_TMP}/TimeTrack.AgentService.runtimeconfig.json" ]]; then
        cp "${AGENT_PUBLISH_TMP}/TimeTrack.AgentService.runtimeconfig.json" "${CONTENTS}/Resources/agent/"
    fi
# Config file alongside the executable
    if [[ -f "${AGENT_DIR}/appsettings.json" ]]; then
        cp "${AGENT_DIR}/appsettings.json" "${CONTENTS}/Resources/agent/"
    fi
    if [[ -f "${AGENT_DIR}/appsettings.Production.json" ]]; then
        cp "${AGENT_DIR}/appsettings.Production.json" "${CONTENTS}/Resources/agent/"
    fi
    rm -rf "${AGENT_PUBLISH_TMP}"
    ok ".NET agent published as single-file (self-contained, ${DOTNET_RUNTIME})"

# --------------- Copy resources & frameworks --------------------------------
    step "[5/8] Assembling app bundle (${target_arch})"

# Info.plist (already has CFBundleIconFile = AppIcon)
    cp "${RESOURCES_DIR}/Info.plist" "${CONTENTS}/Info.plist"
# Inject version into plist
    /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString ${VERSION}" "${CONTENTS}/Info.plist"
    /usr/libexec/PlistBuddy -c "Set :CFBundleVersion ${VERSION}" "${CONTENTS}/Info.plist"
    ok "Info.plist configured (version ${VERSION})"

# App icon (dock + Finder)
    cp "${ICNS_SRC}" "${CONTENTS}/Resources/AppIcon.icns"
    ok "AppIcon.icns installed"

# Sparkle auto-update framework
    local SPARKLE_XCF="${SPM_SCRATCH}/artifacts/sparkle/Sparkle/Sparkle.xcframework"
    local SPARKLE_FW="${SPARKLE_XCF}/macos-arm64_x86_64/Sparkle.framework"
    if [[ -d "${SPARKLE_FW}" ]]; then
        cp -R "${SPARKLE_FW}" "${CONTENTS}/Frameworks/"
        ok "Sparkle.framework bundled"
    else
        warn "Sparkle.framework not found at ${SPARKLE_FW} – skipping"
    fi

# Fix rpath so Swift binary finds Sparkle at runtime
    install_name_tool \
        -add_rpath "@executable_path/../Frameworks" \
        "${CONTENTS}/MacOS/TimeTrack" 2>/dev/null \
        || ok "rpath already set"

# LaunchAgent plist (installer installs this to ~/Library/LaunchAgents)
    mkdir -p "${BUILD_DIR}/launchagent"
    local AGENT_PLIST_SRC="${AGENT_DIR}/Resources/com.cyriusx.timetrack.agent.plist"
    local AGENT_PLIST_OUT="${BUILD_DIR}/launchagent/com.cyriusx.timetrack.agent.plist"
    if [[ -f "${AGENT_PLIST_SRC}" ]]; then
        cp "${AGENT_PLIST_SRC}" "${AGENT_PLIST_OUT}"
        # Update path: agent moved from Contents/MacOS/ to Contents/Resources/agent/
        /usr/libexec/PlistBuddy -c \
            "Set :ProgramArguments:0 '/Applications/${APP_DISPLAY_NAME}.app/Contents/Resources/agent/TimeTrack.MacOSAgentService'" \
            "${AGENT_PLIST_OUT}"
        # Also add DOTNET_CONTENT_ROOT so the agent finds appsettings.json
        /usr/libexec/PlistBuddy -c \
            "Set :EnvironmentVariables:DOTNET_CONTENT_ROOT '/Applications/${APP_DISPLAY_NAME}.app/Contents/Resources/agent'" \
            "${AGENT_PLIST_OUT}" 2>/dev/null \
        || /usr/libexec/PlistBuddy -c \
            "Add :EnvironmentVariables:DOTNET_CONTENT_ROOT string '/Applications/${APP_DISPLAY_NAME}.app/Contents/Resources/agent'" \
            "${AGENT_PLIST_OUT}"
        ok "LaunchAgent plist staged (agent path → Contents/Resources/agent/)"
    fi

    ok "App bundle assembled"

# --------------- Code signing -----------------------------------------------
    step "[6/8] Code signing (${target_arch})"

sign_item() {
    local target="$1"
    if [[ -n "${SIGNING_IDENTITY}" ]]; then
        codesign --force --options runtime \
            --sign "${SIGNING_IDENTITY}" \
            --entitlements "${ENTITLEMENTS}" \
            "${target}" 2>/dev/null
    else
        codesign --force --sign - "${target}" 2>/dev/null
    fi
}

# Sign the .NET agent (now in Resources/agent, single Mach-O executable)
if [[ -f "${CONTENTS}/Resources/agent/TimeTrack.MacOSAgentService" ]]; then
    sign_item "${CONTENTS}/Resources/agent/TimeTrack.MacOSAgentService"
fi

# Sign Sparkle framework — inside-out
if [[ -d "${CONTENTS}/Frameworks/Sparkle.framework" ]]; then
    # Sign dylibs and helper executables first
    while IFS= read -r -d '' item; do
        if file "${item}" | grep -qE 'Mach-O|dynamically linked'; then
            sign_item "${item}"
        fi
    done < <(find "${CONTENTS}/Frameworks/Sparkle.framework" \
        \( -name "*.dylib" -o -name "Autoupdate" -o -name "fileop" \) -print0)

    # Sign Updater.app bundle
    if [[ -d "${CONTENTS}/Frameworks/Sparkle.framework/Versions/B/Updater.app" ]]; then
        sign_item "${CONTENTS}/Frameworks/Sparkle.framework/Versions/B/Updater.app"
    fi

    # Sign the framework itself
    sign_item "${CONTENTS}/Frameworks/Sparkle.framework"
fi

# Sign main Swift executable
sign_item "${CONTENTS}/MacOS/TimeTrack"

# Sign the top-level app bundle (without --deep since we already signed components)
if [[ -n "${SIGNING_IDENTITY}" ]]; then
    codesign --force --options runtime \
        --sign "${SIGNING_IDENTITY}" \
        --entitlements "${ENTITLEMENTS}" \
        "${APP_BUNDLE}"
    ok "Bundle signed with Developer ID: ${SIGNING_IDENTITY}"
    codesign --verify --deep --strict "${APP_BUNDLE}" && ok "Signature verified"
else
    codesign --force --sign - "${APP_BUNDLE}"
    ok "Bundle ad-hoc signed (for local/testing use)"
fi

# --------------- Notarization -----------------------------------------------
if [[ "${DO_NOTARIZE}" == true ]]; then
    step "[6b/8] Notarizing with Apple (${target_arch})"
    : "${APPLE_ID:?APPLE_ID env var required for notarization}"
    : "${APPLE_APP_PASSWORD:?APPLE_APP_PASSWORD env var required}"
    : "${APPLE_TEAM_ID:?APPLE_TEAM_ID env var required}"

    ZIP_PATH="${BUILD_DIR}/${DMG_NAME}-notarize.zip"
    ditto -c -k --keepParent "${APP_BUNDLE}" "${ZIP_PATH}"
    xcrun notarytool submit "${ZIP_PATH}" \
        --apple-id "${APPLE_ID}" \
        --password "${APPLE_APP_PASSWORD}" \
        --team-id "${APPLE_TEAM_ID}" \
        --wait
    xcrun stapler staple "${APP_BUNDLE}"
    rm -f "${ZIP_PATH}"
    ok "Notarization complete and ticket stapled"
fi

# --------------- Create DMG installer ----------------------------------------
    step "[7/8] Creating DMG installer (${target_arch})"

# Build staging folder
rm -rf "${DMG_STAGING}"
mkdir -p "${DMG_STAGING}"
cp -R "${APP_BUNDLE}" "${DMG_STAGING}/"
# Symlink to /Applications for drag-install
ln -sf /Applications "${DMG_STAGING}/Applications"

# Calculate required size (add 30 MB headroom for styling)
APP_SIZE_KB=$(du -sk "${DMG_STAGING}" | awk '{print $1}')
DMG_SIZE_MB=$(( (APP_SIZE_KB / 1024) + 50 ))

# Create writable temp DMG
hdiutil create \
    -srcfolder "${DMG_STAGING}" \
    -volname "${VOLUME_NAME}" \
    -fs HFS+ \
    -fsargs "-c c=64,a=16,b=16" \
    -format UDRW \
    -size "${DMG_SIZE_MB}m" \
    "${DMG_TMP}"

# Mount it (suppress auto-open)
DMG_MOUNT="$(hdiutil attach "${DMG_TMP}" -readwrite -noverify -noautoopen \
    | awk -F'\t' '/\/Volumes\//{print $NF; exit}')"
DMG_MOUNT="${DMG_MOUNT%"${DMG_MOUNT##*[! ]}"}"  # trim trailing whitespace
ok "Mounted at: ${DMG_MOUNT}"

# Set custom volume icon
cp "${ICNS_SRC}" "${DMG_MOUNT}/.VolumeIcon.icns"
# Mark directory as having custom icon
if command -v SetFile >/dev/null 2>&1; then
    SetFile -a C "${DMG_MOUNT}"
fi
# Use osascript as a reliable cross-environment fallback
osascript << APPLESCRIPT 2>/dev/null || true
tell application "Finder"
    set dsk to disk "${VOLUME_NAME}"
    set img to POSIX file "${DMG_MOUNT}/.VolumeIcon.icns" as alias
    try
        set icon of dsk to img
    end try
end tell
APPLESCRIPT

# Style the Finder window via AppleScript
osascript << APPLESCRIPT
tell application "Finder"
    tell disk "${VOLUME_NAME}"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set the bounds of container window to {150, 80, 700, 420}
        set theViewOptions to icon view options of container window
        set arrangement of theViewOptions to not arranged
        set icon size of theViewOptions to 100
        set text size of theViewOptions to 12
        set position of item "${APP_DISPLAY_NAME}.app"         of container window to {170, 175}
        set position of item "Applications"           of container window to {440, 175}
        close
        open
        update without registering applications
        delay 2
        close
    end tell
end tell
APPLESCRIPT
ok "DMG window styled"

# Finalise
sync
hdiutil detach "${DMG_MOUNT}" -quiet

# Convert to compressed read-only UDZO
rm -f "${DMG_FINAL}"
hdiutil convert "${DMG_TMP}" \
    -format UDZO \
    -imagekey zlib-level=9 \
    -o "${DMG_FINAL}"
rm -f "${DMG_TMP}"
rm -rf "${DMG_STAGING}"
ok "DMG created: ${DMG_FINAL}"

# --------------- Verify & summary -------------------------------------------
    step "[8/8] Summary (${target_arch})"
    local APP_SIZE
    local DMG_SIZE
    APP_SIZE="$(du -sh "${APP_BUNDLE}" | cut -f1)"
    DMG_SIZE="$(du -sh "${DMG_FINAL}" | cut -f1)"

echo ""
echo "  App bundle : ${APP_BUNDLE}  (${APP_SIZE})"
echo "  Installer  : ${DMG_FINAL}  (${DMG_SIZE})"
echo ""
echo "  To install: open the DMG and drag ${APP_DISPLAY_NAME}.app to Applications."
echo "  Start at login can be enabled in Settings → General."
echo ""
if [[ -z "${SIGNING_IDENTITY}" ]]; then
    echo "  NOTE: This build is ad-hoc signed. End users must right-click ▸ Open"
    echo "  the first time to bypass Gatekeeper. Use --sign for distribution."
fi
echo ""
echo "Done."
}

# --------------- Dispatcher --------------------------------------------------
if [[ "${ARCH}" == "all" ]]; then
    build_ui_once
    build_for_arch "arm64"
    build_for_arch "x64"
else
    build_for_arch "${ARCH}"
fi
