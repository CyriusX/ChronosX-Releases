#!/bin/bash
# =============================================================================
# TimeTrack Post-Install Setup
#
# This script is bundled inside the DMG. Users double-click it once after
# dragging TimeTrack.app to Applications. It:
#   1. Installs the LaunchAgent so the tracking service starts at every login
#   2. Starts the agent right away (no need to reboot)
#   3. Opens the TimeTrack app
#
# Safe to run multiple times — it's fully idempotent.
# =============================================================================

APP="/Applications/Chronos TimeTrack.app"
AGENT_EXEC="${APP}/Contents/Resources/agent/TimeTrack.MacOSAgentService"
AGENT_DIR="${APP}/Contents/Resources/agent"
PLIST_LABEL="com.cyriusx.timetrack.agent"
PLIST_NAME="${PLIST_LABEL}.plist"
LAUNCH_AGENTS_DIR="${HOME}/Library/LaunchAgents"
PLIST_DEST="${LAUNCH_AGENTS_DIR}/${PLIST_NAME}"

# ── Helpers ──────────────────────────────────────────────────────────────────
ok()   { echo "  ✓ $1"; }
fail() { echo "  ✗ $1" >&2; }
warn() { echo "  ⚠ $1"; }

echo ""
echo "━━━ TimeTrack Setup ━━━"
echo ""

# ── 1. Verify the app is in /Applications ────────────────────────────────────
if [[ ! -d "${APP}" ]]; then
    fail "Chronos TimeTrack.app not found in /Applications."
    echo ""
    echo "  Please drag Chronos TimeTrack.app into your Applications folder first,"
    echo "  then run this script again."
    echo ""
    read -rp "Press Return to close..." _
    exit 1
fi
ok "Chronos TimeTrack.app found"

# ── 2. Make agent executable ─────────────────────────────────────────────────
if [[ -f "${AGENT_EXEC}" ]]; then
    chmod +x "${AGENT_EXEC}"
    ok "Agent permissions set"
else
    warn "Agent executable not found — skipping"
fi

# ── 3. Install LaunchAgent plist ─────────────────────────────────────────────
mkdir -p "${LAUNCH_AGENTS_DIR}"

cat > "${PLIST_DEST}" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>${PLIST_LABEL}</string>
    <key>ProgramArguments</key>
    <array>
        <string>${AGENT_EXEC}</string>
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
    <string>/tmp/timetrack-agent.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/timetrack-agent.log</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
        <key>DOTNET_CONTENT_ROOT</key>
        <string>${AGENT_DIR}</string>
    </dict>
</dict>
</plist>
PLIST
ok "LaunchAgent installed"

# ── 4. Load / reload the LaunchAgent ─────────────────────────────────────────
# Unload first (ignoring errors — it may not be loaded yet)
launchctl unload "${PLIST_DEST}" 2>/dev/null || true
launchctl load -w "${PLIST_DEST}" 2>/dev/null \
    && ok "LaunchAgent loaded — agent will start at every login" \
    || warn "Could not load LaunchAgent automatically; it will activate on next login"

# ── 5. Start agent now if not already running ─────────────────────────────────
if ! pgrep -f "TimeTrack.MacOSAgentService" > /dev/null 2>&1; then
    if [[ -f "${AGENT_EXEC}" ]]; then
        DOTNET_ENVIRONMENT=Production \
        DOTNET_CONTENT_ROOT="${AGENT_DIR}" \
            "${AGENT_EXEC}" &
        sleep 1
        ok "Agent service started"
    fi
else
    ok "Agent service already running"
fi

# ── 6. Remove macOS quarantine from the app ───────────────────────────────────
xattr -dr com.apple.quarantine "${APP}" 2>/dev/null && ok "Quarantine flag cleared" || true

# ── 7. Open the app ──────────────────────────────────────────────────────────
open "${APP}"
ok "Chronos TimeTrack opened"

echo ""
echo "━━━ Setup complete ━━━"
echo ""
echo "  TimeTrack is now installed and running."
echo "  The tracking agent will start automatically every time you log in."
echo ""
echo "  You can manage this in TimeTrack Settings → General → Start at Login"
echo ""
