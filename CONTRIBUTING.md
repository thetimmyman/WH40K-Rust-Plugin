# Contributing to WH40K

Thanks for your interest in contributing! This is a single-file Oxide plugin for Rust, so contributions are pretty straightforward.

## How to contribute

1. **Fork** the repo and create a branch from `main`
2. Make your changes to `WH40K.cs`
3. Test on a local or dev Rust server (see Testing below)
4. Open a pull request with a clear description of what you changed and why

## What we're looking for

- Bug fixes and stability improvements
- New quest types and objectives (see Phase 4 in PLAN.md)
- New faction zones / monument keyword mappings
- Balance improvements (kill rewards, NPC health, respawn timers)
- Lore-accurate flavor text and announcements
- New factions from the backlog (Chaos Space Marines, Aeldari)
- Phase 2–5 features from PLAN.md

## Code style

- Match the existing style in `WH40K.cs` — indentation, naming, comment formatting
- Config-driven: new features should be configurable in JSON where possible, not hardcoded
- Keep it single-file — this is an Oxide plugin, not a multi-assembly project

## Testing

You need a Rust + Oxide dev server. The easiest path:

1. Install a local Rust dedicated server via SteamCMD
2. Install [Oxide/uMod](https://umod.org/games/rust)
3. Copy `WH40K.cs` to `oxide/plugins/`
4. Use `oxide.reload WH40K` to hot-reload after changes
5. Test with `wh40k.zones`, `wh40k.event`, `/wh40k quests`, etc.

For companion plugins (Economics, ServerRewards, XPerience), download from [uMod](https://umod.org/). The plugin gracefully skips reward calls if they're not installed.

## Reporting bugs

Open a GitHub issue with:
- Oxide version
- Rust server version (Devblog number)
- Relevant lines from `oxide/logs/oxide/` error log
- Steps to reproduce

## License

By contributing, you agree your contributions are licensed under GPL v3.
