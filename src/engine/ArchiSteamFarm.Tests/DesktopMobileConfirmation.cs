using ArchiSteamFarm.Steam.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArchiSteamFarm.Tests;

#pragma warning disable CA1812 // The class is instantiated by MSTest.
[TestClass]
internal sealed class DesktopMobileConfirmation {
 [DataRow(true, false, false, true)]
 [DataRow(true, true, false, false)]
 [DataRow(true, false, true, false)]
 [DataRow(true, true, true, false)]
 [DataRow(false, false, false, false)]
 [DataRow(false, true, false, false)]
 [DataRow(false, false, true, false)]
 [DataRow(false, true, true, false)]
 [TestMethod]
 internal void MobileApprovalIsUsedOnlyForDesktopWithoutAvailableCodes(bool desktop, bool authenticator, bool codeReady, bool expected) {
  Assert.AreEqual(expected, BotCredentialsProvider.ShouldUseDesktopMobileConfirmation(desktop, authenticator, codeReady));
 }
}
#pragma warning restore CA1812
