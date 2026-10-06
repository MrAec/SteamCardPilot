// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Text.Json.Nodes;

namespace AutoPlaySteam;

internal sealed record BotView(string Name, string State, string Details, int Cards, int RequiredInput, bool Online) {
 public bool Running { get; init; }
 public string CardsLabel => $"{Cards} cards left";
 internal static BotView FromJson(string name, JsonNode? bot) {
  bool online = bot?["IsConnectedAndLoggedOn"]?.GetValue<bool>() == true;
  bool running = bot?["KeepRunning"]?.GetValue<bool>() == true;
  JsonNode? farmer = bot?["CardsFarmer"];
  bool farming = farmer?["NowFarming"]?.GetValue<bool>() == true;
  bool paused = farmer?["Paused"]?.GetValue<bool>() == true;
  int input = bot?["RequiredInput"]?.GetValue<int>() ?? 0;
  string state = input == 7 ? "Waiting for mobile approval" : input != 0 ? "Waiting for sign-in" : !running ? "Stopped" : !online ? "Connecting" : paused ? "Paused" : farming ? "Farming cards" : "Ready";
  JsonArray? games = farmer?["GamesToFarm"]?.AsArray();
  int cards = games?.Sum(g => g?["CardsRemaining"]?.GetValue<int>() ?? 0) ?? 0;
  string current = farmer?["CurrentGamesFarming"] is JsonArray playing && playing.Count > 0 ? string.Join(", ", playing.Select(g => g?["GameName"]?.GetValue<string>() ?? g?["AppID"]?.ToString())) : "No game is running";
  string nickname = bot?["Nickname"]?.GetValue<string>() ?? name;
  return new(name, state, $"{nickname}  •  {current}", cards, input, online) { Running = running };
 }
}
