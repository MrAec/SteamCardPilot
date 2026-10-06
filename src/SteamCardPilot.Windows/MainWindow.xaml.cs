// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SteamCardPilot;

public partial class MainWindow : Window {
 [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$")]
 private static partial Regex AccountNamePattern();
 private readonly EngineClient engine = new();
 private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(4) };
 private readonly SemaphoreSlim operations = new(1, 1);
 private readonly Mutex instance = new(false, "Local\\AutoPlaySteam.Desktop");
 private bool ready;
 private bool closing;
 private bool closed;
 private readonly SemaphoreSlim refreshGate = new(1, 1);
 private bool refreshFailed;
 private readonly FarmingSelection farmingSelection;
 private readonly DashboardData dashboard;
 private readonly List<ActivityView> activities = [];
 private bool queueOnly;
 private readonly HashSet<string> scannedAccounts = new(StringComparer.Ordinal);
 private BotView? Selected => BotList.SelectedItem as BotView;

 public MainWindow() {
  dashboard = new(Environment.GetCommandLineArgs().Contains("--preview") ? null : Path.Combine(engine.DataPath, "games.json"));
  farmingSelection = new(Environment.GetCommandLineArgs().Contains("--preview") ? null : Path.Combine(engine.DataPath, "selection.json"));
  InitializeComponent();
  DataPathText.Text = Environment.GetCommandLineArgs().Contains("--preview") ? "%LOCALAPPDATA%\\AutoPlaySteam" : engine.DataPath;
  engine.Log += line => Dispatcher.BeginInvoke(() => {
   LogOutput.AppendText(line + Environment.NewLine);
   if (LogOutput.Text.Length > 100000) LogOutput.Text = LogOutput.Text[^80000..];
   LogOutput.ScrollToEnd();
   if (line.Contains("|INFO|", StringComparison.Ordinal) || line.Contains("|WARN|", StringComparison.Ordinal)) {
    string summary = line.Split('|').Last();
    int method = summary.IndexOf("() ", StringComparison.Ordinal);
    if (method >= 0) summary = summary[(method + 3)..];
    if (summary.Length > 120) summary = summary[..117] + "…";
    activities.Insert(0, new ActivityView(summary, DateTime.Now.ToString("HH:mm")));
    if (activities.Count > 5) activities.RemoveAt(activities.Count - 1);
    RecentEvents.ItemsSource = activities.ToArray();
   }
  });
  Loaded += OnLoaded;
  SizeChanged += (_, _) => ApplyResponsiveLayout();
  Closing += OnClosing;
  timer.Tick += async (_, _) => await RefreshAsync();
 }

