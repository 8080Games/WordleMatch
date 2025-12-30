#!/usr/bin/env python3
"""
Fetch tomorrow's Wordle word from NYT review article.
Runs locally with a visible browser window.
"""

import asyncio
import re
import subprocess
from datetime import datetime, timedelta
from playwright.async_api import async_playwright

async def fetch_nyt_wordle():
    """Fetch tomorrow's Wordle word from NYT review article."""

    # Calculate tomorrow's date and game number
    tomorrow = datetime.now() + timedelta(days=1)
    WORDLE_START_DATE = datetime(2021, 6, 19)
    game_number = (tomorrow - WORDLE_START_DATE).days

    # Use today's article (which contains tomorrow's word)
    today = datetime.now()
    year = today.year
    month = str(today.month).zfill(2)
    day = str(today.day).zfill(2)

    # NYT review URL format: uses TODAY's date but has TOMORROW's puzzle
    url = f"https://www.nytimes.com/{year}/{month}/{day}/crosswords/wordle-review-{game_number}.html"

    print("=" * 70)
    print("NYT WORDLE WORD FETCHER")
    print("=" * 70)
    print(f"Tomorrow's date: {tomorrow.strftime('%m/%d/%Y')}")
    print(f"Game number: #{game_number}")
    print(f"Review URL: {url}")
    print()
    print("Opening browser... (you may need to log in to NYT)")
    print("=" * 70)
    print()

    async with async_playwright() as p:
        # Connect to existing browser (Chrome or Edge) via CDP
        try:
            print("Connecting to your browser...")
            browser = await p.chromium.connect_over_cdp("http://localhost:9222")
            print("[OK] Connected to browser!")
        except Exception as e:
            print()
            print("=" * 70)
            print("[ERROR] Could not connect to browser")
            print("=" * 70)
            print()
            print("Please start Edge or Chrome with remote debugging enabled:")
            print()
            print("  Option 1 - Edge (recommended):")
            print("    Run: start-edge-debug.bat")
            print()
            print("  Option 2 - Chrome:")
            print("    Run: start-chrome-debug.bat")
            print()
            print("=" * 70)
            return None

        # Get the default context (your existing Chrome profile)
        contexts = browser.contexts
        if contexts:
            context = contexts[0]
        else:
            print("[ERROR] No browser context found")
            await browser.close()
            return None

        # Create a new page/tab
        page = await context.new_page()

        try:
            print("[1/4] Navigating to NYT review article...")
            await page.goto(url, timeout=30000, wait_until='domcontentloaded')

            # Wait a moment for page to load
            await page.wait_for_timeout(2000)

            print("[2/4] Page loaded successfully!")
            print("[3/4] Looking for 'Click to reveal' button...")

            # Find and click the "Click to reveal" button
            try:
                # Try various selectors for the reveal button
                reveal_selectors = [
                    'text="Click to reveal"',
                    'button:has-text("Click to reveal")',
                    'button:has-text("reveal")',
                    '[aria-label*="reveal"]',
                    '.reveal-button',
                    'button.spoiler-reveal'
                ]

                clicked = False
                for selector in reveal_selectors:
                    try:
                        element = await page.wait_for_selector(selector, timeout=3000)
                        if element:
                            await element.click()
                            print(f"[OK] Clicked reveal button!")
                            clicked = True
                            await page.wait_for_timeout(1000)  # Wait for answer to appear
                            break
                    except:
                        continue

                if not clicked:
                    print("[WARNING] Could not find reveal button, trying to extract anyway...")

            except Exception as e:
                print(f"[WARNING] Error clicking reveal: {e}")

            print("[4/4] Extracting Wordle answer from page...")

            # Get page text content
            page_text = await page.text_content('body')

            # Look for the answer using multiple patterns
            patterns = [
                r'Today.?s?\s+word\s+is\s+([A-Z]{5})',  # "Today's word is FRUIT"
                r'Today.?s?\s+Word[:\s]+([A-Z]{5})',  # "Today's Word: FRUIT"
                r'answer\s+to\s+Wordle\s+\d+\s+is\s+([A-Z]{5})',
                r'answer\s+is\s+([A-Z]{5})',
                r'solution\s+is\s+([A-Z]{5})',
                r'Wordle\s+\d+\s+answer[:\s]+([A-Z]{5})',
                r'\b([A-Z]{5})\s+is\s+the\s+answer',
                r'The word is[:\s]+([A-Z]{5})'
            ]

            word = None
            for pattern in patterns:
                match = re.search(pattern, page_text, re.IGNORECASE)
                if match:
                    word = match.group(1).upper()
                    print(f"[OK] Found word: {word}")
                    break

            if not word:
                print()
                print("[ERROR] Could not find the Wordle answer on the page.")
                print("The page might have a different format than expected.")

                # Save page for debugging
                try:
                    import os
                    html = await page.content()
                    debug_path = os.path.join(os.path.dirname(__file__), 'debug-page.html')
                    with open(debug_path, 'w', encoding='utf-8') as f:
                        f.write(html)
                    print(f"\nPage saved to: {debug_path}")
                    print("First 1000 chars of page text:")
                    print("-" * 70)
                    print(page_text[:1000] if page_text else "No text found")
                    print("-" * 70)
                except Exception as e:
                    print(f"Could not save debug info: {e}")

                print()
                # Don't close browser - let user see what's on the page
                return None

            date_str = tomorrow.strftime('%m/%d/%Y')

            # Display what we found
            print()
            print("=" * 70)
            print("INFORMATION GATHERED")
            print("=" * 70)
            print(f"Word:        {word}")
            print(f"Game:        #{game_number}")
            print(f"Date:        {date_str}")
            print("=" * 70)
            print()

            # Close the tab we opened
            await page.close()
            print("[OK] Browser tab closed")

            # Close browser connection and kill Edge processes
            await browser.close()

            import os
            try:
                if os.name == 'nt':  # Windows
                    os.system('taskkill /F /IM msedge.exe >nul 2>&1')
                else:  # Mac/Linux
                    os.system('pkill -f msedge')
                print("[OK] Browser closed")
            except Exception as e:
                print(f"[WARNING] Could not close browser: {e}")

            return {
                'word': word,
                'game_number': game_number,
                'date': date_str
            }

        except Exception as e:
            print(f"[ERROR] {e}")
            print()
            try:
                await browser.close()
                # Kill Edge process
                import os
                if os.name == 'nt':
                    os.system('taskkill /F /IM msedge.exe >nul 2>&1')
                else:
                    os.system('pkill -f "msedge.*remote-debugging-port=9222"')
            except:
                pass
            return None

