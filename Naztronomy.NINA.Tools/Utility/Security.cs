#region "copyright"

/*
    Copyright (c) 2026 Naztronomy

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Security.Cryptography;
using System.Text;

namespace Naztronomy.NINA.Tools.Utility {

    /// <summary>
    /// Encrypts and decrypts stored secrets (such as webhook URLs) using the Windows Data
    /// Protection API scoped to the current user.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "NINA is currently Windows-only")]
    public static class Security {

        public static string Encrypt(string secret) {
            if (string.IsNullOrEmpty(secret)) {
                return string.Empty;
            }

            byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
            byte[] cipherBytes = ProtectedData.Protect(secretBytes, null, DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(cipherBytes);
        }

        public static string Decrypt(string cipher) {
            if (string.IsNullOrEmpty(cipher)) {
                return string.Empty;
            }

            try {
                byte[] cipherBytes = Convert.FromBase64String(cipher);
                byte[] secretBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(secretBytes);
            } catch {
                return string.Empty;
            }
        }
    }
}
