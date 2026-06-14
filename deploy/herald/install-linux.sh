#!/usr/bin/env bash
# Publishes and installs the Argus Herald agent as a systemd service.
# Usage: sudo ./install-linux.sh [path-to-repo-root]
set -euo pipefail

REPO_ROOT="${1:-$(cd "$(dirname "$0")/../.." && pwd)}"
INSTALL_DIR="/opt/argus-herald"

echo "Publishing herald (linux-x64)..."
dotnet publish "$REPO_ROOT/src/Argus.Herald/Argus.Herald.csproj" \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o "$INSTALL_DIR"

echo "Installing systemd unit..."
cp "$REPO_ROOT/deploy/herald/argus-herald.service" /etc/systemd/system/argus-herald.service
systemctl daemon-reload
systemctl enable --now argus-herald

echo "Done. Configure $INSTALL_DIR/appsettings.json (StyxGrpcEndpoint, LogPaths),"
echo "then: systemctl restart argus-herald && journalctl -u argus-herald -f"
