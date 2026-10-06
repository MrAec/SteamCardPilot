// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.IO;
using System.Text.Json;

namespace SteamCardPilot;

internal sealed class FarmingSelection {
 private readonly string? path;
 private readonly Dictionary<string, uint> selections;
 internal FarmingSelection(string? path = null) {
  this.path = path is null ? null : Path.GetFullPath(path);
  selections = new(StringComparer.Ordinal);
  if (path is null || !File.Exists(path)) return;
  try {
   foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, uint>>(File.ReadAllText(path)) ?? []) selections[pair.Key] = pair.Value;
  } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
 }
 internal uint Get(string bot) => selections.GetValueOrDefault(bot);
 internal void Set(string bot, uint appID) {
  selections[bot] = appID;
  if (path is null) return;
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  string temporary = path + ".tmp";
  File.WriteAllText(temporary, JsonSerializer.Serialize(selections));
  File.Move(temporary, path, true);
 }
}
