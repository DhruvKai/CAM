# CAM

A standalone Windows game for security-awareness events. Players sort "data type" cards
(email address, credit card number, ...) into classification boxes and see why each answer is right,
or play a multiple-choice quiz on data handling and classification. Results are recorded per player
and ranked on a leaderboard. Fully offline.

## Set up the kiosk PC
1. Copy `CAM.exe` to the kiosk PC (any folder you can write to, e.g. `C:\SortingGame`). Nothing needs installing.
2. Double-click it. On first start it creates a `Data` folder next to the exe with three settings files.
3. **Change the admin PIN** in `Data\settings.json` (default `1234`), then restart the game.

The game opens full screen (kiosk mode). Alt+F4 is blocked; use **Admin > Exit app** to close it.
Set `"kioskMode": false` in `settings.json` for a normal window while you are testing.

## Running the event
- Players type their name (and optionally a department) and press **Play**.
- Each name gets `maxOfficialAttempts` ranked rounds (default 1). Further rounds are marked **Practice** and never reach the leaderboard.
- When nobody touches the kiosk for a while, it shows the leaderboard and pages through it to attract players.
- Admin button (top right of the start screen, PIN protected): view all rounds, **Export CSV** (opens in Excel),
  delete individual rounds, clear everything, reload settings, and see which cards people miss most.
  Use the "most missed cards" list to choose topics for follow-up reminders.

## Changing categories, cards and rules (no rebuild needed)
Edit the files in the `Data` folder, then use **Admin > Reload settings** (or restart the game).

| File | What it controls |
|---|---|
| `categories.json` | The boxes: `id`, `name`, `color`. Between 2 and 6 categories. |
| `cards.json` | The card pool: `id`, `label`, `example`, `categoryId`, `difficulty` (1 easy to 3 tricky), `explanation`. |
| `settings.json` | Cards per round, scoring, time limit per card, attempts, difficulty mix, sounds, idle time, admin PIN. |

- Every card's `categoryId` must match a category `id`. Bad entries are skipped and listed under **Settings warnings** in Admin.
- Players get a random selection from the pool, so keep at least 2-3 times `cardsPerRound` cards.
- Use obviously fake example values only.
- Delete a settings file to restore its default.

## Where results are stored
`%LocalAppData%\DataSortingGame\results.jsonl` on the kiosk PC (quiz rounds: `quiz-results.jsonl` in the same
folder). The folder is still named `DataSortingGame` internally so results from before the app was renamed to
CAM keep working. Back it up with **Export CSV** before clearing.
Reset between rehearsal and the real event with **Admin > Clear all results**.

## Build from source
Requires the .NET 10 SDK.
```
dotnet test
dotnet run --project src/DataSortingGame
dotnet publish src/DataSortingGame -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
# produces publish/CAM.exe
```
See `agent.md` for the project layout and rules.
