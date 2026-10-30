# Naztronomy Tools

A plugin for [N.I.N.A. (Nighttime Imaging 'N' Astronomy)](https://nighttime-imaging.eu/) that bundles a growing collection of utilities. The plugin options page is organized into tabs, one per tool.

## Features

### Discord — Custom Image Message

A **sequence trigger** (found under the **Naztronomy Tools** category in the Advanced Sequencer) that posts a message to a Discord channel through an incoming webhook and embeds an image referenced by URL, on a fixed time interval.

- Configurable interval with **seconds / minutes / hours** units.
- Live **countdown** to the next post shown on the trigger.
- Posts a **message** and an **embedded image** from any direct image URL.
- **Dropbox share links are converted automatically** to direct image links (`?dl=0` → `?raw=1`), so you can paste a normal Dropbox link.
- Optional embed styling: **bot name**, **title**, **footer**, **color**, and a UTC **timestamp**.
- The webhook URL and image URL are **encrypted at rest** (Windows DPAPI, current user).

All of the Discord values (webhook URL, image URL, message, and embed styling) are configured once on the plugin's **Discord** options tab and used by the trigger at send time.

### Discord — Send Image Message

A **sequence instruction** (same **Naztronomy Tools** category) that posts the configured message and embedded image to Discord **once** when the sequence reaches it. Uses the same Discord settings as the trigger.

### Discord — Scheduler Preview

A **sequence instruction** that fetches the [Target Scheduler](https://github.com/tcpalmer/nina.plugin.targetscheduler) plan preview (the ordered list of targets, start/end times, waits and per-filter exposure counts) and posts it to Discord. Ideal as a one-time instruction at the start of the night.

This reads Target Scheduler's **HTTP API**, so you must enable it first:

1. In Target Scheduler, open its profile preferences and **enable the API** (it is off by default) and note the **port** (default `8188`).
2. In **Options → Plugins → Naztronomy Tools → Target Scheduler**, set the matching **API host** (usually `localhost`) and **port**.
3. Add the **Naztronomy Tools → Discord - Scheduler Preview** instruction to your sequence.

## Installation

### From a release

1. Download the plugin `.zip` from the [Releases](https://github.com/Naztronaut/NINA-Plugins/releases) page.
2. Extract it to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Naztronomy.NINA.Tools\`.
3. Restart N.I.N.A.

### Configure the Discord tool

1. Create an **incoming webhook** in your Discord server: _Server Settings → Integrations → Webhooks → New Webhook_, then **Copy Webhook URL**.
2. In N.I.N.A., open **Options → Plugins → Naztronomy Tools → Discord** and paste the webhook URL.
3. Set the **Image URL** (e.g. a Dropbox share link) and a **Message**. Optionally set embed title/footer/color and a bot name.
4. Click **Send test message** to verify.
5. In the **Advanced Sequence**, add the **Naztronomy Tools → Discord - Custom Image Message** trigger and set the interval.

## Building from source

Requirements: .NET 8 SDK.

```powershell
dotnet build "Naztronomy.NINA.Tools.sln" -c Debug
```

A successful build copies the plugin into `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Naztronomy.NINA.Tools\` automatically so it is loaded the next time N.I.N.A. starts.

### Packaging a release

```powershell
./Package-Plugin.ps1 -InstallerUrl "https://github.com/Naztronaut/NINA-Plugins/releases/download/v1.0.0.0/Naztronomy.NINA.Tools-1.0.0.0.zip"
```

This builds in `Release`, produces a `.zip` and a NINA `manifest.json` (with SHA256 checksum) under `artifacts/release/`.

## License

[MPL-2.0](https://www.mozilla.org/en-US/MPL/2.0/)
