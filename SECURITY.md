# Security policy

## Supported versions

Security fixes are applied to the latest published release.

## Reporting a vulnerability

Do not include Discord webhook tokens, screenshots containing private information, or other secrets in a public issue. Use GitHub's private vulnerability reporting feature for this repository when available.

## Local security model

- The application runs as the current user and does not request administrator rights.
- Discord webhooks accept only HTTPS URLs on official `discord.com` or `discordapp.com` hosts, including Canary and PTB subdomains.
- Webhook secrets are protected with Windows DPAPI for the current user.
- Detector packs are path-sanitized and every declared file is checked against its SHA-256 digest before installation.
- The current detector pack is retained for rollback when an update is installed.
- No anti-cheat bypass, process injection, memory reading, credential capture, or Roblox client modification is used.
