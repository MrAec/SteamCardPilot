// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Text.RegularExpressions;
using System.IO;

namespace SteamCardPilot;

internal static partial class AccountNameRules {
 [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$")]
 private static partial Regex AllowedName();
 [GeneratedRegex("^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
 private static partial Regex ReservedDeviceName();
 internal static bool IsValid(string name) => AllowedName().IsMatch(name) && IsSafeFileName(name);
 // Existing engine accounts may contain spaces, dots, or non-ASCII letters.
 internal static bool IsSafeFileName(string name) => !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
  && !name.EndsWith('.') && !name.EndsWith(' ') && !name.Equals("ASF", StringComparison.OrdinalIgnoreCase)
  && !ReservedDeviceName().IsMatch(name.Split('.')[0]);
 internal const string Help = "Use 1–64 letters, digits, hyphens, or underscores. ASF and Windows device names (such as CON or COM1) are reserved.";
}