 private async void OnLoaded(object sender, RoutedEventArgs e) {
  string[] args = Environment.GetCommandLineArgs();
  int preview = Array.IndexOf(args, "--preview");
  if (preview >= 0 && preview + 1 < args.Length) {
   if (args.Contains("--mobile")) {
    ready = true;
    var mobile = BotView.FromJson("Demo Account", JsonNode.Parse("""{"RequiredInput":7,"KeepRunning":true}"""));
    BotList.ItemsSource = new[] { mobile };
    BotList.SelectedItem = mobile;
    EmptyState.Visibility = Visibility.Collapsed;
    AccountCount.Text = "1";
    ShowPane(ManagePane, "Accounts");
    if (GuardValue.Visibility != Visibility.Collapsed || GuardSubmit.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Mobile approval must not show a code input.");
   }
   if (args.Contains("--demo")) LoadDesignPreview();
   if (args.Contains("--compact")) { Width = 1180; Height = 800; }
   if (args.Contains("--accounts")) ShowPane(AccountsPane, "Add Account");
   if (args.Contains("--games")) { queueOnly = false; UpdateDashboard(); ShowPane(GamesPane, "Games"); AllGames.SelectedIndex = 0; }
   if (args.Contains("--about")) ShowPane(AboutPane, "About");
   if (args.Contains("--licenses")) { LoadLicense("LICENSE-Mr_Aec.txt"); ShowPane(LicensesPane, "Licenses and Components"); }
   StatusText.Text = args.Contains("--demo") ? "Design preview • Account and game data are illustrative." : "Ready to add your first account.";
   ConnectionText.Text = "●  Preview";
   await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
   if (args.Contains("--demo")) await Task.Delay(2500);
   var surface = (FrameworkElement)Content;
   var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
   bitmap.Render(surface);
   var encoder = new PngBitmapEncoder();
   encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using (var output = File.Create(args[preview + 1])) encoder.Save(output);
   closed = true;
   Close();
   return;
  }
  // A single owner prevents two desktops from sharing the same managed engine data.
  bool acquired;
  try { acquired = instance.WaitOne(0); }
  catch (AbandonedMutexException) { acquired = true; }
  if (!acquired) { MessageBox.Show("Steam Card Pilot is already running.", "Steam Card Pilot"); closed = true; Close(); return; }
  await RunAsync(async () => { await engine.StartAsync(); ready = true; timer.Start(); await RefreshAsync(); }, "Engine ready. Add an account or import existing account files.");
 }

 private async Task RunAsync(Func<Task> action, string success = "Operation complete.") {
  if (closing) return;
  if (!await operations.WaitAsync(0)) { StatusText.Text = "Waiting for the previous operation…"; return; }
  try { refreshFailed = false; StatusText.Text = "Working…"; await action(); if (!refreshFailed) StatusText.Text = success; }
  catch (Exception e) { StatusText.Text = e is OperationCanceledException ? "The request timed out. Check Activity for the current operation status." : e is HttpRequestException ? "The engine is unavailable. You can restart it from Settings." : "Operation failed: " + e.Message; if (!ready) ConnectionText.Text = "●  Engine not ready"; }
  finally { operations.Release(); }
 }

 private async Task RefreshAsync() {
  if (!ready || closing || !await refreshGate.WaitAsync(0)) return;
  try {
   JsonObject bots = (await engine.RequestAsync("Api/Bot/ASF")) as JsonObject ?? throw new InvalidOperationException("Could not load accounts. Saved games are preserved.");
   string? selected = Selected?.Name;
   List<BotView> rows = bots?.Select(pair => BotView.FromJson(pair.Key, pair.Value)).OrderBy(b => b.Name).ToList() ?? [];
   BotList.ItemsSource = rows;
   BotList.SelectedItem = rows.FirstOrDefault(b => b.Name == selected) ?? rows.FirstOrDefault();
   AccountCount.Text = rows.Count.ToString(); OnlineCount.Text = rows.Count(b => b.Online).ToString(); CardCount.Text = rows.Sum(b => b.Cards).ToString();
   EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
   dashboard.Update(bots ?? new JsonObject());
   UpdateDashboard();
   scannedAccounts.RemoveWhere(name => !rows.Any(b => b.Name == name && b.Online));
   foreach (BotView bot in rows.Where(b => b.Online && !scannedAccounts.Contains(b.Name))) {
    await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(bot.Name)}/DesktopGames", post: true);
    scannedAccounts.Add(bot.Name);
   }
   ConnectionText.Text = "●  Engine connected";
   refreshFailed = false;
  } catch (Exception e) { refreshFailed = true; ConnectionText.Text = "●  Disconnected"; StatusText.Text = "Engine unavailable: " + e.Message; BotActions.IsEnabled = false; GuardPanel.Visibility = Visibility.Collapsed; }
  finally { refreshGate.Release(); }
 }

