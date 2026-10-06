// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Text.Json.Nodes;
using SteamCardPilot;

await ArtworkChecks.RunAsync();

static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
Require(new[] { "alpha", "Account_2", "my-bot", "COM10" }.All(AccountNameRules.IsValid), "Valid account names must remain supported.");
Require(new[] { "", "ASF", "asf", "CON", "con", "PRN", "AUX", "NUL", "COM1", "LPT9", "../escape", new string('a', 65) }.All(name => !AccountNameRules.IsValid(name)), "Reserved device names, engine names, paths, and oversized account names must be rejected before creating files.");
var active = BotView.FromJson("Sample", JsonNode.Parse("""
{"IsConnectedAndLoggedOn":true,"KeepRunning":true,"RequiredInput":0,"Nickname":"Demo Player","CardsFarmer":{"NowFarming":true,"GamesToFarm":[{"CardsRemaining":3},{"CardsRemaining":2}],"CurrentGamesFarming":[{"GameName":"Sample Game"}]}}
"""));
Require(active.State == "Farming cards" && active.Cards == 5 && active.Details.Contains("Sample Game"), "Incorrect card count or farming state.");
var waiting = BotView.FromJson("Sample", JsonNode.Parse("""{"RequiredInput":5,"KeepRunning":false}"""));
Require(waiting.State == "Waiting for sign-in" && waiting.RequiredInput == 5, "Incorrect Steam Guard state.");
var mobile = BotView.FromJson("Sample", JsonNode.Parse("""{"RequiredInput":7,"KeepRunning":true}"""));
Require(mobile.State == "Waiting for mobile approval", "Mobile approval must not be treated as code input.");
var dashboardCheck = new DashboardData();
var gameSnapshot = JsonNode.Parse("""
{"TestAccount":{"IsConnectedAndLoggedOn":true,"CardsFarmer":{"NowFarming":true,"TimeRemaining":"02:30:00","CurrentGamesFarming":[{"AppID":42}],"GamesToFarm":[{"AppID":42,"GameName":"Sample","CardsRemaining":3,"HoursPlayed":2.5},{"AppID":43,"GameName":"Queued","CardsRemaining":2}]}}}
""")!.AsObject();
dashboardCheck.Update(gameSnapshot);
Require(dashboardCheck.Games.Count == 2 && dashboardCheck.Games.Count(g => g.Active) == 1 && dashboardCheck.RemainingTime == TimeSpan.FromMinutes(150), "Incorrect active game, queue, or engine estimate.");
gameSnapshot["TestAccount"]!["CardsFarmer"]!["GamesToFarm"]![0]!["CardsRemaining"] = 2;
dashboardCheck.Update(gameSnapshot);
Require(dashboardCheck.ObservedDrops == 1, "Observed card decreases must be counted.");
gameSnapshot["TestAccount"]!["CardsFarmer"]!["GamesToFarm"] = new JsonArray();
dashboardCheck.Update(gameSnapshot);
Require(dashboardCheck.ObservedDrops == 1 && dashboardCheck.Games.Count == 2 && dashboardCheck.Games.All(g => !g.Active && !g.Listed), "Stopping must preserve games; clearing the queue must not count as drops.");
gameSnapshot["TestAccount"]!["IsConnectedAndLoggedOn"] = false;
dashboardCheck.Update(gameSnapshot);
Require(dashboardCheck.Games.Count == 2 && dashboardCheck.Games.All(g => !g.Active), "Games must be preserved when the connection drops.");
string cacheFile = Path.Combine(Path.GetTempPath(), "SteamCardPilot-Games-" + Guid.NewGuid().ToString("N"), "games.json");
var cachedDashboard = new DashboardData(cacheFile);
gameSnapshot["TestAccount"]!["CardsFarmer"]!["GamesToFarm"] = JsonNode.Parse("""[{"AppID":42,"GameName":"Sample","CardsRemaining":2}]""");
cachedDashboard.Update(gameSnapshot);
var restoredDashboard = new DashboardData(cacheFile);
restoredDashboard.Update(JsonNode.Parse("""{"TestAccount":{"IsConnectedAndLoggedOn":true,"CardsFarmer":{"GamesToFarm":[]}}}""")!.AsObject());
Require(restoredDashboard.Games.Count == 1 && !restoredDashboard.Games[0].Active && restoredDashboard.Games[0].Cards == 2, "Games must be restored when the app reopens.");
restoredDashboard.Update(new JsonObject());
Require(restoredDashboard.Games.Count == 0, "Deleted accounts must be removed from the catalog.");
File.WriteAllText(cacheFile, """[null,{"BotName":"TestAccount","AppId":42,"Name":null,"Cards":2,"Hours":0,"Active":false}]""");
var malformedDashboard = new DashboardData(cacheFile);
malformedDashboard.Update(JsonNode.Parse("""{"TestAccount":{"CardsFarmer":{"GamesToFarm":[]}}}""")!.AsObject());
Require(malformedDashboard.Games.Count == 0, "Missing or null cache records must not crash the app.");
var gapDashboard = new DashboardData();
var gapSnapshot = JsonNode.Parse("""{"TestAccount":{"IsConnectedAndLoggedOn":true,"CardsFarmer":{"NowFarming":true,"GamesToFarm":[{"AppID":42,"CardsRemaining":5}]}}}""")!.AsObject();
gapDashboard.Update(gapSnapshot);
gapSnapshot["TestAccount"]!["IsConnectedAndLoggedOn"] = false;
gapDashboard.Update(gapSnapshot);
gapSnapshot["TestAccount"]!["IsConnectedAndLoggedOn"] = true;
gapSnapshot["TestAccount"]!["CardsFarmer"]!["GamesToFarm"]![0]!["CardsRemaining"] = 3;
gapDashboard.Update(gapSnapshot);
Require(gapDashboard.ObservedDrops == 0, "Unobserved decreases while offline must not count as session drops.");
gapSnapshot["TestAccount"]!["CardsFarmer"]!["Paused"] = true;
gapSnapshot["TestAccount"]!["CardsFarmer"]!["TimeRemaining"] = "02:00:00";
gapSnapshot["TestAccount"]!["CardsFarmer"]!["CurrentGamesFarming"] = JsonNode.Parse("""[{"AppID":42}]""");
gapDashboard.Update(gapSnapshot);
Require(gapDashboard.Games.All(g => !g.Active) && gapDashboard.RemainingTime == TimeSpan.Zero, "Paused accounts must not appear active or contribute to remaining time.");
string selectionFile = Path.Combine(Path.GetDirectoryName(cacheFile)!, "selection.json");
var selectionCheck = new FarmingSelection(selectionFile);
selectionCheck.Set("TestAccount", 42);
selectionCheck.Set("SecondAccount", 43);
var restoredSelection = new FarmingSelection(selectionFile);
Require(restoredSelection.Get("TestAccount") == 42 && restoredSelection.Get("SecondAccount") == 43 && restoredSelection.Get("NewAccount") == 0, "Selections must be isolated by account and restored on reopening.");
restoredSelection.Set("TestAccount", 0);
Require(new FarmingSelection(selectionFile).Get("TestAccount") == 0, "Switching to all games must be saved.");
string relativeSelectionFile = Path.GetRelativePath(Environment.CurrentDirectory, selectionFile);
var relativeSelection = new FarmingSelection(relativeSelectionFile);
relativeSelection.Set("RelativeAccount", 44);
Require(new FarmingSelection(selectionFile).Get("RelativeAccount") == 44, "Relative selection paths must save to the same file.");
// A bare file name has no directory component. Both stores must still persist it.
string previousDirectory = Environment.CurrentDirectory;
try {
 Environment.CurrentDirectory = Path.GetDirectoryName(cacheFile)!;
 new FarmingSelection("selection.json").Set("BarePathAccount", 45);
 var relativeDashboard = new DashboardData("games.json");
 relativeDashboard.Update(gameSnapshot);
 Require(new FarmingSelection("selection.json").Get("BarePathAccount") == 45, "A bare selection filename must support saving.");
 Require(JsonNode.Parse(File.ReadAllText("games.json"))!.AsArray().Count == 1, "A bare cache filename must support saving.");
} finally { Environment.CurrentDirectory = previousDirectory; }
Console.WriteLine("Game, queue, estimate, and session progress checks passed.");
Console.WriteLine("Status and card count checks passed.");
Require(ParentalPinSettings.IsValid("0123") && !ParentalPinSettings.IsValid("12a4") && !ParentalPinSettings.IsValid("１２３４"), "PINs must contain four ASCII digits; leading zeroes must be preserved.");
var originalSettings = JsonNode.Parse("""{"SteamLogin":"offline-check","Enabled":false,"SteamPassword":"existing-password","PluginSetting":{"Keep":true}}""")!.AsObject();
var changedSettings = ParentalPinSettings.WithPin(originalSettings, "0123");
Require(changedSettings["SteamPassword"]!.GetValue<string>() == "existing-password" && changedSettings["PluginSetting"]!["Keep"]!.GetValue<bool>() && originalSettings["SteamParentalCode"] is null, "Saving the PIN must preserve existing settings.");
if (args.Length == 0) return;
string testPath = Path.Combine(Path.GetTempPath(), "SteamCardPilot-Check-" + Guid.NewGuid().ToString("N"));
using var engine = new EngineClient(testPath, Path.GetFullPath(args[0]));
List<string> errors = [];
engine.Log += line => { if (line.Contains("|ERROR|", StringComparison.Ordinal)) { lock (errors) errors.Add(line); } };
try {
 await engine.StartAsync();
 await engine.StartAsync();
 bool invalidResponseRejected = false;
 try { await engine.RequestAsync("missing-desktop-check-endpoint"); }
 catch (InvalidOperationException) { invalidResponseRejected = true; }
 Require(invalidResponseRejected, "Non-JSON error responses must produce a readable engine operation error.");
 Require((await engine.RequestAsync("Api/Bot/ASF"))?.AsObject().Count == 0, "An empty workspace was expected.");
 await engine.RequestAsync("Api/Bot/TestAccount", new { BotConfig = new { Enabled = false, SteamLogin = "offline-check" } }, true);
 await engine.WaitForBotAsync("TestAccount");
 bool offlineSelectionRejected = false;
 try { await engine.RequestAsync("Api/Bot/TestAccount/DesktopGame/42", post: true); }
 catch (InvalidOperationException) { offlineSelectionRejected = true; }
 Require(offlineSelectionRejected, "Offline accounts must reject farming requests.");
 await engine.RequestAsync("Api/Bot/TestAccount/Input", new { Type = 2, Value = "not-a-real-password" }, true);
 string botFile = Path.Combine(testPath, "config", "TestAccount.json");
 Require(!File.ReadAllText(botFile).Contains("not-a-real-password"), "Sign-in passwords must not be persisted.");
 await engine.SaveParentalPinAsync("TestAccount", "0123");
 var savedPinConfig = JsonNode.Parse(File.ReadAllText(botFile))!;
 Require(savedPinConfig["SteamParentalCode"]!.GetValue<string>() == "0123" && savedPinConfig["SteamLogin"]!.GetValue<string>() == "offline-check", "The PIN and existing account settings must be saved.");
 var global = JsonNode.Parse(File.ReadAllText(Path.Combine(testPath, "config", "ASF.json")))!;
 Require(global["CurrentCulture"]!.GetValue<string>() == "en-US" && global["UpdatePeriod"]!.GetValue<int>() == 0, "Incorrect distribution settings.");
 var ipc = JsonNode.Parse(File.ReadAllText(Path.Combine(testPath, "config", "IPC.config")))!;
 using var unauthenticated = new HttpClient();
 var rejected = await unauthenticated.GetAsync(ipc["Kestrel"]!["Endpoints"]!["HTTP"]!["Url"]!.GetValue<string>() + "/Api/ASF");
 Require(!rejected.IsSuccessStatusCode, "Unauthenticated API access must be rejected.");
 await engine.StopAsync();
 await engine.StartAsync();
 await engine.WaitForBotAsync("TestAccount");
 var bots = await engine.RequestAsync("Api/Bot/ASF");
 Require(bots?["TestAccount"]?["KeepRunning"]?.GetValue<bool>() == false, "Disabled accounts must stay disabled after restarting.");
 Require(!File.ReadAllText(botFile).Contains("not-a-real-password"), "Restarting must not persist the password.");
 Require(JsonNode.Parse(File.ReadAllText(botFile))!["SteamParentalCode"]!.GetValue<string>() == "0123", "The PIN must survive restarting.");
 lock (errors) Require(errors.Count == 0, "Engine or plugin error: " + string.Join(Environment.NewLine, errors));
 Console.WriteLine("Engine connectivity, account creation, password handling, API authentication, and restart checks passed.");
} finally {
 await engine.StopAsync();
 Console.WriteLine("Check files: " + testPath);
}
