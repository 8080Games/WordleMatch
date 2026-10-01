#!/usr/bin/env python3
"""
Daily Wordle data update (self-healing).

Adds every reuse-era game that is missing from the data files, from #1689 through
tomorrow's game, using the NYT puzzle API (no browser). A normal day adds just
tomorrow's word; after downtime it back-fills every missed day in one commit.

  1. git pull --ff-only
  2. Work out which games are missing from used-words.csv + current-games.json
  3. Look each one up at nytimes.com/svc/wordle/v2/YYYY-MM-DD.json
  4. Add them oldest-first via `node add-word.mjs WORD YYYY-MM-DD --no-git`
  5. Re-read the files to confirm every game landed, then make ONE commit of the
     data files only, and push (also pushes any earlier commit that failed to push)

Usage:  python daily-update.py [--dry-run] [--no-push] [--max-missing N] [--no-toast]
Exit code 0 = up to date (and pushed), 1 = something failed (a toast is shown).
The old browser scraper (fetch-nyt-word.py / backfill-words.py) remains for manual use.
"""

import argparse
import csv
import json
import os
import re
import subprocess
import sys
import time
import urllib.request
from datetime import date, timedelta

from notify import toast, utf8_stdio

utf8_stdio()

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_DIR = os.path.dirname(SCRIPT_DIR)
WWWROOT = os.path.join(REPO_DIR, 'wwwroot')
NYT_API = "https://www.nytimes.com/svc/wordle/v2/{}.json"
WORDLE_START_DATE = date(2021, 6, 19)  # game 0
REUSE_ERA_START = 1689
# The only files add-word.mjs edits; commit exactly these so unrelated work-tree
# changes never get swept into the automated commit.
DATA_FILES = ['wwwroot/current-games.json', 'wwwroot/used-words.csv', 'wwwroot/historical-words.csv',
              'wwwroot/words.txt', 'wwwroot/guess-only-words.txt', 'wwwroot/historical_hints.csv']


def game_date(game_number):
    return WORDLE_START_DATE + timedelta(days=game_number)


def git(*args, check=False):
    r = subprocess.run(['git', *args], cwd=REPO_DIR, capture_output=True, text=True,
                       encoding='utf-8', errors='replace')
    output = (r.stdout + r.stderr).strip()
    if output:
        print(f"  $ git {' '.join(args)}\n    " + output.replace('\n', '\n    '))
    if check and r.returncode != 0:
        raise RuntimeError(f"git {args[0]} failed")
    return r


def known_games():
    """Return {game_number: WORD} for every reuse-era game in the local data files."""
    games = {}
    with open(os.path.join(WWWROOT, 'used-words.csv'), encoding='utf-8-sig') as f:
        for row in csv.reader(f):
            if len(row) >= 3 and row[1].strip().isdigit():
                games[int(row[1].strip())] = row[0].strip().upper()
    with open(os.path.join(WWWROOT, 'current-games.json'), encoding='utf-8-sig') as f:
        current = json.load(f)
    for g in current.get('games', []):
        games[g['gameNumber']] = g['word'].strip().upper()
    return games, current


def missing_games(through_game):
    games, _ = known_games()
    return [n for n in range(REUSE_ERA_START, through_game + 1) if n not in games]


def fetch_answer(game_number, attempts=3, wait_seconds=20):
    """Look up a game's answer from the NYT API. Returns WORD or None."""
    url = NYT_API.format(game_date(game_number).isoformat())
    for attempt in range(1, attempts + 1):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': 'WordleMatch-daily-update'})
            with urllib.request.urlopen(req, timeout=30) as resp:
                data = json.loads(resp.read().decode('utf-8'))
            word = str(data.get('solution', '')).strip().upper()
            if data.get('days_since_launch') != game_number or not re.fullmatch(r'[A-Z]{5}', word):
                print(f"  [ERROR] #{game_number}: unexpected API response {data}")
                return None
            return word
        except Exception as e:
            print(f"  [WARN] #{game_number}: attempt {attempt}/{attempts} failed: {e}")
            if attempt < attempts:
                time.sleep(wait_seconds)
    return None


def add_word(game_number, word):
    """Run add-word.mjs for one game without committing. Returns True on success."""
    r = subprocess.run(['node', 'add-word.mjs', word, game_date(game_number).isoformat(), '--no-git'],
                       cwd=SCRIPT_DIR, capture_output=True, text=True, encoding='utf-8', errors='replace')
    print(r.stdout)
    if r.stderr:
        print(r.stderr)
    if r.returncode != 0:
        print(f"  [ERROR] add-word.mjs failed for {word} (#{game_number})")
        return False
    if f"Game: #{game_number}" not in r.stdout:
        print(f"  [ERROR] add-word.mjs computed a different game number for {word} (expected #{game_number})")
        return False
    return True


