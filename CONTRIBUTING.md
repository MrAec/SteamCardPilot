# Contributing

Everyone is welcome to contribute through issues, discussions, and pull requests. Small, focused fixes and interface improvements are welcome. Keep English text clear, preserve upstream copyright notices, and do not add personal account data. You do not need collaborator access to submit a contribution.

## Submit a change

1. [Fork SteamCardPilot](https://github.com/MrAec/SteamCardPilot/fork) to your GitHub account and clone your fork.
2. Create a branch for your change, for example `git switch -c fix/account-status`.
3. Make and validate your change using the checks below.
4. Commit your changes and push your branch to your fork.
5. [Open a pull request](https://github.com/MrAec/SteamCardPilot/compare) from your fork's branch to `MrAec/SteamCardPilot:main`.
6. Respond to review feedback. A maintainer reviews and merges accepted changes.

For a substantial feature, first discuss the proposal in an issue or [GitHub Discussions](https://github.com/MrAec/SteamCardPilot/discussions). Bug reports should include reproducible steps, the app version, and expected and actual behavior.

## Validate a change

1. Build `SteamCardPilot.slnx` with the .NET 10 SDK on Windows.
2. Run the engine tests and desktop checks described in the README.
3. Check your change at both the default window size and the minimum supported size.
4. Run `tools/check-share.ps1` before sharing.
5. Describe the problem, resulting behavior, and validation in your pull request.

Do not include real passwords, Steam Guard secrets, Family View PINs, session databases, or unredacted logs in examples or issues. Use synthetic accounts and preview mode.

Contributions are submitted under Apache-2.0. Engine changes should keep this fork's desktop behavior separate from ordinary upstream operation where practical.

## Source layout

- `src/SteamCardPilot.Windows`: WPF desktop application and `SteamCardPilot` namespace.
- `tests/SteamCardPilot.Checks`: desktop and local engine integration checks.
- `src/engine`: ArchiSteamFarm engine, optional plugins, and engine tests. These names identify the upstream component and preserve plugin compatibility.
- `tools`: packaging, icon creation, and sharing checks.

The legacy local data directory and single-instance mutex keep their `AutoPlaySteam` names so existing installations can share the same account storage safely. Do not rename them without a migration plan.
