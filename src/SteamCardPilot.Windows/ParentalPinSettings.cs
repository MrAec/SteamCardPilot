// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Text.Json.Nodes;

namespace SteamCardPilot;

internal static class ParentalPinSettings {
 internal static bool IsValid(string pin) => pin.Length == 4 && pin.All(c => c is >= '0' and <= '9');
 internal static JsonObject WithPin(JsonObject original, string pin) {
  if (!IsValid(pin)) throw new ArgumentException("The Family View PIN must contain exactly four digits.");
  var updated = (JsonObject)original.DeepClone();
  updated["SteamParentalCode"] = pin;
  return updated;
 }
}
