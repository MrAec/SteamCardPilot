// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Globalization;
using System.Windows;

namespace AutoPlaySteam;

public partial class App : Application {
 protected override void OnStartup(StartupEventArgs e) {
  CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
  CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
  base.OnStartup(e);
 }
}
