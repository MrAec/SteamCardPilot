// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
  app.Shutdown();
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
