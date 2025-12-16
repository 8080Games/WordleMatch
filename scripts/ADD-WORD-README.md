# Manual Word Addition Tool

Simple script to manually add Wordle words to `used-words.csv`.

## Quick Start

Choose your preferred method:

```bash
# Method 1: npm script (easiest, works anywhere)
cd scripts
npm run add TRUCK

# Method 2: batch file (Windows, from scripts directory)
cd scripts
add-word TRUCK

# Method 3: direct node (if you prefer)
cd scripts
node add-word.mjs TRUCK
```

## Usage

All methods support the same arguments:

```bash
<command> <word> [date] [--commit]
```

Where `<command>` is one of:
- `npm run add` (recommended)
- `add-word` (Windows batch file)
- `node add-word.mjs` (direct)

### Arguments

- **word** (required): 5-letter word to add (case insensitive)
- **date** (optional): Date in `YYYY-MM-DD` or `MM/DD/YYYY` format
  - If omitted, uses the **next sequential game** (last game number + 1)
  - Falls back to tomorrow if file is empty
- **--commit** (optional): Automatically commit and push changes

## Examples

### Add next word (auto-detects next game number)
```bash
npm run add TRUCK
# or: add-word TRUCK
# Automatically adds for the next game after the last entry in the file
```

### Add word for specific date
```bash
npm run add TRUCK 2025-12-16
# or: add-word TRUCK 2025-12-16
```

### Add and commit in one step
```bash
npm run add TRUCK -- --commit
# or: add-word TRUCK --commit
```

### Add for specific date and commit
```bash
npm run add TRUCK 12/16/2025 -- --commit
# or: add-word TRUCK 12/16/2025 --commit
```

**Note:** When using `npm run add`, put `--` before flags like `--commit`

## Features

- ✅ Validates word (must be 5 letters, letters only)
- ✅ Calculates game number automatically from date
- ✅ Checks for duplicates
- ✅ Sorts entries by game number
- ✅ Optional auto-commit and push
- ✅ Warns about conflicts (same game different word, same word different game)

## What It Does

1. Validates the word (5 letters, A-Z only)
2. Calculates the Wordle game number from the date
3. Checks for duplicate entries
4. Adds the entry to `used-words.csv`
5. Sorts the file by game number
6. Optionally commits and pushes changes

## Output

The script provides clear feedback:
- ✓ Success messages
- ⚠️ Warnings for duplicates/conflicts
- ❌ Error messages for invalid input
- Next steps if not auto-committing
