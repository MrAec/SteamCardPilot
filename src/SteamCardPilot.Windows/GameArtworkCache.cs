// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace SteamCardPilot;

// Shared by all rows and accounts: row recycling must not restart an image download.
internal sealed class GameArtworkCache<T> where T : class {
 private sealed record Entry(DateTime Created, Lazy<Task<T?>> Load);
 private readonly ConcurrentDictionary<uint, Entry> entries = new();
 private readonly SemaphoreSlim downloads = new(4);
 private readonly HttpClient http;
 private readonly string directory;
 private readonly Func<byte[], T> decode;

 internal GameArtworkCache(HttpClient http, string directory, Func<byte[], T> decode) {
  this.http = http;
  this.directory = Path.GetFullPath(directory);
  this.decode = decode;
 }

 internal Task<T?> GetAsync(uint appId) {
  if (appId == 0) return Task.FromResult<T?>(null);
  while (true) {
   Entry entry = entries.GetOrAdd(appId, id => new(DateTime.UtcNow, new(() => LoadAsync(id))));
   Task<T?> task = entry.Load.Value;
   // Failed requests get a cooldown, rather than being retried on every scroll.
   if (task.IsCompletedSuccessfully && task.Result is null && DateTime.UtcNow - entry.Created > TimeSpan.FromMinutes(2)) {
    entries.TryRemove(new KeyValuePair<uint, Entry>(appId, entry));
    continue;
   }
   return task;
  }
 }

 private async Task<T?> LoadAsync(uint appId) {
  await downloads.WaitAsync().ConfigureAwait(false);
  try {
   string file = Path.Combine(directory, appId + ".img");
   try {
    if (File.Exists(file)) return decode(await File.ReadAllBytesAsync(file).ConfigureAwait(false));
   } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or FormatException or NotSupportedException or InvalidOperationException) { }

   // Older games still use this address. Newer games can have a hashed asset path.
   T? image = await DownloadAsync($"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/header.jpg", file).ConfigureAwait(false);
   if (image is not null) return image;
   try {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
    string json = await http.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appId}&l=english&filters=basic", timeout.Token).ConfigureAwait(false);
    JsonNode? data = JsonNode.Parse(json)?[appId.ToString()]?["data"];
    foreach (string field in new[] { "header_image", "capsule_image" }) {
     string? url = data?[field]?.GetValue<string>();
     if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" || !uri.Host.EndsWith(".steamstatic.com", StringComparison.OrdinalIgnoreCase)) continue;
     image = await DownloadAsync(uri.AbsoluteUri, file).ConfigureAwait(false);
     if (image is not null) return image;
    }
   } catch (Exception e) when (e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException) { }
   return await DownloadAsync($"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg", file).ConfigureAwait(false);
  } finally { downloads.Release(); }
 }

 private async Task<T?> DownloadAsync(string url, string file) {
  T image;
  byte[] bytes;
  try {
   using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
   bytes = await http.GetByteArrayAsync(url, timeout.Token).ConfigureAwait(false);
   image = decode(bytes);
  } catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException or ArgumentException or FormatException or NotSupportedException or InvalidOperationException) { return null; }
  // A read-only/full disk must not prevent displaying an image already downloaded.
  string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
  try {
   Directory.CreateDirectory(directory);
   await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
   File.Move(temporary, file, true);
  } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
  finally {
   try { if (File.Exists(temporary)) File.Delete(temporary); }
   catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
  }
  return image;
 }
}
