#!/usr/bin/env bash
# Publishes and installs the Argus Herald agent as a launchd daemon on macOS.
# Usage: sudo ./install-macos.sh [path-to-repo-root]
set -euo pipefail

REPO_ROOT="${1:-$(cd "$(dirname "$0")/../.." && pwd)}"
INSTALL_DIR="/usr/local/argus-herald"
PLIST="/Library/LaunchDaemons/com.argus.herald.plist"

# Pick the runtime identifier for this Mac.
case "$(uname -m)" in
  arm64) RID="osx-arm64" ;;
  x86_64) RID="osx-x64" ;;
  *) echo "Unsupported arch $(uname -m)" >&2; exit 1 ;;
esac

echo "Publishing herald ($RID)..."
dotnet publish "$REPO_ROOT/src/Argus.Herald/Argus.Herald.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -o "$INSTALL_DIR"

echo "Installing launchd daemon..."
cp "$REPO_ROOT/deploy/herald/com.argus.herald.plist" "$PLIST"
chown root:wheel "$PLIST"
launchctl bootout system "$PLIST" 2>/dev/null || true
launchctl bootstrap system "$PLIST"

echo "Done. Configure $INSTALL_DIR/appsettings.json (StyxGrpcEndpoint, ApiKey, LogPaths),"
echo "then: sudo launchctl kickstart -k system/com.argus.herald"
echo "Logs: tail -f /var/log/argus-herald.log"