 private void ShowPane(UIElement pane, string title) {
  foreach (UIElement item in new UIElement[] { DashboardPane, ManagePane, GamesPane, InventoryPane, AccountsPane, CommandsPane, LogsPane, SettingsPane, AboutPane, LicensesPane }) item.Visibility = item == pane ? Visibility.Visible : Visibility.Collapsed;
  PageTitle.Text = title;
  foreach (Button button in new[] { NavHome, NavGames, NavQueue, NavInventory, NavSettings, NavAccounts, NavCommands, NavLogs, NavAbout }) button.Background = Brushes.Transparent;
  Button? active = pane == DashboardPane ? NavHome : pane == ManagePane || pane == AccountsPane ? NavAccounts : pane == GamesPane ? queueOnly ? NavQueue : NavGames : pane == InventoryPane ? NavInventory : pane == SettingsPane ? NavSettings : pane == CommandsPane ? NavCommands : pane == LogsPane ? NavLogs : NavAbout;
  active.Background = new SolidColorBrush(Color.FromRgb(22, 71, 123));
  active.BringIntoView();
 }
 private void Dashboard_Click(object sender, RoutedEventArgs e) => ShowPane(DashboardPane, "Overview");
 private void ManageAccounts_Click(object sender, RoutedEventArgs e) => ShowPane(ManagePane, "Accounts");
 private void Games_Click(object sender, RoutedEventArgs e) { queueOnly = false; UpdateDashboard(); ShowPane(GamesPane, "Games"); }
 private void Queue_Click(object sender, RoutedEventArgs e) { queueOnly = true; UpdateDashboard(); ShowPane(GamesPane, "Queue"); }
 private void Inventory_Click(object sender, RoutedEventArgs e) => ShowPane(InventoryPane, "Inventory");
 private void About_Click(object sender, RoutedEventArgs e) => ShowPane(AboutPane, "About");
 private void Licenses_Click(object sender, RoutedEventArgs e) { LoadLicense("LICENSE-Mr_Aec.txt"); ShowPane(LicensesPane, "Licenses and Components"); }
 private void LicenseFile_Click(object sender, RoutedEventArgs e) => LoadLicense((string)((Button)sender).Tag);
 private void LoadLicense(string file) {
  try { LicenseText.Text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file)); }
  catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LicenseText.Text = "Could not read document: " + e.Message; }
 }
 private void Accounts_Click(object sender, RoutedEventArgs e) => ShowPane(AccountsPane, "Add Account");
 private void Commands_Click(object sender, RoutedEventArgs e) => ShowPane(CommandsPane, "Commands");
 private void Logs_Click(object sender, RoutedEventArgs e) => ShowPane(LogsPane, "Activity");
 private void Settings_Click(object sender, RoutedEventArgs e) => ShowPane(SettingsPane, "Settings");
 private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

 private void BotList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
  ProfileName.Text = Selected?.Name ?? "No account added";
  ProfileState.Text = Selected is { } selected ? "●  " + selected.State : "●  Waiting for connection";
  ProfileState.Foreground = Selected?.Online == true ? new SolidColorBrush(Color.FromRgb(74, 222, 144)) : new SolidColorBrush(Color.FromRgb(115, 144, 173));
  BotActions.IsEnabled = ready && Selected is not null;
  int type = Selected?.RequiredInput ?? 0;
  GuardPanel.Visibility = type is 1 or 2 or 3 or 4 or 5 or 7 ? Visibility.Visible : Visibility.Collapsed;
  GuardValue.Visibility = type == 7 ? Visibility.Collapsed : Visibility.Visible;
  GuardSubmit.Visibility = type == 7 ? Visibility.Collapsed : Visibility.Visible;
  GuardLabel.Text = type switch { 1 => "Steam username required", 2 => "Steam password required", 3 => "Email Steam Guard code required", 4 => "Family View PIN required", 5 => "Steam requires a mobile code for this sign-in. Find it in Steam Guard in your Steam app.", 7 => "Open the Steam app on your phone and approve the sign-in request. Sign-in continues automatically; no code is needed here.", _ => "Sign-in information required" };
 }
 private async void BotAction_Click(object sender, RoutedEventArgs e) {
  if (Selected is not { } bot) return;
  string action = (string)((Button)sender).Tag;
  if (action == "Resume") {
   if (!bot.Online) { StatusText.Text = "Connect the account and wait for Steam sign-in to finish."; return; }
   await RunAsync(async () => { await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(bot.Name)}/DesktopGame/{farmingSelection.Get(bot.Name)}", post: true); await RefreshAsync(); }, "Resuming the saved game selection.");
   return;
  }
  await RunAsync(async () => { await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(bot.Name)}/{action}", action == "Pause" ? new { Permanent = true } : null, true); await RefreshAsync(); });
 }
 private async void ParentalPin_Click(object sender, RoutedEventArgs e) {
  if (!ready || Selected is not { } bot) return;
  var dialog = new ParentalPinWindow(bot.Name) { Owner = this };
  if (dialog.ShowDialog() != true) return;
  await RunAsync(async () => { await engine.SaveParentalPinAsync(bot.Name, dialog.Pin); await RefreshAsync(); }, "Family View PIN saved. Reloading the account with its updated settings.");
 }
 private async void Guard_Click(object sender, RoutedEventArgs e) {
  if (Selected is not { RequiredInput: > 0 } bot || bot.RequiredInput == 7) return;
  string value = bot.RequiredInput == 2 ? GuardValue.Password : GuardValue.Password.Trim();
  if (string.IsNullOrEmpty(value)) { StatusText.Text = "Enter the requested sign-in information."; return; }
  await RunAsync(async () => {
   if (bot.RequiredInput == 4) {
    await engine.SaveParentalPinAsync(bot.Name, value);
    GuardValue.Clear();
    await RefreshAsync();
    return;
   }
   await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(bot.Name)}/Input", new { Type = bot.RequiredInput, Value = value }, true);
   GuardValue.Clear();
   JsonNode? loaded = await engine.RequestAsync("Api/Bot/" + Uri.EscapeDataString(bot.Name));
   if (loaded?[bot.Name]?["KeepRunning"]?.GetValue<bool>() != true) await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(bot.Name)}/Start", post: true);
   await RefreshAsync();
  }, "Sign-in information submitted. Connecting the account.");
 }
 private async void AddAccount_Click(object sender, RoutedEventArgs e) {
  if (!ready) { StatusText.Text = "Wait for the engine connection first."; return; }
  string name = NewName.Text.Trim(), login = NewLogin.Text.Trim(), password = NewPassword.Password;
  string parentalPin = NewParentalPin.Password;
  if (!AccountNamePattern().IsMatch(name) || name.Equals("ASF", StringComparison.OrdinalIgnoreCase)) { StatusText.Text = "Account names must contain 1–64 letters, digits, hyphens, or underscores and cannot be ASF."; return; }
  if (string.IsNullOrWhiteSpace(login)) { StatusText.Text = "Enter your Steam username."; return; }
  if (parentalPin.Length != 0 && !ParentalPinSettings.IsValid(parentalPin)) { StatusText.Text = "The Family View PIN must contain exactly four digits."; return; }
  await RunAsync(async () => {
   if (File.Exists(Path.Combine(engine.DataPath, "config", name + ".json"))) throw new InvalidOperationException("This account name already exists.");
   var accountConfig = new JsonObject { ["Enabled"] = true, ["SteamLogin"] = login, ["UseLoginKeys"] = true };
   if (parentalPin.Length != 0) accountConfig["SteamParentalCode"] = parentalPin;
   await engine.RequestAsync("Api/Bot/" + Uri.EscapeDataString(name), new { BotConfig = accountConfig }, true);
   await engine.WaitForBotAsync(name);
   if (!string.IsNullOrEmpty(password)) await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(name)}/Input", new { Type = 2, Value = password }, true);
   NewPassword.Clear(); NewParentalPin.Clear(); NewLogin.Clear(); NewName.Clear();
   JsonNode? loaded = await engine.RequestAsync("Api/Bot/" + Uri.EscapeDataString(name));
   if (loaded?[name]?["KeepRunning"]?.GetValue<bool>() != true) await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(name)}/Start", post: true);
   await RefreshAsync(); ShowPane(ManagePane, "Accounts");
  }, "Account created. Select it and submit any required sign-in information.");
 }
 private async void Import_Click(object sender, RoutedEventArgs e) {
  if (!ready) { StatusText.Text = "Wait for the engine connection first."; return; }
  var dialog = new OpenFileDialog { Title = "Select account files", Filter = "Account files (*.json)|*.json", Multiselect = true };
  if (dialog.ShowDialog(this) != true) return;
  await RunAsync(async () => {
   int copied = 0, skipped = 0;
   foreach (string source in dialog.FileNames) {
    string name = Path.GetFileNameWithoutExtension(source);
    if (name.Equals("ASF", StringComparison.OrdinalIgnoreCase) || !AccountNamePattern().IsMatch(name) || File.Exists(Path.Combine(engine.DataPath, "config", name + ".json"))) { skipped++; continue; }
    JsonNode? config = JsonNode.Parse(await File.ReadAllTextAsync(source));
    if (config is not JsonObject) throw new InvalidOperationException($"{name}: account files must contain a valid JSON object.");
    await engine.RequestAsync("Api/Bot/" + Uri.EscapeDataString(name), new { BotConfig = config }, true);
    copied++;
   }
   CommandOutput.AppendText($"Imported {copied} accounts; skipped {skipped} files.\n");
   await RefreshAsync();
  }, "Import complete. See Commands for details.");
 }
 private async void Command_Click(object sender, RoutedEventArgs e) => await ExecuteCommandAsync();

 private void UpdateDashboard() {
  List<GameView> active = dashboard.Games.Where(g => g.Active).ToList();
  List<GameView> queued = dashboard.Games.Where(g => !g.Active && g.Listed && g.Cards > 0).ToList();
  ActiveGames.ItemsSource = active;
  QueueGames.ItemsSource = queued;
  string? selection = (AllGames.SelectedItem as GameView)?.Key;
  List<GameView> displayed = queueOnly ? queued : dashboard.Games;
  AllGames.ItemsSource = displayed;
  AllGames.SelectedItem = displayed.FirstOrDefault(g => g.Key == selection);
  GamesDescription.Text = "Select a game to start. Stopping keeps the connection and game list; cached card counts are marked as last known.";
  CardCount.Text = dashboard.Games.Sum(g => g.Cards).ToString();
  GameCountLabel.Text = $"{dashboard.Games.Count} games • {dashboard.Games.Count(g => !g.Listed)} last known";
  ActiveCount.Text = active.Count.ToString();
  EarnedCount.Text = dashboard.ObservedDrops.ToString();
  TimeEstimate.Text = DashboardData.FormatTime(dashboard.RemainingTime);
  ActiveTitle.Text = $"●  Games Farming Now ({active.Count})";
  QueueTitle.Text = $"Queued Games ({queued.Count})";
  ActiveSummary.Text = $"●  Active games: {active.Count}";
  QueueSummary.Text = $"●  Queued games: {queued.Count}";
  ActiveEmpty.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
  QueueEmpty.Visibility = queued.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
  int total = dashboard.ObservedDrops + dashboard.Games.Sum(g => g.Cards);
  double progress = total == 0 ? 0 : (double)dashboard.ObservedDrops / total;
  ProgressPercent.Text = total == 0 ? "—" : $"{progress:P0}";
  ProgressCards.Text = dashboard.ObservedDrops.ToString();
  if (progress <= 0) ProgressArc.Data = null;
  else {
   double angle = Math.Min(progress, 0.9999) * Math.PI * 2 - Math.PI / 2;
   var end = new Point(57 + 49 * Math.Cos(angle), 57 + 49 * Math.Sin(angle));
   var path = new PathFigure { StartPoint = new Point(57, 8) };
   path.Segments.Add(new ArcSegment(end, new Size(49, 49), 0, progress > 0.5, SweepDirection.Clockwise, true));
   ProgressArc.Data = new PathGeometry(new[] { path });
  }
 }
 private void ApplyResponsiveLayout() {
  bool compact = ActualWidth < 1380;
  SidebarColumn.Width = new GridLength(compact ? 196 : 236);
  SidePanelColumn.Width = new GridLength(compact ? 240 : 292);
  TimeEstimate.FontSize = compact ? 18 : 26;
  ProgressCards.FontSize = compact ? 18 : 22;
  DropsLabel.Text = compact ? "Observed" : "Observed Drops";
  foreach (Button button in new[] { NavHome, NavGames, NavQueue, NavInventory, NavSettings, NavAccounts, NavCommands, NavLogs, NavAbout }) {
   if (button.Content is StackPanel content) content.Width = compact ? 134 : 170;
  }
 }
 private void GameArtwork_Failed(object sender, ExceptionRoutedEventArgs e) => ((Image)sender).Visibility = Visibility.Collapsed;
 private async void PauseGame_Click(object sender, RoutedEventArgs e) {
  if (((Button)sender).DataContext is not GameView game || !ready) return;
  await RunAsync(async () => { await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(game.BotName)}/Pause", new { Permanent = true }, true); await RefreshAsync(); }, $"Farming paused for {game.BotName}.");
 }
 private async void StartSelectedGame_Click(object sender, RoutedEventArgs e) {
  if (!ready || AllGames.SelectedItem is not GameView game) { StatusText.Text = "Select a game from the list first."; return; }
  BotView? bot = BotList.Items.Cast<BotView>().FirstOrDefault(b => b.Name == game.BotName);
  if (bot?.Online != true) { StatusText.Text = "Connect this account from Accounts and wait for Steam sign-in to finish."; return; }
  await RunAsync(async () => {
   await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(game.BotName)}/DesktopGame/{game.AppId}", post: true);
   farmingSelection.Set(game.BotName, game.AppId);
   await RefreshAsync();
  }, $"Farming requested for {game.Name} only. The engine is checking card eligibility.");
 }
 private async void ScanGames_Click(object sender, RoutedEventArgs e) {
  if (!ready) return;
  await RunAsync(async () => {
   foreach (BotView bot in BotList.Items.Cast<BotView>().Where(b => b.Online)) {
    await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(bot.Name)}/DesktopGames", post: true);
   }
   await RefreshAsync();
  }, "Scanning paused accounts. The list updates as scans finish.");
 }
 private async void StopSelectedGame_Click(object sender, RoutedEventArgs e) {
  if (AllGames.SelectedItem is not GameView game || !ready) { StatusText.Text = "Select a game from the list first."; return; }
  await RunAsync(async () => { await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(game.BotName)}/Pause", new { Permanent = true }, true); await RefreshAsync(); }, "Farming stopped; the Steam connection stays open.");
 }
 private async void AllPause_Click(object sender, RoutedEventArgs e) => await RunForAllAsync("Pause");
 private async void AllFarm_Click(object sender, RoutedEventArgs e) => await RunForAllAsync("Farm");
 private async void AllResume_Click(object sender, RoutedEventArgs e) => await RunForAllAsync("Resume");
 private async Task RunForAllAsync(string action) {
  if (!ready || BotList.Items.Count == 0) { StatusText.Text = "Add an account and wait for the engine connection."; return; }
  BotView[] accounts = BotList.Items.Cast<BotView>().ToArray();
  if (action is "Farm" or "Resume" && accounts.Any(b => !b.Online)) {
   StatusText.Text = "All accounts must be signed in for bulk farming. You can start individual online accounts from Games.";
   return;
  }
  await RunAsync(async () => {
   foreach (BotView bot in accounts) {
    string target = Uri.EscapeDataString(bot.Name);
    if (action is "Farm" or "Resume" && !bot.Running) await engine.RequestAsync($"Api/Bot/{target}/Start", post: true);
    if (action == "Farm" && bot.Online) {
     await engine.RequestAsync($"Api/Bot/{target}/DesktopGame/0", post: true);
     farmingSelection.Set(bot.Name, 0);
    }
    else if (action == "Pause") { if (bot.Running && bot.State != "Paused") await engine.RequestAsync($"Api/Bot/{target}/Pause", new { Permanent = true }, true); }
    else if (bot.State == "Paused") await engine.RequestAsync($"Api/Bot/{target}/DesktopGame/{farmingSelection.Get(bot.Name)}", post: true);
   }
   await RefreshAsync();
  });
 }
 private async void LoadInventory_Click(object sender, RoutedEventArgs e) {
  if (Selected is not { Online: true } bot || !ready) { StatusText.Text = "Select an online account from Accounts."; return; }
  await RunAsync(async () => {
   JsonNode? result = await engine.RequestAsync($"Api/Bot/{Uri.EscapeDataString(bot.Name)}/Inventory/753/6?language=english");
   JsonNode? inventory = result?[bot.Name];
   if (inventory?["Assets"] is not JsonArray assets || inventory?["Descriptions"] is not JsonArray descriptions) throw new InvalidOperationException("Could not load inventory. Check account connectivity and inventory access.");
   var names = descriptions.GroupBy(d => (d?["classid"]?.ToString() ?? "") + "/" + (d?["instanceid"]?.ToString() ?? "")).ToDictionary(g => g.Key, g => g.First()?["name"]?.ToString() ?? "Item");
   InventoryList.ItemsSource = assets.Select(a => new InventoryView(names.GetValueOrDefault((a?["classid"]?.ToString() ?? "") + "/" + (a?["instanceid"]?.ToString() ?? ""), "Steam item"), $"{a?["amount"]?.ToString() ?? "1"} items")).ToList();
  }, "Steam community inventory loaded.");
 }
 private void LoadDesignPreview() {
  ready = true;
  JsonObject previewBots = JsonNode.Parse("""
  {"Demo Account":{"IsConnectedAndLoggedOn":true,"KeepRunning":true,"Nickname":"Gamecu","RequiredInput":0,"CardsFarmer":{"NowFarming":true,"TimeRemaining":"08:30:00","CurrentGamesFarming":[{"AppID":730},{"AppID":570},{"AppID":440}],"GamesToFarm":[{"AppID":730,"GameName":"Counter-Strike 2","CardsRemaining":4,"HoursPlayed":182.5},{"AppID":570,"GameName":"Dota 2","CardsRemaining":3,"HoursPlayed":64},{"AppID":440,"GameName":"Team Fortress 2","CardsRemaining":2,"HoursPlayed":26},{"AppID":252490,"GameName":"Rust","CardsRemaining":4,"HoursPlayed":14},{"AppID":431960,"GameName":"Wallpaper Engine","CardsRemaining":3,"HoursPlayed":6},{"AppID":1091500,"GameName":"Cyberpunk 2077","CardsRemaining":5,"HoursPlayed":12},{"AppID":292030,"GameName":"The Witcher 3: Wild Hunt","CardsRemaining":4,"HoursPlayed":22},{"AppID":413150,"GameName":"Stardew Valley","CardsRemaining":3,"HoursPlayed":3}]}}}
  """)!.AsObject();
  dashboard.Update(previewBots);
  foreach (JsonNode? game in previewBots["Demo Account"]!["CardsFarmer"]!["GamesToFarm"]!.AsArray()) game!["CardsRemaining"] = game["CardsRemaining"]!.GetValue<int>() + 2;
  dashboard.Update(previewBots);
  foreach (JsonNode? game in previewBots["Demo Account"]!["CardsFarmer"]!["GamesToFarm"]!.AsArray()) game!["CardsRemaining"] = game["CardsRemaining"]!.GetValue<int>() - 2;
  dashboard.Update(previewBots);
  BotList.ItemsSource = previewBots.Select(p => BotView.FromJson(p.Key, p.Value)).ToArray();
  BotList.SelectedIndex = 0;
  AccountCount.Text = "1"; OnlineCount.Text = "1";
  RecentEvents.ItemsSource = new[] { new ActivityView("Card farming started", "Sample data"), new ActivityView("Mobile sign-in approved", "Sample data"), new ActivityView("Account connected to Steam", "Sample data") };
  UpdateDashboard();
 }
 private async void CommandValue_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await ExecuteCommandAsync(); } }
 private async Task ExecuteCommandAsync() {
  if (!ready) { StatusText.Text = "Engine connection is not ready."; return; }
  string command = CommandValue.Text.Trim();
  if (command.Length == 0) return;
  await RunAsync(async () => { JsonNode? result = await engine.RequestAsync("Api/Command", new { Command = command }, true); CommandOutput.AppendText((result?.ToString() ?? "Operation complete.") + "\n\n"); CommandValue.Clear(); CommandOutput.ScrollToEnd(); await RefreshAsync(); });
 }
 private void OpenConfig_Click(object sender, RoutedEventArgs e) => OpenFolder("config");
 private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder("logs");
 private void OpenFolder(string folder) { string path = Path.Combine(engine.DataPath, folder); Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
 private async Task StopEngineAsync() {
  ready = false; timer.Stop();
  await refreshGate.WaitAsync();
  try { await engine.StopAsync(); }
  finally { refreshGate.Release(); }
 }
 private async void Restart_Click(object sender, RoutedEventArgs e) => await RunAsync(async () => { await StopEngineAsync(); scannedAccounts.Clear(); await engine.StartAsync(); ready = true; timer.Start(); await RefreshAsync(); }, "Engine restarted. Your selection is saved; choose Resume to continue.");
 private async void OnClosing(object? sender, CancelEventArgs e) {
  if (closed) return;
  e.Cancel = true;
  if (closing) return;
  closing = true; timer.Stop(); IsEnabled = false; StatusText.Text = "Closing accounts and the engine…";
  await operations.WaitAsync();
  try { await StopEngineAsync(); }
  catch (Exception error) { Debug.WriteLine(error); }
  finally { engine.Dispose(); instance.Dispose(); closed = true; Close(); }
 }
}
