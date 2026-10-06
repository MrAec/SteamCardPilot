// Copyright © 2026 Mr_Aec. License: LICENSE-Mr_Aec.txt.
using System.Windows;
using System.Windows.Input;

namespace AutoPlaySteam;

public partial class ParentalPinWindow : Window {
 internal string Pin { get; private set; } = "";
 public ParentalPinWindow(string accountName) {
  InitializeComponent();
  AccountLabel.Text = accountName;
  Loaded += (_, _) => PinValue.Focus();
 }
 private void Save_Click(object sender, RoutedEventArgs e) => Save();
 private void PinValue_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; Save(); } }
 private void Save() {
  if (!ParentalPinSettings.IsValid(PinValue.Password)) { ErrorText.Text = "The PIN must contain exactly four digits."; return; }
  Pin = PinValue.Password;
  PinValue.Clear();
  DialogResult = true;
 }
}
