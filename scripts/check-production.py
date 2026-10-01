#!/usr/bin/env python3
"""
Daily production smoke test for WordleMatch (https://wordlematch.8080games.com).

Checks, against the LIVE site:
  1. Data files: today's game is deployed with hints, tomorrow's game is deployed
     (i.e. the 4:30 AM fetch ran and Azure redeployed), and the used-word history
     has no gaps, duplicates or wrong dates.
  2. NYT cross-check: today's/tomorrow's words match NYT's own puzzle API.
  3. Browser: the app loads in headless Edge, shows today's puzzle number, reveals
     today's word, and the used/unused word-list counts match the deployed data.

Exit code 0 = all passed, 1 = something failed (a Windows toast is shown).

Usage:  python check-production.py [--no-browser] [--no-toast]
"""

import argparse
import csv
import io
import json
import re
import sys
import time
import urllib.request
from datetime import date, datetime, timedelta
from datetime import time as dtime

from notify import toast, utf8_stdio

utf8_stdio()

SITE = "https://wordlematch.8080games.com/"
NYT_API = "https://www.nytimes.com/svc/wordle/v2/{}.json"
WORDLE_START_DATE = date(2021, 6, 19)  # game 0
REUSE_ERA_START = 1689                 # first game of the word-reuse era
# The daily fetch runs at 4:30 AM ET (before midnight in UTC+14 Kiribati); after this time tomorrow's game must be live.
FETCH_DEADLINE = dtime(4, 45)
DEPLOY_WAIT_SECONDS = 20 * 60
# Sample guesses for the guess test; the first one that isn't today's answer is used.
# All have 5 distinct letters, so "consistent with the colors" is unambiguous.
SAMPLE_GUESSES = ['CRANE', 'SLOTH', 'PUDGY']


class Checker:
    def __init__(self):
        self.failures = []
        self.warnings = []

    def ok(self, msg):
        print(f"  [PASS] {msg}")

    def fail(self, msg):
        print(f"  [FAIL] {msg}")
        self.failures.append(msg)

    def warn(self, msg):
        print(f"  [WARN] {msg}")
        self.warnings.append(msg)

    def check(self, condition, pass_msg, fail_msg):
        if condition:
            self.ok(pass_msg)
        else:
            self.fail(fail_msg)
        return condition


def fetch(url):
    """GET a URL, bypassing caches, and return the body as text."""
    sep = '&' if '?' in url else '?'
    req = urllib.request.Request(f"{url}{sep}nocache={int(time.time())}",
                                 headers={'User-Agent': 'WordleMatch-prod-check',
                                          'Cache-Control': 'no-cache'})
    with urllib.request.urlopen(req, timeout=30) as resp:
        return resp.read().decode('utf-8-sig')


def wordle_feedback(guess, answer):
    """Standard Wordle coloring: 'green' / 'yellow' / 'white' for each letter of guess."""
    result = ['white'] * 5
    unmatched = [a for g, a in zip(guess, answer) if g != a]
    for i, (g, a) in enumerate(zip(guess, answer)):
        if g == a:
            result[i] = 'green'
        elif g in unmatched:
            result[i] = 'yellow'
            unmatched.remove(g)
    return result


def game_date(game_number):
    return WORDLE_START_DATE + timedelta(days=game_number)


def mdy(d):
    """Format a date the way the data files do: M/D/YYYY."""
    return f"{d.month}/{d.day}/{d.year}"


