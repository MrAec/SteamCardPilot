# Contributing

Small, focused fixes and interface improvements are welcome. Keep English text clear, preserve upstream copyright notices, and do not add personal account data.

1. Build `SteamCardPilot.slnx` with the .NET 10 SDK on Windows.
2. Run the engine tests and desktop checks described in the README.
3. Check your change at both the default window size and the minimum supported size.
4. Run `tools/check-share.ps1` before sharing.
5. Describe the problem, resulting behavior, and validation in your pull request.

Do not include real passwords, Steam Guard secrets, Family View PINs, session databases, or unredacted logs in examples or issues. Use synthetic accounts and preview mode.

Contributions are submitted under Apache-2.0. Engine changes should keep this fork's desktop behavior separate from ordinary upstream operation where practical.