def add_word_to_database(word_data):
    """Add the word using add-word.mjs script."""
    try:
        print("=" * 70)
        print(f"[5/5] Adding {word_data['word']} to database...")
        print("=" * 70)
        print()

        result = subprocess.run(
            ['node', 'add-word.mjs', word_data['word'], word_data['date']],
            cwd='scripts',
            capture_output=True,
            text=True,
            encoding='utf-8',
            errors='replace'
        )

        print(result.stdout)
        if result.stderr:
            print(result.stderr)

        if result.returncode != 0:
            # Check if it's just a duplicate
            combined_output = result.stdout + (result.stderr or "")
            if "already exists" in combined_output or "No changes made" in combined_output:
                print()
                print("=" * 70)
                print("[INFO] Word was not added - already exists in database")
                print("=" * 70)
                return False
            else:
                print()
                print("=" * 70)
                print("[ERROR] Failed to add word to database")
                print("=" * 70)
                return False

        return True

    except Exception as e:
        print(f"[ERROR] {e}")
        return False

async def main():
    print()

    # Fetch the word from NYT
    word_data = await fetch_nyt_wordle()

    if not word_data:
        print()
        print("[FAILED] Could not fetch Wordle word")
        return

    # Add to database
    added = add_word_to_database(word_data)

    print()
    print("=" * 70)
    print("FINAL RESULT")
    print("=" * 70)
    if added:
        print(f"SUCCESS: {word_data['word']} added to database and committed to GitHub!")
    else:
        print(f"COMPLETE: {word_data['word']} was already in the database, no changes made")
    print("=" * 70)
    print()

if __name__ == "__main__":
    asyncio.run(main())
