# CAM

A standalone Windows app for security-awareness events with two games:

- **Label Legends**: players read scenario cards ("A completed background-screening report for a job applicant.")
  and sort each into Public / Internal / Confidential / Highly Restricted, then see why.
- **Cyber Trivia**: a timed multiple-choice quiz on data handling and classification.

Results are recorded per player and ranked on a leaderboard per game. Fully offline.

**Both games ship empty.** Add your own cards and questions before the event: see
[docs/COPILOT-PROMPTS.md](docs/COPILOT-PROMPTS.md) for the JSON format, sample files, and ready-to-paste Copilot prompts
that convert your Excel sheet or question document.

## Set up the kiosk PC
1. Copy `CAM.exe` to the kiosk PC (any folder you can write to, e.g. `C:\SortingGame`). Nothing needs installing.
2. Double-click it. On first start it creates a `Data` folder next to the exe with the settings files
   and two sample files (`cards.sample.json`, `quizQuestions.sample.json`).
3. Put your cards in `Data\cards.json` and your questions in `Data\quizQuestions.json`
   ([how](docs/COPILOT-PROMPTS.md)). Until then the Play buttons are disabled.
4. **Change the admin PIN** in `Data\settings.json` (default `1234`), then restart the game.

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
| `cards.json` | Label Legends cards: `id`, `scenario`, `categoryId`, `why`. |
| `settings.json` | Label Legends: title, cards per round, scoring, time limit per card, attempts, sounds, idle time, admin PIN. |
| `quizQuestions.json` | Cyber Trivia questions: `id`, `text`, `options` (2-4), `correctIndex` (0-based), `difficulty` (1-3), `explanation`. |
| `quizSettings.json` | Cyber Trivia: title, questions per round, scoring, seconds per question, attempts. |

- Every card's `categoryId` must match a category `id` (or its `name`, e.g. `"Highly Restricted"`). Bad entries are
  skipped and listed under **Settings warnings** in Admin.
- Players get a random selection from the pool, so keep at least 2-3 times `cardsPerRound` cards.
- Delete a settings file to restore its default.
- **Upgrading from an older CAM?** An existing `Data` folder is never overwritten. Older `cards.json` files use the
  previous `label`/`example` format and will be skipped, and `settings.json`, `quizSettings.json` and
  `categories.json` keep their old titles and names. Delete those files (or the whole `Data` folder) to get the
  new defaults, then add your content again.

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
