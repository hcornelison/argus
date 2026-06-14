#!/usr/bin/env bash
# Deploys Argus styx (Kestrel systemd service) + pantheon (nginx static SPA)
# on Ubuntu 24.04, building everything from source on the server.
#
# Usage:
#   sudo ./install.sh [OPTIONS]
#
# Options:
#   --repo-root DIR        Path to the checked-out repo (default: two dirs above this script)
#   --connection STRING    SQL Server connection string (required on first run; saved to
#                          /etc/argus-styx/appsettings.Production.json for subsequent runs)
#   --api-keys KEY,...     Comma-separated ingest API keys for herald agents
#                          (default: reads from existing config if present, else prompts)
#   --domain DOMAIN        Public hostname (e.g. argus.example.com) used in the nginx
#                          server_name and for CORS. Use "localhost" for LAN-only.
#   --styx-rest-port PORT  Internal Kestrel REST+SignalR port (default: 8080)
#   --styx-grpc-port PORT  Internal Kestrel gRPC port (default: 8081)
#   --skip-build           Skip dotnet publish and ng build (redeploy config only)
#   --skip-nginx           Skip nginx config (useful if you manage it separately)
#
# On first run supply --connection and --api-keys (or be prompted).
# On subsequent runs (updates) you can omit them; the saved config is preserved.
#
# Prerequisites on the server:
#   - .NET 10 SDK  (https://learn.microsoft.com/dotnet/core/install/linux-ubuntu)
#   - Node.js 22+  (https://nodejs.org or via nvm)
#   - nginx        (apt install nginx)
#   - A reachable SQL Server instance

set -euo pipefail

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${REPO_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
CONNECTION_STRING=""
API_KEYS=""
DOMAIN="localhost"
STYX_REST_PORT=8080
STYX_GRPC_PORT=8081
SKIP_BUILD=false
SKIP_NGINX=false

STYX_INSTALL_DIR="/opt/argus-styx"
PANTHEON_INSTALL_DIR="/var/www/argus-pantheon"
STYX_CONFIG_DIR="/etc/argus-styx"
STYX_SERVICE="argus-styx"

# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo-root)     REPO_ROOT="$2";       shift 2 ;;
    --connection)    CONNECTION_STRING="$2"; shift 2 ;;
    --api-keys)      API_KEYS="$2";         shift 2 ;;
    --domain)        DOMAIN="$2";           shift 2 ;;
    --styx-rest-port) STYX_REST_PORT="$2"; shift 2 ;;
    --styx-grpc-port) STYX_GRPC_PORT="$2"; shift 2 ;;
    --skip-build)    SKIP_BUILD=true;       shift ;;
    --skip-nginx)    SKIP_NGINX=true;       shift ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

if [[ $EUID -ne 0 ]]; then
  echo "Run as root (sudo ./install.sh ...)" >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Resolve connection string — existing config → flag → prompt
# ---------------------------------------------------------------------------
EXISTING_CONFIG="$STYX_CONFIG_DIR/appsettings.Production.json"

