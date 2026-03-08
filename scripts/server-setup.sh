#!/usr/bin/env bash
# One-time server setup for HoaSite on Fedora.
# Run as root on the target server.
set -euo pipefail

DOMAIN="claymont-estates.com"

# --- .NET runtime ---

if ! command -v dotnet &>/dev/null; then
    dnf install -y aspnetcore-runtime-10.0
fi

# --- System user and directories ---

if ! id hoasite &>/dev/null; then
    useradd -r -s /sbin/nologin hoasite
    echo "Created hoasite user"
fi

mkdir -p /opt/hoasite
mkdir -p /var/lib/hoasite/Documents
mkdir -p /var/lib/hoasite/Data
chown -R hoasite:hoasite /opt/hoasite /var/lib/hoasite

# --- Environment file for secrets ---

if [ ! -f /etc/hoasite/env ]; then
    mkdir -p /etc/hoasite
    cat > /etc/hoasite/env <<'EOF'
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5000
ConnectionStrings__DefaultConnection=DataSource=/var/lib/hoasite/Data/app.db;Cache=Shared
DocumentStorage__Path=/var/lib/hoasite/Documents
Email__SmtpHost=email-smtp.us-east-1.amazonaws.com
Email__SmtpPort=587
Email__SmtpUsername=CHANGE_ME
Email__SmtpPassword=CHANGE_ME
EOF
    chmod 600 /etc/hoasite/env
    chown root:hoasite /etc/hoasite/env
    echo "Created /etc/hoasite/env — edit this to set your SES credentials"
fi

# --- Systemd service ---

cat > /etc/systemd/system/hoasite.service <<EOF
[Unit]
Description=Claymont Estates HOA Site
After=network.target

[Service]
Type=simple
User=hoasite
WorkingDirectory=/opt/hoasite
ExecStart=/usr/bin/dotnet /opt/hoasite/Server.dll
EnvironmentFile=/etc/hoasite/env
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable hoasite
echo "Installed hoasite.service"

# --- Caddy reverse proxy ---

if ! command -v caddy &>/dev/null; then
    dnf install -y caddy
fi

cat > /etc/caddy/Caddyfile <<EOF
${DOMAIN} {
    reverse_proxy localhost:5000
}
EOF

systemctl enable --now caddy
systemctl reload caddy
echo "Caddy configured for ${DOMAIN}"


echo ""
echo "Setup complete. Next steps:"
echo "  1. Edit /etc/hoasite/env with your SES SMTP credentials"
echo "  2. Point DNS for ${DOMAIN} to this server"
echo "  3. Deploy the app with scripts/deploy.sh"