def check_data(c, today_game, expect_tomorrow):
    """Validate the deployed data files. Returns the parsed data for later checks."""
    print("\n== Deployed data files")

    try:
        html = fetch(SITE)
        c.check('blazor.webassembly' in html, "Home page loads (HTTP 200, Blazor app)",
                "Home page loaded but does not reference the Blazor runtime")
    except Exception as e:
        c.fail(f"Home page failed to load: {e}")

    current = json.loads(fetch(SITE + "current-games.json"))
    # After a late boot this check can start alongside the daily update; give the
    # update + Azure deploy time to land before declaring tomorrow's game missing.
    deadline = time.time() + DEPLOY_WAIT_SECONDS
    while expect_tomorrow and time.time() < deadline and \
            not any(g['gameNumber'] == today_game + 1 for g in current.get('games', [])):
        print("  Tomorrow's game not live yet -- waiting for the daily update/deploy...")
        time.sleep(60)
        current = json.loads(fetch(SITE + "current-games.json"))
    words = {w.strip().lower() for w in fetch(SITE + "words.txt").split('\n') if len(w.strip()) == 5}
    used_rows = [r for r in csv.reader(io.StringIO(fetch(SITE + "used-words.csv"))) if len(r) >= 3]
    hist_rows = [r for r in csv.reader(io.StringIO(fetch(SITE + "historical-words.csv"))) if len(r) >= 3]

    games = {g['gameNumber']: g for g in current.get('games', [])}
    print(f"  current-games.json holds games {sorted(games)}")

    # Today's game
    today = games.get(today_game)
    if c.check(today is not None, f"Today's game #{today_game} is deployed ({today and today['word']})",
               f"Today's game #{today_game} is MISSING from current-games.json"):
        c.check(today['date'] == mdy(game_date(today_game)), "Today's game has the correct date",
                f"Today's game date is {today['date']}, expected {mdy(game_date(today_game))}")
        hints = today.get('hints', {})
        c.check(hints.get('synonym') and hints.get('haiku'), "Today's game has synonym + haiku hints",
                "Today's game is missing its synonym or haiku hint")

    # Tomorrow's game (proves this morning's fetch ran and was deployed)
    tomorrow = games.get(today_game + 1)
    if tomorrow:
        c.ok(f"Tomorrow's game #{today_game + 1} is deployed ({tomorrow['word']})")
        hints = tomorrow.get('hints', {})
        c.check(hints.get('synonym') and hints.get('haiku'), "Tomorrow's game has synonym + haiku hints",
                "Tomorrow's game is missing its synonym or haiku hint")
    elif expect_tomorrow:
        c.fail(f"Tomorrow's game #{today_game + 1} is MISSING -- did the 4:30 AM fetch or the deploy fail?")
    else:
        print(f"  [INFO] Tomorrow's game not expected before {FETCH_DEADLINE:%H:%M}")

    # Used-word history: every reuse-era game from 1689 up to the newest game,
    # exactly once, with the date matching its game number.
    all_games = {}
    duplicates = []
    for word, num, d in [(r[0], r[1], r[2]) for r in used_rows] + \
                        [(g['word'], g['gameNumber'], g['date']) for g in games.values()]:
        num = int(num)
        if num in all_games and all_games[num][0].upper() != word.strip().upper():
            duplicates.append(num)
        all_games[num] = (word.strip().upper(), d.strip())
    newest = max(all_games)
    missing = [n for n in range(REUSE_ERA_START, newest + 1) if n not in all_games]
    bad_dates = [n for n, (_, d) in all_games.items() if d != mdy(game_date(n))]
    c.check(not missing, f"No gaps in game history #{REUSE_ERA_START}-#{newest}",
            f"Game history has gaps: {missing[:10]}")
    c.check(not duplicates, "No conflicting duplicate game numbers",
            f"Game numbers with conflicting words: {duplicates[:10]}")
    c.check(not bad_dates, "All game dates match their game numbers",
            f"Games with wrong dates: {bad_dates[:10]}")

    hist_max = max(int(r[1]) for r in hist_rows if r[1].strip().isdigit())
    used_max = max(int(r[1]) for r in used_rows)
    c.check(hist_max == used_max, f"historical-words.csv and used-words.csv both end at #{used_max}",
            f"historical-words.csv ends at #{hist_max} but used-words.csv ends at #{used_max}")
    c.check(newest - used_max <= 2, "Games roll from current-games.json into used-words.csv",
            f"used-words.csv ends at #{used_max} but newest game is #{newest} -- archive step broken?")

    not_in_list = [all_games[n][0] for n in (today_game, today_game + 1)
                   if n in all_games and all_games[n][0].lower() not in words]
    if not_in_list:
        c.warn(f"Answer(s) not in words.txt, so the helper can never suggest them: {not_in_list}")

    # The word list the app should show today: games 1689..today-1 are "used"
    used_today = {all_games[n][0].lower() for n in range(REUSE_ERA_START, today_game) if n in all_games}
    expected_used = len(used_today & words)
    print(f"  Expected word list: {len(words)} total, {expected_used} used, {len(words) - expected_used} unused")

    # The app shows hints from new_words_synonyms_haikus.csv, falling back to the
    # hints stored with the game in current-games.json.
    expected_hints = None
    if today:
        hint_rows = csv.reader(io.StringIO(fetch(SITE + "new_words_synonyms_haikus.csv")))
        master = {r[0].strip().upper(): (r[1].strip(), r[2].strip()) for r in hint_rows if len(r) >= 3}
        stored = (today.get('hints', {}).get('synonym', '').strip(), today.get('hints', {}).get('haiku', '').strip())
        expected_hints = master.get(today['word'].upper(), stored)
        if today['word'].upper() in master and master[today['word'].upper()] != stored:
            c.warn(f"Hints for {today['word']} differ between new_words_synonyms_haikus.csv {master[today['word'].upper()]} "
                   f"and current-games.json {stored}; the app shows the CSV version")

    return {'games': all_games, 'words': words, 'expected_used': expected_used, 'hints': expected_hints}


