// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using SteamCardPilot;

internal static class Program {
 [STAThread]
 private static void Main() {
  var app = new App();
  app.InitializeComponent();
  // Apply real application templates without opening a window or loading accounts.
  var button = new Button { Content = "Start Selected Game", Style = (Style)app.FindResource(typeof(Button)) };
  button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
  button.Arrange(new Rect(button.DesiredSize));
  button.ApplyTemplate();
  var chrome = (Border)button.Template.FindName("Chrome", button);
  var ring = (Border)button.Template.FindName("FocusRing", button);
  Require(chrome.BorderThickness == new Thickness(0) && !ring.IsHitTestVisible, "Keyboard focus must use an overlay, without resizing the button content or intercepting clicks.");
  Size before = button.DesiredSize;
  ring.Visibility = Visibility.Visible;
  button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
  Require(button.DesiredSize == before, "Showing the focus ring must not change button layout.");
  Require(button.Template.Triggers.OfType<Trigger>().Any(t => t.Property == UIElement.IsKeyboardFocusedProperty && t.Setters.OfType<Setter>().Any(s => s.TargetName == "FocusRing" && s.Value.Equals(Visibility.Visible))), "Keyboard focus must actually activate the visible ring.");

  var tooltip = new ToolTip { Content = "A long game title", Style = (Style)app.FindResource(typeof(ToolTip)) };
  Require(Contrast(((SolidColorBrush)tooltip.Foreground).Color, ((SolidColorBrush)tooltip.Background).Color) >= 4.5, "Dark tooltips must keep readable text contrast.");
  Require(((SolidColorBrush)tooltip.Background).Color == Color.FromRgb(0x16, 0x26, 0x38) && tooltip.Template is not null, "Tooltips must use the application's dark template rather than the system light theme.");
  var pin = new ParentalPinWindow("Synthetic Account");
  Button save = Children(pin).OfType<Button>().Single(b => Equals(b.Content, "Save PIN"));
  Require(Contrast(((SolidColorBrush)save.Foreground).Color, ((SolidColorBrush)save.Background).Color) >= 4.5, "The PIN action must have readable text on its blue background.");
  Console.WriteLine("Dark tooltip, keyboard focus layout, and PIN button contrast checks passed.");
  if (Environment.GetCommandLineArgs().Contains("--preview")) { CheckAccountSubmission(); CheckGameActions(); }
  app.Shutdown();
 }
 private static void CheckAccountSubmission() {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
  var window = new MainWindow();
  var form = (ScrollViewer)window.FindName("AccountsPane");
  var manage = (Grid)window.FindName("ManagePane");
  var create = (Button)window.FindName("AddAccountButton");
  var name = (TextBox)window.FindName("NewName");
  var login = (TextBox)window.FindName("NewLogin");
  var password = (PasswordBox)window.FindName("NewPassword");
  var status = (TextBlock)window.FindName("AccountStatus");
  var flags = BindingFlags.Instance | BindingFlags.NonPublic;
  object engine = typeof(MainWindow).GetField("engine", flags)!.GetValue(window)!;
  using var handler = new AccountEngineStub();
  using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1/") };
  ((HttpClient)engine.GetType().GetField("http", flags)!.GetValue(engine)!).Dispose();
  engine.GetType().GetField("http", flags)!.SetValue(engine, client);
  name.Text = "../invalid";
  create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Require(status.Text.Contains("1–64"), "Invalid names must show validation inside the account form.");
  name.Text = "UiCheck_" + Guid.NewGuid().ToString("N");
  login.Text = "synthetic-login";
  password.Password = "synthetic-password";
  create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Require(status.Text.Contains("not connected") && password.Password == "synthetic-password" && create.IsEnabled,
   "Engine startup failures must be visible and preserve entered details for retry.");
  typeof(MainWindow).GetField("ready", flags)!.SetValue(window, true);
  var gate = (SemaphoreSlim)typeof(MainWindow).GetField("operations", flags)!.GetValue(window)!;
  gate.Wait();
  form.Visibility = Visibility.Visible;
  handler.Name = name.Text;
  create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Require(!create.IsEnabled && status.Text.Contains("Waiting") && handler.Created == 0,
   "Submitting during another operation must show pending progress instead of discarding the click.");
  create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  gate.Release();
  PumpUntil(() => create.IsEnabled);
  Require(handler.Created == 1 && handler.PasswordReceived && handler.Started,
   $"A pending submission must create exactly one account, send its password, and start it after the previous operation finishes. Created={handler.Created}, password={handler.PasswordReceived}, started={handler.Started}, status={status.Text}");
  Require(manage.Visibility == Visibility.Visible && form.Visibility == Visibility.Collapsed && name.Text.Length == 0 && password.Password.Length == 0,
   "Successful creation must clear sign-in fields and open Accounts.");
  form.Visibility = Visibility.Visible;
  name.Text = handler.Name + "_retry";
  login.Text = "synthetic-login";
  password.Password = "synthetic-password";
  handler.Reject = true;
  create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  PumpUntil(() => create.IsEnabled);
  Require(status.Text.Contains("Synthetic API failure") && password.Password.Length > 0 && form.Visibility == Visibility.Visible,
   "API errors must stay visible beside the create button and keep input available for retry.");
  Console.WriteLine("Account submit validation, startup failure, queued clicks, duplicate prevention, sign-in handoff, and API failure checks passed.");
 }
 private static void PumpUntil(Func<bool> done) {
  var frame = new DispatcherFrame();
  var timeout = DateTime.UtcNow.AddSeconds(10);
  var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
  timer.Tick += (_, _) => { if (done() || DateTime.UtcNow >= timeout) frame.Continue = false; };
  timer.Start();
  Dispatcher.PushFrame(frame);
  timer.Stop();
  Require(done(), "Account submission did not complete in time.");
 }
 private static void CheckGameActions() {
  var window = new MainWindow();
  var flags = BindingFlags.Instance | BindingFlags.NonPublic;
  object engine = typeof(MainWindow).GetField("engine", flags)!.GetValue(window)!;
  using var handler = new GameEngineStub();
  using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1/") };
  ((HttpClient)engine.GetType().GetField("http", flags)!.GetValue(engine)!).Dispose();
  engine.GetType().GetField("http", flags)!.SetValue(engine, client);
  typeof(MainWindow).GetField("ready", flags)!.SetValue(window, true);
  var grid = (DataGrid)window.FindName("AllGames");
  var status = (TextBlock)window.FindName("StatusText");
  void Refresh() { Task task = (Task)typeof(MainWindow).GetMethod("RefreshAsync", flags)!.Invoke(window, null)!; PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
  static string Bot(object game) => (string)game.GetType().GetProperty("BotName")!.GetValue(game)!;
  static uint Id(object game) => (uint)game.GetType().GetProperty("AppId")!.GetValue(game)!;
  void Select(params uint[] ids) {
   grid.SelectedItems.Clear();
   foreach (object game in grid.Items.Cast<object>().Where(game => Bot(game) == "Online" && ids.Contains(Id(game)))) grid.SelectedItems.Add(game);
  }
  void Click(string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Refresh();
  Select(42, 43);
  Refresh();
  Require(grid.SelectedItems.Count == 2, "Polling must preserve Ctrl/Shift multi-game selection.");
  Select(42);
  var gate = (SemaphoreSlim)typeof(MainWindow).GetField("operations", flags)!.GetValue(window)!;
  gate.Wait();
  Click("StartSingleGame");
  Require(handler.FarmRequests.Count == 0 && status.Text.Contains("Waiting"), "Game start clicks must wait for another operation rather than disappear.");
  gate.Release();
  PumpUntil(() => status.Text.StartsWith("Card farming started"));
  object saved = typeof(MainWindow).GetField("farmingSelection", flags)!.GetValue(window)!;
  uint SavedGame() => (uint)saved.GetType().GetMethod("Get", flags)!.Invoke(saved, ["Online"])!;
  Require(SavedGame() == 42, "Only successful farming must save the selected game.");
  Select(43);
  handler.FailOnline = true;
  Click("StartSingleGame");
  PumpUntil(() => status.Text.Contains("Synthetic Steam play blocked"));
  Require(SavedGame() == 42, "A rejected start must show its reason without overwriting the last successful selection.");
  handler.FailOnline = false;
  Click("StartAllGames");
  PumpUntil(() => status.Text.StartsWith("Farm:") && status.Text.Contains("1 failed"));
  Require(status.Text.Contains("2 account(s) completed") && status.Text.Contains("1 offline skipped") && handler.FarmRequests.Any(request => request == "Healthy/0"),
   "One failing or offline account must not block other accounts in Farm All.");
  Select(42, 43);
  Click("PlaySelectedGames");
  PumpUntil(() => status.Text.StartsWith("Play Together:"));
  Require(handler.ManualRequests.Last().SequenceEqual(new uint[] { 42, 43 }) && grid.SelectedItems.Count == 2,
   "Multi-select must send both games and keep the selection after refresh.");
  Require(grid.SelectedItems.Cast<object>().All(game => (string)game.GetType().GetProperty("State")!.GetValue(game)! == "●  Playing together"),
   "Manual games must be displayed as playing together rather than paused or farming cards.");
  Click("StopGames");
  PumpUntil(() => status.Text.StartsWith("Stop:"));
  Require(handler.Pauses == 1 && handler.Bots["Online"]!["CardsFarmer"]!["DesktopPlayingAppIDs"]!.AsArray().Count == 0, "Stop must clear manual play even when the farmer is paused.");
  Button resume = Children(window).OfType<Button>().Single(button => button.Content is string text && text.Contains("Resume Saved Selection"));
  resume.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  PumpUntil(() => status.Text.StartsWith("Resume:"));
  Require(handler.ManualRequests.Count == 2 && handler.ManualRequests.Last().SequenceEqual(new uint[] { 42, 43 }), "Resume must restore the saved simultaneous mode.");
  int before = handler.ManualRequests.Count;
  handler.Bots["Online"]!["CardsFarmer"]!["GamesToFarm"] = new JsonArray(Enumerable.Range(1, 33).Select(id => (JsonNode)new JsonObject { ["AppID"] = id, ["CardsRemaining"] = 1 }).ToArray());
  Refresh();
  Click("PlayAllGames");
  Require(status.Text.Contains("up to 32") && handler.ManualRequests.Count == before, "An oversized Play All selection must reject the whole operation before changing accounts.");
  Console.WriteLine("Game start queuing, Steam errors, partial bulk success, multi-selection polling, simultaneous play, stop/resume, and Steam limit checks passed.");
 }
 private sealed class GameEngineStub : HttpMessageHandler {
  internal readonly JsonObject Bots = JsonNode.Parse("""
   {"Online":{"KeepRunning":true,"IsConnectedAndLoggedOn":true,"CardsFarmer":{"Paused":true,"GamesToFarm":[{"AppID":42,"GameName":"One","CardsRemaining":2},{"AppID":43,"GameName":"Two","CardsRemaining":3}]}},
    "Healthy":{"KeepRunning":true,"IsConnectedAndLoggedOn":true,"CardsFarmer":{"Paused":true,"GamesToFarm":[{"AppID":44,"CardsRemaining":2}]}},
    "Failed":{"KeepRunning":true,"IsConnectedAndLoggedOn":true,"CardsFarmer":{"Paused":true,"GamesToFarm":[{"AppID":45,"CardsRemaining":2}]}},
    "Offline":{"KeepRunning":false,"IsConnectedAndLoggedOn":false,"CardsFarmer":{"Paused":true,"GamesToFarm":[{"AppID":46,"CardsRemaining":2}]}}}
   """)!.AsObject();
  internal readonly List<string> FarmRequests = [];
  internal readonly List<uint[]> ManualRequests = [];
  internal bool FailOnline;
  internal int Pauses;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
   string[] parts = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
   if (request.Method == HttpMethod.Get) return Response(true, Bots);
   string name = Uri.UnescapeDataString(parts[2]);
   JsonObject farmer = Bots[name]!["CardsFarmer"]!.AsObject();
   if (parts[3] == "DesktopGame") {
    FarmRequests.Add(name + "/" + parts[4]);
    if (name == "Failed" || FailOnline && name == "Online") return Response(false, message: "Synthetic Steam play blocked");
    farmer["NowFarming"] = true; farmer["Paused"] = false; farmer["DesktopPlayingAppIDs"] = new JsonArray();
   } else if (parts[3] == "DesktopPlay") {
    uint[] ids = System.Text.Json.JsonSerializer.Deserialize<uint[]>(await request.Content!.ReadAsStringAsync(cancellationToken))!;
    ManualRequests.Add(ids);
    farmer["DesktopPlayingAppIDs"] = new JsonArray(ids.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
    farmer["NowFarming"] = false; farmer["Paused"] = true;
   } else if (parts[3] == "Pause") {
    Pauses++; farmer["DesktopPlayingAppIDs"] = new JsonArray(); farmer["NowFarming"] = false; farmer["Paused"] = true;
   }
   return Response(true);
  }
  private static HttpResponseMessage Response(bool success, JsonNode? result = null, string? message = null) => new(HttpStatusCode.OK) {
   Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { Success = success, Result = result, Message = message }), Encoding.UTF8, "application/json")
  };
 }
 private sealed class AccountEngineStub : HttpMessageHandler {
  internal string Name = "";
  internal int Created;
  internal bool PasswordReceived, Started, Reject;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
   string path = request.RequestUri!.AbsolutePath;
   JsonNode? body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
   if (Reject) return Response(new { Success = false, Message = "Synthetic API failure" });
   if (request.Method == HttpMethod.Post && path == "/Api/Bot/" + Name) {
    Created++;
    Require(body?["botConfig"]?["SteamPassword"] is null, "Account files must not receive plaintext sign-in passwords.");
   }
   if (path.EndsWith("/Input")) PasswordReceived = body?["type"]?.GetValue<int>() == 2 && body?["value"]?.GetValue<string>() == "synthetic-password";
   if (path.EndsWith("/Start")) { Require(PasswordReceived, "Password input must precede starting the account."); Started = true; }
   return Response(new { Success = true, Result = new Dictionary<string, object> { [Name] = new { KeepRunning = Started, RequiredInput = 0 } } });
  }
  private static HttpResponseMessage Response(object body) => new(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
 }
 private static IEnumerable<DependencyObject> Children(DependencyObject parent) {
  foreach (object child in LogicalTreeHelper.GetChildren(parent)) {
   if (child is not DependencyObject dependency) continue;
   yield return dependency;
   foreach (DependencyObject descendant in Children(dependency)) yield return descendant;
  }
 }
 private static double Contrast(Color a, Color b) {
  static double Channel(byte value) { double c = value / 255d; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
  static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
  double x = Luminance(a), y = Luminance(b);
  return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
 }
 private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
