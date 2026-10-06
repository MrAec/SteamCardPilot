// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SteamCardPilot;

public sealed class GameArtworkImage : Image {
 private static readonly GameArtworkCache<BitmapSource> Cache = new(new HttpClient(),
  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoPlaySteam", "artwork"), Decode);
 public static readonly DependencyProperty AppIdProperty = DependencyProperty.Register(nameof(AppId), typeof(uint), typeof(GameArtworkImage),
  new PropertyMetadata(0u, OnAppIdChanged));
 public uint AppId { get => (uint)GetValue(AppIdProperty); set => SetValue(AppIdProperty, value); }
 private int requestVersion;

 private static BitmapSource Decode(byte[] bytes) {
  using var stream = new MemoryStream(bytes, writable: false);
  var bitmap = new BitmapImage();
  bitmap.BeginInit();
  bitmap.CacheOption = BitmapCacheOption.OnLoad;
  bitmap.DecodePixelWidth = 234; // Covers the small thumbnail on high-DPI displays.
  bitmap.StreamSource = stream;
  bitmap.EndInit();
  bitmap.Freeze();
  return bitmap;
 }

 private static async void OnAppIdChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
  var image = (GameArtworkImage)sender;
  int version = ++image.requestVersion;
  image.Source = null;
  Task<BitmapSource?> load = Cache.GetAsync((uint)args.NewValue);
  if (load.IsCompletedSuccessfully) { image.Source = load.Result; return; }
  BitmapSource? source = await load.ConfigureAwait(false);
  // Bindings can start downloads before a dispatcher synchronization context exists.
  // An old download must not paint over a row recycled for another game.
  try {
   await image.Dispatcher.InvokeAsync(() => { if (version == image.requestVersion) image.Source = source; });
  } catch (OperationCanceledException) { } // The desktop may close during a download.
 }
}
