# Changelog

All notable changes to Naztronomy Tools are documented here.

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
