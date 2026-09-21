# agent.md — Data Sorting Game

Guide for AI agents (and humans) working on this repository.

## What this is
A standalone Windows desktop game for a security-awareness event. Players are shown "data type" cards (email address, credit card number, ...) and must sort each one into a classification box: **Public / Internal / Confidential / Restricted**. Score = correct answers (+ small streak/speed bonuses). Results are recorded per employee name and shown on a leaderboard.

It replaces a physical card-and-box game. It runs on **one shared kiosk PC**; results are stored locally (no network, no server).

## Hard rules
1. **No branding.** No company names, logos, product names or trademarks anywhere: code, UI, assets, file/exe metadata. The app title is the neutral "Data Sorting Game" (configurable via `eventTitle`).
2. **Fake data only.** Cards show data *types* and obviously fake examples (e.g. the `4111 1111 1111 1111` test card number). Never put real personal data, real credentials or real company facts in cards.
3. **Categories are not final.** Never hard-code category names, counts or colours in C#/XAML. They come from `categories.json` (2–6 categories supported). Card `categoryId` values must reference it.
4. **Config is editable without a rebuild.** `cards.json`, `categories.json`, `settings.json` live in a `Data` folder next to the exe. Missing files are re-created from defaults embedded in `DataSortingGame.Core`.
5. **Keep it a single self-contained exe** (no installer, no runtime prerequisite, no NuGet runtime dependencies unless there is a strong reason).

## Stack
- C# / .NET 10, WPF (`net10.0-windows`) for the UI; plain `net10.0` class library for logic.
- xUnit for tests. No other NuGet packages.
- Results: append-only JSON Lines file (`results.jsonl`) in `%LocalAppData%\DataSortingGame\`.

## Layout
```
agent.md
DataSortingGame.slnx
src/DataSortingGame.Core/        # UI-free logic (unit-tested)
  Models/                        # Category, Card, GameSettings, CardAnswer, PlayerResult
  Services/                      # ConfigLoader, AppPaths, GameEngine, GameSession, ResultStore,
                                 # Leaderboard, CsvExporter, ResultStats, PlayerName
  Defaults/                      # cards.json, categories.json, settings.json (embedded resources)
src/DataSortingGame/             # WPF app (views are UserControls swapped by MainWindow)
  MainWindow.xaml(.cs)           # navigation, kiosk mode, idle handling, saving finished rounds
  AppState.cs                    # loaded config + result store shared by all views; Ui brush helpers
  Views/                         # Start, Game, Result, Leaderboard, Admin, PinDialog
tests/DataSortingGame.Tests/     # xUnit
```

## Commands (run from repo root)
```
dotnet build
dotnet test
dotnet run --project src/DataSortingGame
dotnet publish src/DataSortingGame -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

## Conventions
- Game rules (drawing cards, scoring, ranking, attempts) belong in **Core** with unit tests, not in code-behind.
- Views stay thin: they render state from a `GameSession` and forward user input.
- Core must not reference WPF. Dates are stored in UTC and shown in local time.
- Anything typed by a player (name, department) is untrusted: it is written into CSV, so `CsvExporter` neutralises spreadsheet formulas. Keep that behaviour.
- WPF quirk: the app-wide `TextBlock` style in `App.xaml` sets a white foreground, which also reaches inside control templates. Controls on a light background (the admin `DataGrid`) need explicit dark text.
- Screens are laid out on a fixed 1280x720 canvas inside a `Viewbox`, so they scale to any display. Keep new views within it.
- The admin PIN in `settings.json` is a kiosk deterrent, not real security. Do not present it as such.
- Adding a card: add an entry to `Defaults/cards.json` (fields: `id`, `label`, `example`, `categoryId`, `difficulty` 1–3, `explanation`). The `DefaultConfigTests` will fail if it is malformed or references an unknown category.

## Definition of done for a change
`dotnet build` has 0 warnings-as-problems, `dotnet test` is green, and any UI change was actually run with `dotnet run`.
