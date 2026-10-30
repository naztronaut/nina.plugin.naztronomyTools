#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.ComponentModel.Composition;
using System.Windows;

namespace Naztronomy.NINA.Tools.Discord {

    [Export(typeof(ResourceDictionary))]
    public partial class DiscordCustomImageMessageTemplate : ResourceDictionary {

        public DiscordCustomImageMessageTemplate() {
            InitializeComponent();
        }
    }
}
