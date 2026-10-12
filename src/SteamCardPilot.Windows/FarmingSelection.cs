// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.IO;
using System.Text.Json;

namespace SteamCardPilot;

internal sealed class FarmingSelection {
 private readonly string? path;
 private sealed record Selection(uint AppId, uint[]? Playing = null);
 private readonly Dictionary<string, Selection> selections;
 internal FarmingSelection(string? path = null) {
  this.path = path is null ? null : Path.GetFullPath(path);
  selections = new(StringComparer.Ordinal);
  if (path is null || !File.Exists(path)) return;
  try {
   using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
   if (document.RootElement.ValueKind != JsonValueKind.Object) return;
   foreach (JsonProperty pair in document.RootElement.EnumerateObject()) {
    // Read the original single-game format as well as saved simultaneous selections.
    Selection? saved = pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetUInt32(out uint id) ? new(id)
     : pair.Value.ValueKind == JsonValueKind.Object ? pair.Value.Deserialize<Selection>() : null;
    if (saved is not null && (saved.Playing is null || saved.Playing.Length is > 0 and <= 32 && saved.Playing.All(id => id > 0) && saved.Playing.Distinct().Count() == saved.Playing.Length)) selections[pair.Name] = saved;
   }
  } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
 }
 internal uint Get(string bot) => selections.GetValueOrDefault(bot)?.AppId ?? 0;
 internal uint[]? GetPlaying(string bot) => selections.GetValueOrDefault(bot)?.Playing?.ToArray();
 internal void Set(string bot, uint appID) {
  selections[bot] = new(appID);
  Save();
 }
 internal void SetPlaying(string bot, IEnumerable<uint> appIDs) {
  uint[] games = appIDs.Distinct().ToArray();
  if (games.Length is < 1 or > 32 || games.Any(id => id == 0)) throw new ArgumentException("Select 1–32 games per account.");
  selections[bot] = new(0, games);
  Save();
 }
 private void Save() {
  if (path is null) return;
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  string temporary = path + ".tmp";
  File.WriteAllText(temporary, JsonSerializer.Serialize(selections));
  File.Move(temporary, path, true);
 }
}
