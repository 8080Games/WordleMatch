# Daily Wordle Runner — Setup

How to make a Windows machine the daily Wordle word fetcher. The job pulls
**tomorrow's** answer from the NYT "Wordle Review" page (the source of truth)
via a real, logged-in Edge browser, then commits + pushes via `add-word.mjs`.

> NYT blocks remote/CI automation, so this **must** run locally on a real machine
> with a logged-in browser. Do not use GitHub Actions for the daily fetch.

## Prerequisites
- **Python** with the Playwright package: `python -m pip install playwright`
  (no browser download needed — it attaches to a running Edge over CDP).
  Verify `where python` in cmd resolves to that interpreter.
- **Node.js** on PATH (runs `add-word.mjs`).
- **Git** push credentials configured for `github.com/8080Games/WordleMatch`.
- **Microsoft Edge** installed.

## One-time setup

### 1. Stable debug profile + NYT login
`%TEMP%` can be wiped (dropping the login), so use a permanent folder. Launch:
```cmd
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --remote-debugging-port=9222 --user-data-dir="C:\Users\chris\edge-debug-profile"
```
In that window: log into **nytimes.com**, confirm a Wordle Review article opens,
then close it. The login persists in that profile.

### 2. Point `start-edge-debug.bat` at the stable profile
Change the launch line's profile path:
```
--user-data-dir="%TEMP%\edge-debug-profile"   ->   --user-data-dir="C:\Users\chris\edge-debug-profile"
```

### 3. Add a pull to `run-daily-fetch.bat`
Right after `cd /d "%~dp0"` near the top, insert:
```bat
git pull --ff-only >> "%LOGFILE%" 2>&1
```
Prevents the "fetch first" push rejection if the remote moved.

### 4. Create the scheduled task (example: 6:05 AM daily, only when logged on)
```cmd
schtasks /Create /TN "Wordle Daily Fetch" /TR "C:\AI\claude\Wordle\scripts\run-daily-fetch.bat" /SC DAILY /ST 06:05 /F
```
No `/RU` → runs as the current user, **only when logged on** (a browser-driven task
needs this). Then in Task Scheduler → this task → **Settings**, enable
**"Run task as soon as possible after a scheduled start is missed"** so a late boot
still triggers the day's run (and optionally **"Wake the computer to run this task"**).

### 5. Test
```cmd
schtasks /Run /TN "Wordle Daily Fetch"
```
Check the newest file in `scripts\logs\` — expect `[OK] Found word:` and the real
`add-word.mjs` summary (the cp1252 `charmap` crash is fixed in `fetch-nyt-word.py`).

## Operating notes
- The machine must be **on and logged in** at the scheduled time (or rely on the
  missed-start / wake settings).
- **NYT logins expire** periodically. If a run logs "Could not find reveal button,"
  redo step 1 to refresh the login.
- The daily job fetches **one** word/day (tomorrow's). After downtime, run
  **`python scripts\backfill-words.py`** once to fill the gap — it loops the missing
  game-number range, validates against a known answer (#1824 = TOKEN) before writing,
  and aborts if any fetch fails.
- Always cross-check a fetched word against an independent source if anything looks
  off — the automation once recorded a wrong answer (#1826 ALIGN vs the real EMOJI).

## Data model (handled by `add-word.mjs WORD M/D/YYYY`)
- `wwwroot/current-games.json` — 2 most-recent games (sliding window) + `recentUsedWords`.
- Archived games → `wwwroot/historical-words.csv` + `wwwroot/used-words.csv`.
- Hints → `wwwroot/historical_hints.csv` (master lookup `wwwroot/3158_wordle_hints.csv`).
- Game number = days since 2021-06-19. The review article on date D reveals game D+1,
  so for game G the URL date is (G's date − 1): `nytimes.com/{Y}/{MM}/{DD}/crosswords/wordle-review-{G}.html`.
