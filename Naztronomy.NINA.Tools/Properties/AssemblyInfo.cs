#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The unique identifier of the plugin. Keep this value stable for the lifetime of the plugin.
[assembly: Guid("a3f1e4c2-7b9d-4e6a-8c21-5f0b2d9e4a77")]

// [MANDATORY] The assembly versioning. Increment for each new release build of the plugin.
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// [MANDATORY] The name of the plugin as shown in NINA.
[assembly: AssemblyTitle("Naztronomy Tools")]
// [MANDATORY] A short description of the plugin.
[assembly: AssemblyDescription("A collection of tools for NINA for Naztronomy.")]

// Your name / author.
[assembly: AssemblyCompany("Naztronomy")]
// The product name that this plugin is part of.
[assembly: AssemblyProduct("Naztronomy Tools")]
[assembly: AssemblyCopyright("Copyright © 2026 Naztronomy")]

// [MANDATORY] The minimum version of N.I.N.A. that this plugin is compatible with.
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

// The license this plugin's code is using.
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where the plugin is hosted.
[assembly: AssemblyMetadata("Repository", "https://github.com/Naztronaut/NINA-Plugins")]

// [Optional] Plugin homepage URL.
[assembly: AssemblyMetadata("Homepage", "https://www.naztronomy.com/")]
// [Optional] Common tags that describe the plugin.
[assembly: AssemblyMetadata("Tags", "Discord,Notifications,Webhook,Tools")]
// [Optional] A link to a changelog.
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/Naztronaut/NINA-Plugins/blob/main/CHANGELOG.md")]
// [Optional] A url to a featured logo shown in the plugin list.
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
// [Optional] A url to an example screenshot of the plugin in action.
[assembly: AssemblyMetadata("ScreenshotURL", "")]
// [Optional] An additional example screenshot.
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
// [Optional] An in-depth description of the plugin.
[assembly: AssemblyMetadata("LongDescription", @"Naztronomy Tools is a collection of utilities for N.I.N.A.

**Discord - Custom Image Message** sequence trigger: on a configurable interval (seconds, minutes or hours), posts a message to a Discord channel via a webhook and embeds an image from a URL (for example a Dropbox share link). Dropbox share links are automatically converted to direct image links, and the trigger shows a live countdown to the next post.

**Log Monitor**: a background monitor (toggled on the plugin's Logs tab) that watches NINA's latest log file and posts a Discord alert when it sees error, warning, or custom-phrase matches you configure. It runs independently of sequences and stays on across NINA restarts.

Configure the shared Discord webhook URL on the plugin's Config options tab; it is encrypted at rest. Image and message styling live on the Discord tab.")]

// Setting ComVisible to false makes the types in this assembly not visible to COM components.
[assembly: ComVisible(false)]
