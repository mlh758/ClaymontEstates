#!/usr/bin/env bash
# Deploy HoaSite to production.
# Run from the repository root on your dev machine.
set -euo pipefail

SERVER="hoa_server"
REMOTE_PATH="/opt/hoasite"

echo "Publishing..."
dotnet publish src/Server -c Release -o .publish/server
dotnet publish src/CLI -c Release -o .publish/cli

echo "Syncing to server..."
rsync -az --delete .publish/server/ "${SERVER}:${REMOTE_PATH}/"
rsync -az --delete .publish/cli/ "${SERVER}:/opt/hoasite-cli/"

echo "Restarting service..."
ssh "${SERVER}" "chown -R hoasite:hoasite ${REMOTE_PATH} /opt/hoasite-cli && systemctl restart hoasite"

rm -rf .publish

echo "Deployed successfully"
