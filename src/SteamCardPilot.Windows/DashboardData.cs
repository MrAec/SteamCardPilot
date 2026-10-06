// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Text.Json.Nodes;
using System.Text.Json;
using System.IO;

namespace SteamCardPilot;

internal sealed record ActivityView(string Text, string Time);
internal sealed record InventoryView(string Name, string Detail);

internal sealed record GameView(string BotName, uint AppId, string Name, int Cards, float Hours, bool Active, bool Listed = true) {
 public string Artwork => $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{AppId}/header.jpg";
 public string Initial => Name.Length == 0 ? "?" : Name[..1].ToUpperInvariant();
 public string State => Active ? "●  Farming cards" : Listed ? "Waiting" : "Last known";
 public string CardsLabel => $"{Cards} cards left";
 public string HoursLabel => $"{Hours:0.#} hours played";
 public string Estimate => DashboardData.FormatTime(TimeSpan.FromMinutes(Cards * 30));
 public string Key => BotName + "/" + AppId;
}

internal sealed class DashboardData {
 private readonly Dictionary<string, int> previousCards = new(StringComparer.Ordinal);
 private readonly Dictionary<string, GameView> catalog = new(StringComparer.Ordinal);
 private readonly string? cachePath;
 public DashboardData(string? cachePath = null) {
  this.cachePath = cachePath is null ? null : Path.GetFullPath(cachePath);
  if (cachePath is null || !File.Exists(cachePath)) return;
  try {
   foreach (GameView game in JsonSerializer.Deserialize<List<GameView>>(File.ReadAllText(cachePath)) ?? []) {
    if (game is null || game.AppId == 0 || string.IsNullOrEmpty(game.BotName) || game.Name is null || game.Cards < 0 || !float.IsFinite(game.Hours) || game.Hours < 0) continue;
    catalog[game.Key] = game with { Active = false, Listed = false };
   }
  } catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
 }
 public int ObservedDrops { get; private set; }
 public List<GameView> Games { get; private set; } = [];
 public TimeSpan RemainingTime { get; private set; }

 public void Update(JsonObject bots) {
  List<GameView> games = [];
  TimeSpan longest = TimeSpan.Zero;
  foreach ((string name, JsonNode? bot) in bots) {
   JsonNode? farmer = bot?["CardsFarmer"];
   bool online = bot?["IsConnectedAndLoggedOn"]?.GetValue<bool>() == true;
   bool farming = farmer?["NowFarming"]?.GetValue<bool>() == true && farmer?["Paused"]?.GetValue<bool>() != true;
   if (!online) foreach (string key in previousCards.Keys.Where(k => k.StartsWith(name + "/", StringComparison.Ordinal)).ToArray()) previousCards.Remove(key);
   HashSet<uint> active = farmer?["CurrentGamesFarming"] is JsonArray current ? current.Select(g => g?["AppID"]?.GetValue<uint>() ?? 0).ToHashSet() : [];
   if (online && farming && TimeSpan.TryParse(farmer?["TimeRemaining"]?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out TimeSpan time) && time > longest) longest = time;
   if (farmer?["GamesToFarm"] is not JsonArray queue) continue;
   foreach (JsonNode? game in queue) {
    uint appId = game?["AppID"]?.GetValue<uint>() ?? 0;
    if (appId == 0) continue;
    var row = new GameView(name, appId, game?["GameName"]?.GetValue<string>() ?? $"Game {appId}", game?["CardsRemaining"]?.GetValue<int>() ?? 0, game?["HoursPlayed"]?.GetValue<float>() ?? 0, online && farming && active.Contains(appId));
    games.Add(row);
    // Count only observed decreases in known games. Removed queues and offline accounts are not card drops.
    if (online) {
     if (previousCards.TryGetValue(row.Key, out int old) && old > row.Cards) ObservedDrops += old - row.Cards;
     previousCards[row.Key] = row.Cards;
    }
   }
  }
  HashSet<string> present = games.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
  foreach (string missing in previousCards.Keys.Where(k => !present.Contains(k)).ToArray()) previousCards.Remove(missing);
  foreach (string key in catalog.Keys.ToArray()) {
   if (!bots.ContainsKey(catalog[key].BotName)) { catalog.Remove(key); previousCards.Remove(key); }
   else catalog[key] = catalog[key] with { Active = false, Listed = false };
  }
  foreach (GameView game in games) catalog[game.Key] = game;
  Games = catalog.Values.OrderBy(g => g.BotName).ThenBy(g => g.Name).ToList();
  if (cachePath is not null) {
   try {
    Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
    string temporary = cachePath + ".tmp";
    File.WriteAllText(temporary, JsonSerializer.Serialize(Games));
    File.Move(temporary, cachePath, true);
   } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
  }
  RemainingTime = longest;
 }

 public static string FormatTime(TimeSpan time) => time <= TimeSpan.Zero ? "—" : time.TotalHours >= 1 ? $"{(int)time.TotalHours} h {time.Minutes} min" : $"{Math.Max(1, (int)time.TotalMinutes)} min";
}