if [[ -z "$CONNECTION_STRING" && -f "$EXISTING_CONFIG" ]]; then
  CONNECTION_STRING=$(python3 -c "
import json, sys
d = json.load(open('$EXISTING_CONFIG'))
print(d.get('ConnectionStrings', {}).get('Argus', ''))
" 2>/dev/null || true)
fi

if [[ -z "$CONNECTION_STRING" ]]; then
  read -rp "SQL Server connection string: " CONNECTION_STRING
fi

if [[ -z "$CONNECTION_STRING" ]]; then
  echo "A connection string is required." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Resolve API keys
# ---------------------------------------------------------------------------
if [[ -z "$API_KEYS" && -f "$EXISTING_CONFIG" ]]; then
  API_KEYS=$(python3 -c "
import json, sys
d = json.load(open('$EXISTING_CONFIG'))
keys = d.get('Ingest', {}).get('ApiKeys', [])
print(','.join(keys))
" 2>/dev/null || true)
fi

if [[ -z "$API_KEYS" ]]; then
  read -rp "Herald ingest API key(s) (comma-separated): " API_KEYS
fi

if [[ -z "$API_KEYS" ]]; then
  echo "At least one API key is required." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Build styx
# ---------------------------------------------------------------------------
if [[ "$SKIP_BUILD" == false ]]; then
  echo ""
  echo "==> Building styx (linux-x64)..."
  dotnet publish "$REPO_ROOT/src/Argus.Styx/Argus.Styx.csproj" \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -o "$STYX_INSTALL_DIR"
  echo "    Published to $STYX_INSTALL_DIR"
fi

# ---------------------------------------------------------------------------
# Build pantheon
# ---------------------------------------------------------------------------
if [[ "$SKIP_BUILD" == false ]]; then
  echo ""
  echo "==> Building pantheon (Angular 21)..."
  PANTHEON_SRC="$REPO_ROOT/src/pantheon"
  (cd "$PANTHEON_SRC" && npm ci && npx ng build --configuration production)

  echo "    Deploying SPA to $PANTHEON_INSTALL_DIR..."
  rm -rf "$PANTHEON_INSTALL_DIR"
  mkdir -p "$PANTHEON_INSTALL_DIR"
  cp -r "$PANTHEON_SRC/dist/pantheon/browser/." "$PANTHEON_INSTALL_DIR/"
fi

# ---------------------------------------------------------------------------
# Write styx production config
# ---------------------------------------------------------------------------
echo ""
echo "==> Writing styx config ($EXISTING_CONFIG)..."
mkdir -p "$STYX_CONFIG_DIR"

# Convert comma-separated keys to JSON array
KEYS_JSON=$(python3 -c "
import json, sys
keys = [k.strip() for k in '$API_KEYS'.split(',') if k.strip()]
print(json.dumps(keys))
")

# Public URL used for CORS (nginx is the public face)
if [[ "$DOMAIN" == "localhost" ]]; then
  ORIGIN="http://localhost"
else
  ORIGIN="https://$DOMAIN"
fi

cat > "$EXISTING_CONFIG" << EOF
{
  "ConnectionStrings": {
    "Argus": "$CONNECTION_STRING"
  },
  "Database": {
    "MigrateOnStartup": true
  },
  "Kestrel": {
    "Endpoints": {
      "Rest": {
        "Url": "http://127.0.0.1:$STYX_REST_PORT",
        "Protocols": "Http1"
      },
      "Grpc": {
        "Url": "http://0.0.0.0:$STYX_GRPC_PORT",
        "Protocols": "Http2"
      }
    }
  },
  "Ingest": {
    "ApiKeys": $KEYS_JSON
  },
  "Cors": {
    "Origins": [ "$ORIGIN" ]
  },
  "Retention": {
    "Days": 7
  }
}
EOF

# Restrict permissions — connection string is sensitive
chmod 640 "$EXISTING_CONFIG"

# ---------------------------------------------------------------------------
# Systemd unit for styx
# ---------------------------------------------------------------------------
echo ""
echo "==> Installing systemd unit ($STYX_SERVICE)..."

cat > "/etc/systemd/system/${STYX_SERVICE}.service" << EOF
[Unit]
Description=Argus Styx (REST + gRPC ingest)
After=network.target

[Service]
Type=notify
User=www-data
WorkingDirectory=$STYX_INSTALL_DIR
ExecStart=$STYX_INSTALL_DIR/Argus.Styx
Restart=on-failure
RestartSec=5

# Production config overlay
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_CONTENTROOT=$STYX_INSTALL_DIR

# Styx reads /etc/argus-styx/appsettings.Production.json automatically when
# ASPNETCORE_ENVIRONMENT=Production because ASP.NET Core searches these paths:
#   1. appsettings.json (baked into the publish output)
#   2. appsettings.Production.json next to the binary  ← we symlink to /etc
Environment=ASPNETCORE_CONTENTROOT=$STYX_INSTALL_DIR

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=argus-styx

[Install]
WantedBy=multi-user.target
EOF

# ASP.NET Core also checks the directory of the binary for appsettings.Production.json.
# Symlink from the install dir so the config in /etc is used without baking it in.
ln -sf "$EXISTING_CONFIG" "$STYX_INSTALL_DIR/appsettings.Production.json"

# Ensure www-data can read the install dir
chown -R www-data:www-data "$STYX_INSTALL_DIR"
chown -R www-data:www-data "$STYX_CONFIG_DIR"

systemctl daemon-reload
systemctl enable "$STYX_SERVICE"
systemctl restart "$STYX_SERVICE"

echo "    styx started (journalctl -u $STYX_SERVICE -f to tail logs)"

# ---------------------------------------------------------------------------
# nginx config
# ---------------------------------------------------------------------------
if [[ "$SKIP_NGINX" == false ]]; then
  echo ""
  echo "==> Writing nginx config..."

  NGINX_CONF="/etc/nginx/sites-available/argus"

  cat > "$NGINX_CONF" << EOF
# Argus — pantheon SPA + styx reverse proxy
# Generated by deploy/ubuntu/install.sh

server {
    listen 80;
    server_name $DOMAIN;

    root $PANTHEON_INSTALL_DIR;
    index index.html;

    # --- pantheon SPA (Angular) ---
    # All routes not matched below fall through to index.html (HTML5 pushState).
    location / {
        try_files \$uri \$uri/ /index.html;
    }

    # --- styx REST API ---
    location /api/ {
        proxy_pass         http://127.0.0.1:$STYX_REST_PORT;
        proxy_http_version 1.1;
        proxy_set_header   Host              \$host;
        proxy_set_header   X-Real-IP         \$remote_addr;
        proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
    }

    # --- styx SignalR hub (WebSocket) ---
    location /hubs/ {
        proxy_pass         http://127.0.0.1:$STYX_REST_PORT;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade           \$http_upgrade;
        proxy_set_header   Connection        "upgrade";
        proxy_set_header   Host              \$host;
        proxy_set_header   X-Real-IP         \$remote_addr;
        proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}
EOF

  # Enable site
  ln -sf "$NGINX_CONF" /etc/nginx/sites-enabled/argus
  # Disable default site if it's still there
  rm -f /etc/nginx/sites-enabled/default

  nginx -t
  systemctl reload nginx
  echo "    nginx configured for $DOMAIN"
fi

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
echo ""
echo "==> Deployment complete."
echo ""
echo "    Styx:     http://127.0.0.1:$STYX_REST_PORT  (behind nginx)"
echo "              gRPC :$STYX_GRPC_PORT  (herald agents connect here directly)"
echo "    Pantheon: http://$DOMAIN"
echo ""
echo "Next steps:"
echo "  - Set up TLS: certbot --nginx -d $DOMAIN"
echo "    Then update CORS origin in $EXISTING_CONFIG to https://$DOMAIN and restart styx."
echo "  - Restrict gRPC port :$STYX_GRPC_PORT to your agent network via ufw or iptables."
echo "  - Use a least-privilege SQL login (not sa) in the connection string."
echo "  - Replace API keys with real per-agent values."
