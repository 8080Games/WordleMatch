# NYT Wordle Word Fetcher

Local script to fetch tomorrow's Wordle word from the New York Times review article.

## Prerequisites

1. **Python 3.7+** with pip installed
2. **Node.js** (already installed if you can run add-word.mjs)
3. **NYT Subscription** (or free account with Wordle access)

## First Time Setup

### 1. Install Python dependencies

```bash
pip install playwright
```

### 2. Install Playwright browsers

```bash
playwright install chromium
```

### 3. Install Node dependencies (if not already done)

```bash
cd scripts
npm install
```

## Usage

### Step 1: Start Chrome with Debugging

**Windows:**
```bash
cd scripts
start-chrome-debug.bat
```

This starts your regular Chrome browser with remote debugging enabled. **Keep Chrome running.**

**Mac/Linux:**
```bash
/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --remote-debugging-port=9222 &
```

### Step 2: Run the Fetch Script

**Windows:**
```bash
cd scripts
fetch-nyt-word.bat
```

**Mac/Linux:**
```bash
cd scripts
python3 fetch-nyt-word.py
```

## How It Works

1. **Connects to your Chrome browser** - Uses your existing Chrome with your NYT login
2. **Opens a new tab** - Navigates to NYT review article for tomorrow's puzzle
3. **Extracts the answer** - Searches the article for the Wordle word
4. **Adds to database** - Calls `add-word.mjs` to update files and commit
5. **Closes the tab** - Leaves Chrome running for next time

## First Run

On your first run:
1. Start Chrome with debugging enabled (use `start-chrome-debug.bat`)
2. Make sure you're logged into nytimes.com in Chrome
3. Run the fetch script
4. It will open a new tab, grab the word, and close the tab
5. Future runs work the same way

## Troubleshooting

### "Could not connect to Chrome"
- Make sure Chrome is running with debugging enabled
- Run `start-chrome-debug.bat` first
- Check that port 9222 isn't blocked by firewall

### "Could not find the Wordle answer"
- The article format may have changed
- Check if you're logged into NYT in Chrome
- Try manually opening the URL shown to see if the article exists
- The article usually publishes around 7 AM ET

### Login/subscription issues
- Log into nytimes.com in your Chrome browser first
- Make sure you have access to the Games section
- The script uses your existing Chrome session/cookies

## Schedule

The NYT review article typically publishes:
- **Around 7 AM ET** each day
- Contains **tomorrow's** Wordle answer

Recommended schedule:
- Run this script anytime after 7 AM ET
- This gives you tomorrow's word to add to the database

## Files Modified

- `wwwroot/used-words.csv` - Adds the new word
- `wwwroot/word-hints.csv` - Adds hints if available
- `wwwroot/words.txt` - Adds word to dictionary if missing

Changes are automatically committed and pushed to GitHub.

## Security Note

The script uses your existing Chrome browser session and cookies. Make sure you trust the environment where you run this script, as it has access to your logged-in NYT account.
