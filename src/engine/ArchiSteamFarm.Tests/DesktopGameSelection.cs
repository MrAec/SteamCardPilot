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
}
#pragma warning restore CA1812