def check_nyt(c, today_game, data):
    print("\n== NYT cross-check")
    for n in (today_game, today_game + 1):
        if n not in data['games']:
            continue
        ours = data['games'][n][0]
        try:
            nyt = json.loads(fetch(NYT_API.format(game_date(n).isoformat())))
        except Exception as e:
            c.warn(f"Could not reach NYT API for #{n}: {e}")
            continue
        theirs = nyt.get('solution', '').upper()
        c.check(ours == theirs and nyt.get('days_since_launch') == n,
                f"#{n} {ours} matches NYT",
                f"#{n} is {ours} on the site but NYT says {theirs} (game {nyt.get('days_since_launch')})")


def check_hints(c, page, today_word, expected_hints):
    """Click each hint button and check what it shows."""
    def show(button):
        page.click(f'.hints-section .mode-btn:text-is("{button}")')
        # The button turns active in the same render that updates the hint text
        page.wait_for_selector(f'.hints-section .mode-btn.active:text-is("{button}")', timeout=5000)
        page.wait_for_selector('.hint-display', timeout=5000)
        return page.inner_text('.hint-display').strip()

    vowels = sum(ch in 'AEIOU' for ch in today_word)
    expected = f"Contains {vowels} vowel{'' if vowels == 1 else 's'}"
    shown = show('Vowels')
    c.check(shown == expected, f"Vowels hint: '{shown}'", f"Vowels hint shows '{shown}', expected '{expected}'")

    synonym, haiku = expected_hints or ('', '')
    shown = show('Synonym')
    if c.check(shown, f"Synonym hint: '{shown}'", "Synonym hint is EMPTY -- hints did not load for today's word"):
        c.check(shown == synonym, "Synonym hint matches the data files",
                f"Synonym hint shows '{shown}', expected '{synonym}'")

    shown = show('Haiku')
    lines = [line.strip() for line in shown.split('\n') if line.strip()]
    if c.check(lines, f"Haiku hint shows {len(lines)} lines", "Haiku hint is EMPTY -- hints did not load for today's word"):
        c.check(' / '.join(lines) == haiku, "Haiku hint matches the data files",
                f"Haiku hint shows '{' / '.join(lines)}', expected '{haiku}'")
        if len(lines) != 3:
            c.warn(f"Haiku has {len(lines)} lines, not 3: {lines}")

    show('Reveal')
    page.click('.reveal-confirm-btn')
    shown = page.inner_text('.reveal-word').strip().upper()
    c.check(shown == today_word, f"Reveal shows today's word {shown}",
            f"Reveal shows {shown}, expected {today_word}")


def check_sample_guess(c, page, today_word, words, wait_for_count):
    """Type a guess with auto-color on and check the colors and the narrowed word list."""
    guess = next(g for g in SAMPLE_GUESSES if g != today_word)
    colors = wordle_feedback(guess, today_word)
    expected = sum(wordle_feedback(guess, w.upper()) == colors for w in words)
    print(f"  Sample guess {guess}: expect {colors}, {len(words)} -> {expected} words")

    page.click('.filter-btn.both-btn')
    wait_for_count(page, len(words))
    page.click('.gameplay-mode-section .mode-btn:text-is("On")')
    page.keyboard.type(guess.lower(), delay=100)

    got = wait_for_count(page, expected)
    boxes = page.locator('.entry-row .letter-box')
    shown = []
    for i in range(5):
        cls = boxes.nth(i).get_attribute('class') or ''
        shown.append(next((col for col in ('green', 'yellow', 'white') if col in cls.split()), 'none'))
    c.check(shown == colors, f"Guess {guess} is auto-colored {shown}",
            f"Guess {guess} is colored {shown}, expected {colors}")
    c.check(got == expected, f"Guess {guess} narrows the list to {got} words",
            f"Guess {guess} narrows the list to {got} words, expected {expected}")

    page.click('#add-guess-btn')
    try:
        page.wait_for_selector('.previous-guess-row .count-after', timeout=5000)
        before = page.inner_text('.previous-guess-row .count-before').strip()
        after = page.inner_text('.previous-guess-row .count-after').strip()
        c.check(before == str(len(words)) and after == str(expected),
                f"Adding the guess shows {before} -> {after}",
                f"Adding the guess shows {before} -> {after}, expected {len(words)} -> {expected}")
    except Exception:
        c.fail("Clicking + did not add the guess to the board")


