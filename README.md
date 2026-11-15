# Brigade DPS

A party damage meter for The Spell Brigade (Unity IL2CPP, BepInEx 6).

[GitHub](https://github.com/slikfoul/BrigadeDps) · [Changelog](CHANGELOG.md)

## Features

- Total damage, DPS and damage share for each player and spell, with native spell names and icons.
- Russian or English UI based on the game language, using game fonts. Other languages use English UI text.
- Full spell names, word initials or icons only, configurable in F5 settings.
- A separate boss section showing damage, DPS and each player's share of party damage to the boss. Damage to other enemies is excluded from this section.
- Native dark card artwork with subdued ornamental lines and curls. The top mirrors the bottom, without an emblem. A continuous outer frame and background surrounds both sections, with an ornamental divider between boss and party statistics.
- A compact table that automatically fits the visible text and rows, with small column gaps and centered headings.
- Move the meter by dragging its top row. Choose horizontal growth (right/left) and vertical growth (down/up) in settings. Moving the meter preserves these directions, and the frame stays within screen bounds.
- Move the settings window by dragging its title. Window positions persist between launches.
- Unassigned damage remains in player totals without a separate row.
- In-game settings for visibility, spell icons and names, bosses, results, solo play, position locking, scale, opacity, height limit, spell count, DPS mode and interval, and shortcuts.
- The meter appears during an active run and on the results screen; it stays hidden in the main menu and lobby.

Game artwork is loaded at runtime and is not bundled with the mod. A fallback frame appears until the native assets are available.

## Controls

| Action | Default |
| --- | --- |
| Toggle meter | F3 |
| Toggle spell rows | F4 |
| Open settings | F5 |
| Move meter | Drag the table's top row |
| Move settings | Drag the settings title |
| Release the game cursor | Hold Alt |

Enable **Abbreviate spell names** to display initials, for example `Wings of Dawn` becomes `WOD`. Disable **Show spell names** for icons only. If an icon is unavailable, initials keep the spell identifiable.

## DPS modes

In F5, **Player and spell DPS** switches between two modes independently for each viewer:

- **Rolling interval** (default): damage over the last N seconds divided by N. During the first N seconds of a run, the elapsed run time is used instead. The host sets the interval from 1 to 60 seconds; the default is 10.
- **Game value**: the game's own DPS for each spell, calculated as total damage since the spell was equipped divided by elapsed game time since equipping it. Each spell has its own averaging period. The player row sums the native DPS of all equipped spells, including those hidden by the row limit.

For example, if a spell dealt 6,000 damage over 60 seconds since it was equipped, including 2,000 damage in the last 10 seconds, its game DPS is **100** and its rolling DPS is **200**.

Game values require a host running version 0.1.10 or newer. Unavailable values appear as a dash. Damage totals and percentages continue to use the mod's damage records in both modes. Spell percentages are relative to their player's total damage.

## Boss statistics

Boss DPS uses the host's rolling interval in both display modes and freezes when the encounter ends. Boss percentages show each player's share of the party's damage to the boss.

After a boss dies, its statistics and the divider remain visible for **10 seconds** by default. In F5, **Boss results after defeat (s)** accepts 0–3600 seconds: enter a number and press Enter, or use the +/- buttons. Set it to 0 to hide defeated boss statistics immediately.

This setting is individual for each viewer. The countdown uses real time, including pauses and the results screen; hiding the meter does not restart it.

## Multiplayer

The **host** needs Brigade DPS to collect statistics. Players who want to see the meter should install the same version. Other players can join without the mod.

## Installation

Import `slikfoul-BrigadeDps-1.0.0.zip` using **Import local mod** in r2modman, enable BrigadeDps and launch with **Start modded**. BepInExPack IL2CPP 6.0.788 is the required dependency.

For manual installation into a profile with BepInExPack IL2CPP, place `BrigadeDps.dll` in `BepInEx/plugins/slikfoul-BrigadeDps/`. Close the game before updating.

Configuration file: `BepInEx/config/slikfoul.BrigadeDps.cfg`.

## Building and checks

```powershell
dotnet build src/BrigadeDps.csproj -c Release
dotnet run --project tests/BrigadeDps.Tests.csproj -c Release
pwsh -File ./package.ps1
```

Building requires .NET SDK 9 and an installed copy of the game with BepInExPack IL2CPP. Launch the game with BepInEx once to generate its `interop` assemblies. By default, references come from the r2modman profile at `TheSpellBrigade/profiles/Default/BepInEx`. Override this with `-p:BepInExDir="..."`. The mod targets .NET 6; the standalone checks target .NET 9 and do not require the game.

Source code is in `src`, standalone checks in `tests`, and package metadata and the icon in `packaging`. The packaging script creates the r2modman ZIP in `dist`. Game assemblies, extracted artwork and local research files are not included in the repository or package.

Version 1.0.0 builds without warnings and passes 124 standalone checks covering statistics, network data, localization, window positioning, boss-result retention and DPS mode selection. These checks do not replace in-game UI and multiplayer testing.
