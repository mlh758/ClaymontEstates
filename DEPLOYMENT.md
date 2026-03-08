# Deployment Guide

Server: Hetzner (Fedora 43)
Address: `2a01:4f9:c014:929::1`
SSH alias: `hoa_server` (configured in `~/.ssh/config`)

```
Host hoa_server
    HostName 2a01:4f9:c014:929::1
    User root
```


## One-Time Server Setup

Copy and run the setup script on the server:

```bash
scp scripts/server-setup.sh hoa_server:~/
ssh hoa_server 'bash ~/server-setup.sh'
```

This will:
- Create a `hoasite` service user
- Create app and data directories
- Install a systemd service with an environment file for secrets
- Install and configure Caddy as a reverse proxy with automatic TLS
- Open HTTP/HTTPS in the firewall

After running, edit `/etc/hoasite/env` on the server to set your SES credentials.

## Deploying

From the repository root:

```bash
./scripts/deploy.sh
```

This publishes the app and CLI, rsyncs both to the server, and restarts the service.

## Email (Amazon SES)

The app uses FluentEmail with SMTP. In development it connects to localhost:2525 (no auth). In production it connects to SES via the environment variables in `/etc/hoasite/env`.

### SES setup checklist

1. Verify `mail.claymont-estates.com` in the SES console (add DKIM CNAME records + SPF)
2. Create SMTP credentials in SES console (SMTP Settings > Create Credentials)
3. Update `Email__SmtpUsername` and `Email__SmtpPassword` in `/etc/hoasite/env` on the server
4. Request production access (exit sandbox) before going live

## Directory Layout

| Path | Contents |
|---|---|
| `/opt/hoasite/` | Application binaries (replaced on each deploy) |
| `/var/lib/hoasite/Data/app.db` | SQLite database |
| `/var/lib/hoasite/Documents/` | Uploaded document files |
| `/etc/hoasite/env` | Environment variables and secrets |

## CLI

The CLI is deployed to `/opt/hoasite-cli/`. To run commands, source the environment file first:

```bash
set -a && source /etc/hoasite/env && set +a
dotnet /opt/hoasite-cli/CLI.dll create-user \
  --name "Mike Init" \
  --email fake@example.com \
  --address "123 Claymont Dr" \
  --password "!replaceMe!1235!@" \
  --role President \
  --db /var/lib/hoasite/Data/app.db
```

## Notes

- Data in `/var/lib/hoasite/` is persistent and not affected by deploys.
- Secrets are stored in `/etc/hoasite/env` (mode 600, owned by root:hoasite).
- SSH to server: `ssh hoa_server`
- The server requires an IPv4 address for outbound SMTP to SES (AWS SES endpoints are IPv4-only).
