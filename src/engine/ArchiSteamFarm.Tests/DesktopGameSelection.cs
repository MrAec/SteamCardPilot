using System.Linq;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using ArchiSteamFarm.Steam.Cards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArchiSteamFarm.Tests;

#pragma warning disable CA1812 // Instantiated by MSTest.
[TestClass]
internal sealed class DesktopGameSelection {
 [DataRow(0U, 42U, true)]
 [DataRow(42U, 42U, true)]
 [DataRow(42U, 43U, false)]
 [DataRow(43U, 42U, false)]
 [TestMethod]
 internal void SelectedGameExcludesOtherGames(uint selectedAppID, uint appID, bool expected) {
  Assert.AreEqual(expected, CardsFarmer.MatchesDesktopSelection(selectedAppID, appID));
 }
 [TestMethod]
 internal void SimultaneousSelectionHonoursSteamLimitWithoutTruncating() {
  Assert.IsTrue(CardsFarmer.IsValidDesktopPlaySelection([42]));
  Assert.IsTrue(CardsFarmer.IsValidDesktopPlaySelection(Enumerable.Range(1, 32).Select(id => (uint) id).ToArray()));
  Assert.IsFalse(CardsFarmer.IsValidDesktopPlaySelection([]));
  Assert.IsFalse(CardsFarmer.IsValidDesktopPlaySelection([0, 42]));
  Assert.IsFalse(CardsFarmer.IsValidDesktopPlaySelection([42, 42]));
  Assert.IsFalse(CardsFarmer.IsValidDesktopPlaySelection(Enumerable.Range(1, 33).Select(id => (uint) id).ToArray()));
 }
 [TestMethod]
 internal async Task OfflineStartsFailAndManualStopClearsStateWhilePaused() {
  using Steam.Bot bot = Bot.GenerateBot("DesktopPlayChecks");
  (bool success, _) = await bot.CardsFarmer.SelectDesktopGame(42).ConfigureAwait(false);
  Assert.IsFalse(success, "An offline single-game request must not be reported as started.");
  (success, _) = await bot.CardsFarmer.PlayDesktopGames([42, 43]).ConfigureAwait(false);
  Assert.IsFalse(success, "An offline simultaneous request must not be reported as started.");
  bot.CardsFarmer.SetInitialState(true);
  PropertyInfo selection = typeof(CardsFarmer).GetProperty(nameof(CardsFarmer.DesktopPlayingAppIDs))!;
  selection.SetValue(bot.CardsFarmer, ImmutableHashSet.Create(42U, 43U));
  (success, _) = await bot.Actions.Pause(true).ConfigureAwait(false);
  Assert.IsTrue(success, "Stop must be accepted even though automatic farming is already paused during manual play.");
  Assert.IsTrue(bot.CardsFarmer.DesktopPlayingAppIDs.IsEmpty);
  selection.SetValue(bot.CardsFarmer, ImmutableHashSet.Create(42U));
  bot.CardsFarmer.OnDisconnected();
  Assert.IsTrue(bot.CardsFarmer.DesktopPlayingAppIDs.IsEmpty, "Disconnected games must no longer be reported as playing.");
 }
}
#pragma warning restore CA1812
