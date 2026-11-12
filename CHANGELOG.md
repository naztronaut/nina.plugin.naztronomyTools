# Changelog

All notable changes to Naztronomy Tools are documented here.

## Unreleased

- **Config** options tab (now the last tab): the Discord webhook URL lives here and is shared by every notification feature (Discord, Target Scheduler, Log Monitor), so the individual tabs no longer ask for it.
- **Logs** options tab: an on/off **Log monitor** that watches NINA's latest log file in the background (no sequence required) and stays on across restarts. Controls what it alerts on — notify on errors (ERROR/FATAL), optionally on warnings, plus a list of phrases (e.g. "filter wheel", "timed out") that raise an alert on any matching log line. Its Discord alerts have their own bot name and title, separate from the custom image message.

## 1.0.0.0

- Initial release.
- **Discord - Custom Image Message** sequence trigger:
  - Posts a message and embedded image to a Discord webhook on a configurable interval (seconds / minutes / hours).
  - Live countdown to the next post.
  - Automatic Dropbox share-link to direct-image-link conversion.
  - Optional embed styling: bot name, title, footer, color, and UTC timestamp.
  - Webhook and image URLs encrypted at rest (Windows DPAPI).
- **Discord - Send Image Message** sequence instruction: posts the configured message and image to Discord once.
- **Discord - Scheduler Preview** sequence instruction: fetches the Target Scheduler plan preview via its HTTP API and posts it to Discord.
