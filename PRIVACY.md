# Keep accounts out of the repository

Runtime data belongs in `%LOCALAPPDATA%\AutoPlaySteam`, outside this source tree.
It must never be copied into a Git commit, source ZIP, issue, or release package.

Before publishing:

- Run `./tools/check-share.ps1` and review `git status`.
- Exclude all `config`, `logs`, `debug`, and runtime cache directories.
- Exclude `.db`, `.db.*`, `.maFile`, `.keys`, private signing keys, token files, `.env` files, and user configuration JSON.
- Do not share `games.json`, `selection.json`, `ASF.json`, or `IPC.config`.
- Use preview mode for screenshots. Its account data is synthetic and it does not load your Steam configuration.
- Build release binaries from this source using `tools/publish-windows.ps1`. Never zip the live runtime data directory.

The ignore rules and sharing check are safeguards, not a substitute for reviewing files. Public dependency references, the upstream public signing key (`.snk.pub`), and fake test credentials are intentionally part of the source.

If credentials were already published, removing a file in a later commit does not remove the old copy. Revoke affected sessions or tokens, then remove the exposed material from repository history before republishing.
