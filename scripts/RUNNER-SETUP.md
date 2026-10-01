# Daily Wordle Runner — Setup

How to make a Windows machine the daily Wordle word updater. The job looks up
answers from the NYT puzzle API (`nytimes.com/svc/wordle/v2/YYYY-MM-DD.json`),
adds them via `add-word.mjs`, and makes one commit + push (which triggers the
Azure Static Web Apps deploy). No browser is involved.

> NYT reportedly blocks remote/CI automation, so this runs locally rather than in
> GitHub Actions.

## How it works (`run-daily-fetch.bat` → `daily-update.py`)
1. `git pull --ff-only`
2. Finds every reuse-era game (#1689 through **tomorrow**) missing from
   `used-words.csv` + `current-games.json`.
3. Looks each one up in the NYT API (3 attempts each).
4. Adds them oldest-first with `node add-word.mjs WORD YYYY-MM-DD --no-git`.
5. Re-reads the data files to confirm every game landed, commits **only** the
   wwwroot data files, and pushes (rebasing and retrying once if rejected; also
   pushes any earlier commit that failed to push).

It is **self-healing**: a normal day adds just tomorrow's word, and after any
downtime the first run back-fills every missed day in one commit. Re-running is
safe — if nothing is missing it does nothing.

Failures show a Windows toast and make the task's Last Result non-zero. Logs go to
`scripts\logs\fetch-*.log`.

Useful flags: `--dry-run` (look up and print, change nothing), `--no-push`,
`--max-missing N` (default 60; the job refuses to add more than this at once).

## Daily production check (`run-prod-check.bat` → `check-production.py`)
Runs at 4:50 AM against the live site and toasts on failure. It checks that today's
and tomorrow's games are deployed with hints, the game history has no gaps, the
words match the NYT API, and that the app in headless Edge shows the right puzzle
number, reveals the right word, and shows the right used/unused counts. If
tomorrow's game isn't live yet it waits up to 20 minutes for the update + deploy.
Logs go to `scripts\logs\prod-check-*.log`.

## Prerequisites
- **Python** on PATH (`where python` in cmd). The daily update uses only the
  standard library; the production check also needs `python -m pip install playwright`
  (it drives the installed Microsoft Edge, no browser download needed).
- **Node.js** on PATH (runs `add-word.mjs`).
- **Git** push credentials for `github.com/8080Games/WordleMatch`.
- `wwwroot/3158_wordle_hints.csv` (hint source for `add-word.mjs`; currently untracked).

## Scheduled tasks
The fetch runs at 4:30 AM ET so tomorrow's word is live before midnight in the
earliest timezone (Kiribati's Line Islands, UTC+14 — midnight there is 5:00 AM EST /
6:00 AM EDT). The NYT API publishes tomorrow's answer well before then.

Both run as the current user, only when logged on, with **"Run task as soon as
possible after a scheduled start is missed"** and **"Wake the computer to run this
task"** enabled.

| Task | Script | Triggers |
|---|---|---|
| Wordle Daily Fetch | `scripts\run-daily-fetch.bat` | 4:30 AM and 12:05 PM (second run is a retry; no-op if up to date) |
| WordleMatch Prod Check | `scripts\run-prod-check.bat` | 4:50 AM |

```cmd
schtasks /Create /TN "Wordle Daily Fetch" /TR "C:\AI\claude\Wordle\scripts\run-daily-fetch.bat" /SC DAILY /ST 04:30 /F
schtasks /Create /TN "WordleMatch Prod Check" /TR "C:\AI\claude\Wordle\scripts\run-prod-check.bat" /SC DAILY /ST 04:50 /F
```
Then in Task Scheduler enable the two settings above on each task, and add the
12:05 PM trigger to the fetch task. Test with `schtasks /Run /TN "<task name>"` and
check the newest log in `scripts\logs\`.

## Operating notes
- The machine must be on (or asleep) and logged in for the tasks to run; a shut-down
  PC catches up at the next logon.
- If the update toasts "N games missing (cap 60)", check the data, then run
  `python scripts\daily-update.py --max-missing <N>` once.
- If a toast says hints are missing for a word, add its synonym/haiku to
  `wwwroot/3158_wordle_hints.csv` and re-run `add-word.mjs` for that game.
- The old browser scrapers (`fetch-nyt-word.py`, `backfill-words.py`) are kept for
  manual use if the NYT API ever stops working.

## Data model (handled by `add-word.mjs WORD YYYY-MM-DD`)
- `wwwroot/current-games.json` — 2 most-recent games (sliding window) + `recentUsedWords`.
- Archived games → `wwwroot/historical-words.csv` + `wwwroot/used-words.csv`.
- Hints → `wwwroot/historical_hints.csv` (master lookup `wwwroot/3158_wordle_hints.csv`).
- Game number = days since 2021-06-19.
