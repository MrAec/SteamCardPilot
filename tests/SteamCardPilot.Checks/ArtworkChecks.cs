// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Net;
using System.Text;
using SteamCardPilot;

internal static class ArtworkChecks {
 internal static async Task RunAsync() {
  string directory = Path.Combine(Path.GetTempPath(), "SteamCardPilot-Artwork-" + Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);
  try {
   int requests = 0;
   var handler = new TestHandler(async request => {
    Interlocked.Increment(ref requests);
    await Task.Delay(15);
    string url = request.RequestUri!.AbsoluteUri;
    if (url.Contains("/api/appdetails", StringComparison.Ordinal)) return new(HttpStatusCode.OK) { Content = new StringContent("""
     {"3527290":{"success":true,"data":{"header_image":"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/3527290/hash/header.jpg"}}}
     """, Encoding.UTF8, "application/json") };
    if (url.Contains("/hash/", StringComparison.Ordinal)) return new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
    // A successful HTTP response can still contain an invalid image.
    return new(HttpStatusCode.OK) { Content = new ByteArrayContent([0]) };
   });
   using var http = new HttpClient(handler);
   static byte[] Decode(byte[] bytes) => bytes.Length > 0 && bytes[0] == 1 ? bytes : throw new FormatException("Invalid test image.");
   var cache = new GameArtworkCache<byte[]>(http, directory, Decode);
   byte[]?[] images = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => cache.GetAsync(3527290)));
   Require(images.All(image => ReferenceEquals(image, images[0])) && images[0]?.Length == 3 && requests == 3,
    "Rows must share one download and recover hashed Steam artwork after an invalid fixed-path image.");
   await cache.GetAsync(3527290);
   Require(requests == 3, "Scrolling must reuse the memory cache without requesting Steam again.");
   var reopened = new GameArtworkCache<byte[]>(http, directory, Decode);
   Require((await reopened.GetAsync(3527290))?.Length == 3 && requests == 3, "Reopening must reuse the disk cache.");
   await File.WriteAllBytesAsync(Path.Combine(directory, "3527290.img"), [0]);
   Require((await new GameArtworkCache<byte[]>(http, directory, Decode).GetAsync(3527290))?.Length == 3 && requests == 6,
    "Corrupt disk artwork must be replaced by a valid download.");

   int failedRequests = 0;
   using var offline = new HttpClient(new TestHandler(_ => {
    Interlocked.Increment(ref failedRequests);
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
   }));
   var failedCache = new GameArtworkCache<byte[]>(offline, directory, Decode);
   Require(await failedCache.GetAsync(99) is null, "Unavailable artwork must use the placeholder.");
   int beforeScroll = failedRequests;
   await failedCache.GetAsync(99);
   Require(failedRequests == beforeScroll && await failedCache.GetAsync(0) is null,
    "Missing artwork must not restart failed requests on every scroll.");
   Console.WriteLine("Artwork fallback, concurrent loading, memory/disk caching, corruption recovery, and failure cooldown checks passed.");
  } finally {
   string fullPath = Path.GetFullPath(directory);
   string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
   if (fullPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullPath).StartsWith("SteamCardPilot-Artwork-", StringComparison.Ordinal)) Directory.Delete(fullPath, recursive: true);
  }
 }
 private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
 private sealed class TestHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
 }
}