def commit(added):
    if not git('status', '--porcelain', '--', *DATA_FILES).stdout.strip():
        print("  No data file changes to commit")
        return
    if len(added) == 1:
        n, w = added[0]
        d = game_date(n)
        message = f"Add Wordle word: {w} (game #{n}, {d.month}/{d.day}/{d.year})"
    else:
        message = "Add Wordle words: " + ", ".join(f"{w} (#{n})" for n, w in added)
    git('add', '--', *DATA_FILES, check=True)
    git('commit', '-m', message, '--', *DATA_FILES, check=True)


def push():
    """Push any unpushed commits. Rebase onto the remote and retry once if rejected."""
    git('fetch', '--quiet')
    ahead = git('rev-list', '--count', '@{u}..HEAD').stdout.strip()
    if ahead in ('', '0'):
        print("  Nothing to push")
        return True
    if git('push').returncode == 0:
        return True
    print("  Push rejected -- rebasing onto the remote and retrying")
    if git('pull', '--rebase', '--autostash').returncode != 0:
        git('rebase', '--abort')
        return False
    return git('push').returncode == 0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--dry-run', action='store_true', help='look up missing words and print them; change nothing')
    parser.add_argument('--no-push', action='store_true', help='commit locally but do not push')
    parser.add_argument('--max-missing', type=int, default=60,
                        help='abort if more games than this are missing (default 60)')
    parser.add_argument('--no-toast', action='store_true', help='do not show a toast on failure')
    args = parser.parse_args()

    def failed(message):
        print(f"\nRESULT: FAILED - {message}")
        if not args.no_toast:
            toast("Wordle daily update FAILED", message)
        return 1

    today_game = (date.today() - WORDLE_START_DATE).days
    tomorrow_game = today_game + 1
    print("=" * 70)
    print(f"Wordle daily update - {date.today()} - today #{today_game}, tomorrow #{tomorrow_game}")
    print("=" * 70)

    if not args.dry_run:
        print("\n[1/5] Syncing with remote")
        if git('pull', '--ff-only').returncode != 0:
            print("  [WARN] git pull --ff-only failed; will try to rebase at push time")

    print("\n[2/5] Finding missing games")
    needed = missing_games(tomorrow_game)
    print(f"  Missing: {needed if needed else 'none'}")
    if len(needed) > args.max_missing:
        return failed(f"{len(needed)} games missing (cap {args.max_missing}). "
                      f"Check the data, then re-run with --max-missing.")

    added, unresolved = [], []
    if needed:
        print("\n[3/5] Looking up answers from the NYT API")
        resolved = []
        for n in needed:
            word = fetch_answer(n)
            print(f"  #{n}  {game_date(n)}  ->  {word or 'FAILED'}")
            if word:
                resolved.append((n, word))
            else:
                unresolved.append(n)

        if args.dry_run:
            print("\nDry run: nothing written.")
            return 1 if unresolved else 0

        print("\n[4/5] Adding words")
        for n, word in resolved:
            if not add_word(n, word):
                break
            added.append((n, word))

        # Trust the files, not the exit codes: every added game must now be present
        games, current = known_games()
        lost = [n for n, w in added if games.get(n) != w]
        if lost:
            return failed(f"Games {lost} did not land in the data files; nothing committed. "
                          f"Inspect wwwroot/ and the log.")
        for g in current.get('games', []):
            if not (g.get('hints', {}).get('synonym') and g.get('hints', {}).get('haiku')):
                print(f"  [WARN] No hints for #{g['gameNumber']} {g['word']} -- add them to 3158_wordle_hints.csv")
                if not args.no_toast:
                    toast("Wordle hints missing", f"#{g['gameNumber']} {g['word']} has no synonym/haiku")
        if added:
            commit(added)
    elif args.dry_run:
        print("\nDry run: nothing to do.")
        return 0

    print("\n[5/5] Pushing")
    if args.no_push:
        print("  Skipped (--no-push)")
    elif not push():
        return failed("git push failed. The commit is saved locally and will be pushed on the next run.")

    if unresolved or len(added) < len(needed):
        return failed(f"Could not add games {[n for n in needed if n not in dict(added)]}. "
                      f"The next run will retry.")
    print(f"\nRESULT: OK - added {len(added)} game(s)" + (f": {', '.join(w for _, w in added)}" if added else ""))
    return 0


if __name__ == '__main__':
    sys.exit(main())