def check_browser(c, today_game, data):
    print("\n== Live app in headless Edge")
    from playwright.sync_api import sync_playwright, TimeoutError as PWTimeout

    today_word = data['games'].get(today_game, ('', ''))[0]
    yesterday_word = data['games'].get(today_game - 1, ('', ''))[0]
    total = len(data['words'])
    expected_used = data['expected_used']

    def results_count(page):
        m = re.search(r'\((\d+)\)', page.inner_text('.results h2'))
        return int(m.group(1)) if m else -1

    def wait_for_count(page, expected):
        try:
            page.wait_for_function(
                "n => (document.querySelector('.results h2')?.textContent || '').includes('(' + n + ')')",
                arg=expected, timeout=15000)
        except PWTimeout:
            pass
        return results_count(page)

    with sync_playwright() as p:
        browser = p.chromium.launch(channel='msedge', headless=True)
        page = browser.new_page()
        errors = []
        page.on('pageerror', lambda e: errors.append(str(e)))
        try:
            page.goto(SITE, wait_until='domcontentloaded', timeout=60000)
            reveal = page.locator('.hints-section button', has_text='Reveal')
            try:
                page.wait_for_selector('.puzzle-number', timeout=60000)
                reveal.wait_for(state='visible', timeout=30000)
                page.wait_for_function(
                    "() => [...document.querySelectorAll('.hints-section button')]"
                    ".some(b => b.textContent.trim() === 'Reveal' && !b.disabled)", timeout=30000)
                c.ok("App loaded and today's word is available (hint buttons enabled)")
            except PWTimeout:
                c.fail("App did not finish loading, or has no word for today (hint buttons stay disabled)")

            puzzle = page.inner_text('.puzzle-number').strip()
            c.check(puzzle == f"Puzzle #{today_game}", f"Shows '{puzzle}'",
                    f"Shows '{puzzle}', expected 'Puzzle #{today_game}'")

            if reveal.is_enabled():
                check_hints(c, page, today_word, data['hints'])

            page.click('.filter-btn.used-btn')
            got = wait_for_count(page, expected_used)
            c.check(got == expected_used, f"'used' list has {got} words",
                    f"'used' list has {got} words, expected {expected_used}")
            used_shown = {w.strip().upper() for w in page.locator('.word-list .used-word').all_inner_texts()}
            if yesterday_word and yesterday_word.lower() in data['words']:
                c.check(yesterday_word in used_shown, f"Yesterday's word {yesterday_word} is marked used",
                        f"Yesterday's word {yesterday_word} is NOT in the used list")
            c.check(today_word not in used_shown, f"Today's word is not marked used",
                    f"Today's word {today_word} is wrongly shown as used")

            page.click('.filter-btn.unused-btn')
            got = wait_for_count(page, total - expected_used)
            c.check(got == total - expected_used, f"'unused' list has {got} words",
                    f"'unused' list has {got} words, expected {total - expected_used}")

            if reveal.is_enabled():
                check_sample_guess(c, page, today_word, data['words'], wait_for_count)

            if errors:
                c.warn(f"Page raised JavaScript errors: {errors[:3]}")
        except Exception as e:
            c.fail(f"Browser check crashed: {e}")
        finally:
            browser.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--no-browser', action='store_true', help='skip the headless-browser checks')
    parser.add_argument('--no-toast', action='store_true', help='do not show a toast on failure')
    args = parser.parse_args()

    now = datetime.now()
    today_game = (now.date() - WORDLE_START_DATE).days
    expect_tomorrow = now.time() >= FETCH_DEADLINE

    print("=" * 70)
    print(f"WordleMatch production check - {now:%Y-%m-%d %H:%M} - today is game #{today_game}")
    print("=" * 70)

    c = Checker()
    data = None
    try:
        data = check_data(c, today_game, expect_tomorrow)
    except Exception as e:
        c.fail(f"Could not load data files from {SITE}: {e}")

    if data:
        check_nyt(c, today_game, data)
        if not args.no_browser:
            check_browser(c, today_game, data)

    print("\n" + "=" * 70)
    if c.failures:
        print(f"RESULT: FAILED ({len(c.failures)} failure(s), {len(c.warnings)} warning(s))")
        for f in c.failures:
            print(f"  - {f}")
        if not args.no_toast:
            toast("WordleMatch production check FAILED", c.failures[0])
        return 1
    print(f"RESULT: PASSED ({len(c.warnings)} warning(s))")
    return 0


if __name__ == '__main__':
    sys.exit(main())
