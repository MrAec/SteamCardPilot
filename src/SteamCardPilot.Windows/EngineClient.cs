// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SteamCardPilot;

internal sealed class EngineClient : IDisposable {
 internal string DataPath { get; }
 private readonly string engineDirectory;
 internal EngineClient(string? dataPath = null, string? enginePath = null) {
  DataPath = dataPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoPlaySteam");
  engineDirectory = enginePath ?? Path.Combine(AppContext.BaseDirectory, "engine");
 }
 internal event Action<string>? Log;
 private HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
 private Process? process;
 private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

 internal async Task StartAsync() {
  if (process is { HasExited: false }) { await RequestAsync("Api/ASF"); return; }
  string executable = Path.Combine(engineDirectory, "ArchiSteamFarm.exe");
  if (!File.Exists(executable)) throw new FileNotFoundException("Engine not found. Run the app from its complete distribution folder.");
  string configDirectory = Path.Combine(DataPath, "config");
  Directory.CreateDirectory(configDirectory);
  string configFile = Path.Combine(configDirectory, "ASF.json");
  JsonObject config = File.Exists(configFile) ? JsonNode.Parse(await File.ReadAllTextAsync(configFile))?.AsObject() ?? new() : new();
  string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
  var listener = new TcpListener(IPAddress.Loopback, 0);
  listener.Start();
  int port = ((IPEndPoint)listener.LocalEndpoint).Port;
  listener.Stop();
  config["CurrentCulture"] = "en-US";
  config["Headless"] = true;
  config["IPC"] = true;
  config["IPCPassword"] = secret;
  config["AutoRestart"] = false;
  config["UpdatePeriod"] = 0;
  config["UpdateChannel"] = 0;
  await File.WriteAllTextAsync(configFile, config.ToJsonString(JsonOptions));
  await File.WriteAllTextAsync(Path.Combine(configDirectory, "IPC.config"), JsonSerializer.Serialize(new { Kestrel = new { Endpoints = new { HTTP = new { Url = $"http://127.0.0.1:{port}" } } } }, JsonOptions));
  http.Dispose();
  http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = Timeout.InfiniteTimeSpan };
  http.DefaultRequestHeaders.Remove("Authentication");
  http.DefaultRequestHeaders.Add("Authentication", secret);
  var start = new ProcessStartInfo(executable) { WorkingDirectory = DataPath, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
  start.ArgumentList.Add("--path");
  start.ArgumentList.Add(DataPath);
  start.ArgumentList.Add("--no-restart");
  start.ArgumentList.Add("--desktop-mobile-confirmation");
  start.ArgumentList.Add("--no-steam-parental-generation");
  process?.Dispose();
  process = new Process { StartInfo = start };
  process.OutputDataReceived += (_, e) => { if (e.Data is not null) Log?.Invoke(e.Data); };
  process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Log?.Invoke(e.Data); };
  process.Start();
  process.BeginOutputReadLine();
  process.BeginErrorReadLine();
  for (int attempt = 0; attempt < 80; attempt++) {
   if (process.HasExited) throw new InvalidOperationException("The engine could not start. Check Activity for details.");
   try { await RequestAsync("Api/ASF"); return; }
   catch (HttpRequestException) { }
   catch (TaskCanceledException) { }
   await Task.Delay(500);
  }
  throw new TimeoutException("Engine connection timed out. Check Activity for details.");
 }

 internal async Task<JsonNode?> RequestAsync(string path, object? body = null, bool post = false) {
  using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, path);
  if (post) request.Content = JsonContent.Create(body ?? new { });
  using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(path.Contains("/DesktopGame/", StringComparison.Ordinal) ? 180 : path.Contains("/Inventory/", StringComparison.Ordinal) || path.EndsWith("/Pause", StringComparison.Ordinal) ? 60 : 12));
  using HttpResponseMessage response = await http.SendAsync(request, cancellation.Token);
  JsonNode? result;
  try { result = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation.Token)); }
  catch (JsonException) { throw new InvalidOperationException($"The engine returned an invalid response ({(int)response.StatusCode}). Check Activity for details."); }
  if (!response.IsSuccessStatusCode || result?["Success"]?.GetValue<bool>() != true) {
   throw new InvalidOperationException(result?["Message"]?.GetValue<string>() ?? $"Operation failed ({(int)response.StatusCode}).");
  }
  return result?["Result"]?.DeepClone();
 }

 internal async Task SaveParentalPinAsync(string name, string pin) {
  if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Equals("ASF", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid account name.");
  string file = Path.Combine(DataPath, "config", name + ".json");
  JsonObject config = JsonNode.Parse(await File.ReadAllTextAsync(file))?.AsObject() ?? throw new InvalidOperationException("Could not read account settings.");
  JsonObject updated = ParentalPinSettings.WithPin(config, pin);
  await RequestAsync("Api/Bot/" + Uri.EscapeDataString(name), new { BotConfig = updated }, true);
 }

 internal async Task WaitForBotAsync(string name) {
  for (int attempt = 0; attempt < 40; attempt++) {
   JsonNode? result = await RequestAsync("Api/Bot/ASF");
   if (result?[name] is not null) return;
   await Task.Delay(250);
  }
  throw new TimeoutException("The account has not loaded yet. Check Activity for details.");
 }

 internal async Task StopAsync() {
  if (process is not { HasExited: false }) return;
  try { await RequestAsync("Api/ASF/Exit", post: true); }
  catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException) { Log?.Invoke("Stopping engine…"); }
  using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
  try { await process.WaitForExitAsync(timeout.Token); }
  catch (OperationCanceledException) { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
 }

 public void Dispose() {
  if (process is { HasExited: false }) process.Kill(entireProcessTree: true);
  process?.Dispose();
  http.Dispose();
 }
}
